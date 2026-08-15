using System.Globalization;
using DarsJadvali.Application.Import;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>
/// Sessiyaning "tuzilma" qismi: sinflar, bo'linishlar, guruhlar, dars ta'riflari va
/// kartochkalar.
/// </summary>
internal sealed partial class AscImportSession
{
    // -------------------------------------------------------------------------
    // Sinflar
    // -------------------------------------------------------------------------

    private async Task ImportClassesAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.SchoolClass);

        var existing = await _db.SchoolClasses
            .IgnoreQueryFilters()
            .Where(c => c.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var live = existing.Where(c => !c.IsDeleted).ToList();
        var names = new UniqueNames(50, live.Select(c => c.Name));
        var shorts = new UniqueNames(24, live.Select(c => c.ShortName));

        var byExternal = BuildExternalIndex(existing, c => c.ExternalId);
        var byShort = live
            .GroupBy(c => c.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var source in _doc.Classes)
        {
            stat.Found++;

            var nameCandidate = source.Name.Length > 0 ? source.Name : (source.Short ?? source.Id);
            var shortCandidate = source.Short ?? nameCandidate;

            var schoolClass = Lookup(byExternal, byShort, source.Id, source.Short);

            if (schoolClass is null)
            {
                schoolClass = new SchoolClass
                {
                    AcademicYearId = _year.Id,
                    Name = names.Take(nameCandidate, source.Id),
                    ShortName = shorts.Take(shortCandidate, source.Id),
                    ExternalId = Cut(source.Id, 64)
                };
                _db.SchoolClasses.Add(schoolClass);
                stat.Created++;
            }
            else
            {
                names.Release(schoolClass.Name);
                shorts.Release(schoolClass.ShortName);

                schoolClass.AcademicYearId = _year.Id;
                schoolClass.Name = names.Take(nameCandidate, source.Id);
                schoolClass.ShortName = shorts.Take(shortCandidate, source.Id);
                schoolClass.ExternalId = Cut(source.Id, 64);
                if (schoolClass.IsDeleted) schoolClass.IsDeleted = false;
                stat.Updated++;
            }

            ApplyClassReferences(schoolClass, source);
            _classByAscId[source.Id] = schoolClass;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Sinfning parallel / sinf rahbari / uy xonasi havolalarini bog'laydi.</summary>
    private void ApplyClassReferences(SchoolClass schoolClass, AscClass source)
    {
        if (!string.IsNullOrWhiteSpace(source.GradeKey))
        {
            if (_gradeByKey.TryGetValue(source.GradeKey, out var grade))
            {
                schoolClass.GradeId = grade.Id;
            }
            else
            {
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-GRADE",
                    $"'{schoolClass.Name}' sinfining paralleli topilmadi (grade = {source.GradeKey}).",
                    source.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(source.TeacherId))
        {
            if (_teacherByAscId.TryGetValue(source.TeacherId, out var teacher))
            {
                schoolClass.ClassTeacherId = teacher.Id;
            }
            else
            {
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-TEACHER",
                    $"'{schoolClass.Name}' sinfining rahbari topilmadi (teacherid = {source.TeacherId}).",
                    source.Id);
            }
        }

        // classes.classroomids — RO'YXAT, bizda esa bitta "asosiy xona": birinchisi olinadi.
        foreach (var roomId in source.ClassroomIds)
        {
            if (_classroomByAscId.TryGetValue(roomId, out var classroom))
            {
                schoolClass.HomeClassroomId = classroom.Id;
                break;
            }

            AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-CLASSROOM",
                $"'{schoolClass.Name}' sinfining uy xonasi topilmadi (classroomid = {roomId}).",
                source.Id);
        }

        if (source.ClassroomIds.Count > 1)
        {
            AddMessage(ImportSeverity.Info, "ASC-MULTI-HOMEROOM",
                $"'{schoolClass.Name}' sinfida {source.ClassroomIds.Count} ta uy xonasi ko'rsatilgan — " +
                "faqat birinchisi saqlandi.", source.Id);
        }
    }

    // -------------------------------------------------------------------------
    // Bo'linishlar va guruhlar — importning eng nozik joyi
    // -------------------------------------------------------------------------

    /// <summary>
    /// aSc <c>groups</c> → <see cref="ClassDivision"/> + <see cref="StudentGroup"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Asosiy qoida (aSc #1895).</b> Bir xil <c>divisiontag</c> ga ega guruhlar
    /// BITTA bo'linishga tegishli va faqat shu guruhlar bir vaqtda dars o'ta oladi.
    /// Shuning uchun <c>divisiontag</c> to'g'ridan-to'g'ri
    /// <see cref="ClassDivision.DivisionTag"/> ga o'giriladi, guruh esa o'sha
    /// bo'linishning bolasi bo'ladi. Bandlik esa guruh aniqligida yozilgani uchun
    /// bir bo'linishdagi ikki guruh bitta slotda yonma-yon tura oladi.</para>
    /// <para><b>"Butun sinf" kafolati.</b> Har sinfda AYNAN BITTA
    /// <see cref="StudentGroup.IsEntireClass"/> guruh bo'lishi shart
    /// (<c>UX_StudentGroups_SchoolClassId_EntireClass</c>). aSc'da bunday guruh
    /// bo'lmasa — yaratiladi; bir nechta bo'lsa — birinchisidan boshqasi oddiy
    /// guruhga aylantiriladi.</para>
    /// </remarks>
    private async Task ImportGroupsAsync(CancellationToken ct)
    {
        var divisionStat = Stat(ImportEntityKind.ClassDivision);
        var groupStat = Stat(ImportEntityKind.StudentGroup);
        groupStat.Found += _doc.Groups.Count;

        var classIds = _classByAscId.Values.Select(c => c.Id).Where(id => id != 0).ToList();

        var existingDivisions = classIds.Count == 0
            ? new List<ClassDivision>()
            : await _db.ClassDivisions
                .Where(d => classIds.Contains(d.SchoolClassId))
                .ToListAsync(ct).ConfigureAwait(false);

        var existingGroups = classIds.Count == 0
            ? new List<StudentGroup>()
            : await _db.StudentGroups
                .IgnoreQueryFilters()
                .Where(g => classIds.Contains(g.SchoolClassId))
                .ToListAsync(ct).ConfigureAwait(false);

        var groupsBySourceClass = _doc.Groups
            .GroupBy(g => g.ClassId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // Yetim guruhlar: sinfi umuman import qilinmagan (yoki mavjud emas).
        foreach (var (ascClassId, orphans) in groupsBySourceClass)
        {
            if (_classByAscId.ContainsKey(ascClassId)) continue;

            groupStat.Skipped += orphans.Count;
            AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-CLASS",
                $"Guruhning sinfi topilmadi (classid = {ascClassId}) — {orphans.Count} ta guruh " +
                "o'tkazib yuborildi.");
        }

        foreach (var (ascClassId, schoolClass) in _classByAscId)
        {
            groupsBySourceClass.TryGetValue(ascClassId, out var sourceGroups);
            sourceGroups ??= new List<AscGroup>();

            ImportGroupsOfClass(
                schoolClass,
                sourceGroups,
                existingDivisions,
                existingGroups,
                divisionStat,
                groupStat);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        await BuildGroupIndexAsync(classIds, ct).ConfigureAwait(false);
    }

    private void ImportGroupsOfClass(
        SchoolClass schoolClass,
        List<AscGroup> sourceGroups,
        List<ClassDivision> existingDivisions,
        List<StudentGroup> existingGroups,
        Counter divisionStat,
        Counter groupStat)
    {
        var divisions = new Dictionary<int, ClassDivision>();
        var nameScopes = new Dictionary<int, UniqueNames>();

        // Yangi yaratilgan bo'linishning Id si hali 0 — shuning uchun ham havola
        // tengligi, ham Id bo'yicha solishtiriladi.
        static bool InDivision(StudentGroup group, ClassDivision division) =>
            ReferenceEquals(group.ClassDivision, division)
            || (division.Id != 0 && group.ClassDivisionId == division.Id);

        ClassDivision Division(int tag)
        {
            if (divisions.TryGetValue(tag, out var cached)) return cached;

            var division = existingDivisions.FirstOrDefault(d =>
                d.SchoolClassId == schoolClass.Id && d.DivisionTag == tag);

            if (division is null)
            {
                division = new ClassDivision
                {
                    SchoolClass = schoolClass,
                    DivisionTag = tag,
                    Name = DivisionName(tag)
                };
                _db.ClassDivisions.Add(division);
                existingDivisions.Add(division);
                divisionStat.Created++;
            }
            else
            {
                divisionStat.Updated++;
            }

            divisionStat.Found++;
            divisions[tag] = division;

            nameScopes[tag] = new UniqueNames(64, existingGroups
                .Where(g => !g.IsDeleted && InDivision(g, division))
                .Select(g => g.Name));

            return division;
        }

        bool InClass(StudentGroup group) =>
            ReferenceEquals(group.SchoolClass, schoolClass)
            || (schoolClass.Id != 0 && group.SchoolClassId == schoolClass.Id);

        // Sinfning HOZIRGI "butun sinf" guruhi (bazadagi holat).
        // <c>UX_StudentGroups_SchoolClassId_EntireClass</c> bo'yicha u bittadan ortiq
        // bo'la olmaydi. Qayta importda AYNAN SHU guruh yana "butun sinf" bo'lib qolishi
        // shart — aks holda ikkinchi importda sinf butun sinf guruhisiz qolardi.
        var entireOwner = existingGroups.FirstOrDefault(g => InClass(g) && g.IsEntireClass && !g.IsDeleted);

        foreach (var source in sourceGroups)
        {
            var tag = Math.Max(0, source.DivisionTag);
            var division = Division(tag);
            var scope = nameScopes[tag];

            var group = existingGroups.FirstOrDefault(g =>
                             !string.IsNullOrWhiteSpace(g.ExternalId)
                             && string.Equals(g.ExternalId, source.Id, StringComparison.Ordinal))
                        ?? existingGroups.FirstOrDefault(g =>
                            InDivision(g, division)
                            && !g.IsDeleted
                            && string.Equals(g.Name, UniqueNames.Normalize(source.Name),
                                StringComparison.OrdinalIgnoreCase));

            var studentCount = source.StudentCount is >= 0 ? source.StudentCount : null;

            // "Butun sinf" bayrog'ini kim oladi. Egasi allaqachon boshqa guruh bo'lsa,
            // uni ATAYLAB tortib olmaymiz: aks holda bitta SaveChanges ichida
            // "eskisini o'chir + yangisini yoq" ketma-ketligi unikal indeksni buzardi.
            var isEntire = false;
            if (source.EntireClass)
            {
                if (entireOwner is null || (group is not null && ReferenceEquals(entireOwner, group)))
                {
                    isEntire = true;
                }
                else
                {
                    AddMessage(ImportSeverity.Warning, "ASC-MULTI-ENTIRECLASS",
                        $"'{schoolClass.Name}' sinfida 'butun sinf' guruhi allaqachon bor " +
                        $"('{entireOwner.Name}') — '{source.Name}' oddiy guruh sifatida saqlandi.",
                        source.Id);
                }
            }

            if (group is null)
            {
                group = new StudentGroup
                {
                    SchoolClass = schoolClass,
                    ClassDivision = division,
                    Name = scope.Take(source.Name, source.Id),
                    IsEntireClass = isEntire,
                    StudentCount = studentCount,
                    ExternalId = Cut(source.Id, 64)
                };
                _db.StudentGroups.Add(group);
                existingGroups.Add(group);
                groupStat.Created++;
            }
            else
            {
                scope.Release(group.Name);
                group.SchoolClass = schoolClass;
                group.ClassDivision = division;
                group.Name = scope.Take(source.Name, source.Id);
                group.IsEntireClass = isEntire;
                group.StudentCount = studentCount;
                group.ExternalId = Cut(source.Id, 64);
                if (group.IsDeleted) group.IsDeleted = false;
                groupStat.Updated++;
            }

            if (isEntire) entireOwner = group;
            else if (ReferenceEquals(entireOwner, group)) entireOwner = null;
        }

        if (entireOwner is not null) return;

        // "Butun sinf" guruhi majburiy: dars faqat sinfga (groupids'siz) berilganda
        // aynan shu guruh ishlatiladi.
        var fallbackDivision = Division(ClassStructureFactory.TagEntireClass);
        var fallbackScope = nameScopes[ClassStructureFactory.TagEntireClass];

        var entire = new StudentGroup
        {
            SchoolClass = schoolClass,
            ClassDivision = fallbackDivision,
            Name = fallbackScope.Take(ClassStructureFactory.EntireClassGroupName, "Butun sinf"),
            IsEntireClass = true
        };

        _db.StudentGroups.Add(entire);
        existingGroups.Add(entire);
        groupStat.Created++;

        AddMessage(ImportSeverity.Info, "ASC-ENTIRECLASS-ADDED",
            $"'{schoolClass.Name}' sinfida 'butun sinf' guruhi yo'q edi — yaratildi.");
    }

    /// <summary>
    /// Bo'linish nomi. 0/1/2 teglari aSc'ning standart uchligiga to'g'ri keladi
    /// (butun sinf, 1/2 guruh, o'g'il/qiz) — dasturning qolgan qismidagi nomlar bilan
    /// bir xil bo'lishi uchun aynan shular ishlatiladi.
    /// </summary>
    private static string DivisionName(int tag) => tag switch
    {
        ClassStructureFactory.TagEntireClass => "Butun sinf",
        ClassStructureFactory.TagHalves => "Guruhlar",
        ClassStructureFactory.TagGender => "O'g'il/qiz",
        _ => $"{tag}-bo'linish"
    };

    /// <summary>
    /// Bandlikni yoyish uchun kerak bo'ladigan indeks: sinf → barcha guruhlar,
    /// sinf → "butun sinf" guruhi.
    /// </summary>
    private async Task BuildGroupIndexAsync(List<int> classIds, CancellationToken ct)
    {
        _groupsByClassId.Clear();
        _groupById.Clear();
        _entireClassGroupByClassId.Clear();
        _groupByAscId.Clear();

        var ids = _classByAscId.Values.Select(c => c.Id).Where(id => id != 0).Distinct().ToList();
        foreach (var id in classIds)
        {
            if (!ids.Contains(id)) ids.Add(id);
        }

        if (ids.Count == 0) return;

        var groups = await _db.StudentGroups
            .Where(g => ids.Contains(g.SchoolClassId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var group in groups)
        {
            if (!_groupsByClassId.TryGetValue(group.SchoolClassId, out var list))
            {
                list = new List<StudentGroup>();
                _groupsByClassId[group.SchoolClassId] = list;
            }

            list.Add(group);
            _groupById[group.Id] = group;

            if (group.IsEntireClass) _entireClassGroupByClassId[group.SchoolClassId] = group;
            if (!string.IsNullOrWhiteSpace(group.ExternalId)) _groupByAscId[group.ExternalId] = group;
        }
    }

    // -------------------------------------------------------------------------
    // Dars ta'riflari
    // -------------------------------------------------------------------------

    private async Task ImportLessonsAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Lesson);

        var existing = await _db.Lessons
            .Where(l => l.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var byExternal = BuildExternalIndex(existing, l => l.ExternalId);
        var daysDefs = _doc.DaysDefs.ToDictionary(d => d.Id, d => d, StringComparer.Ordinal);
        var weeksDefs = _doc.WeeksDefs.ToDictionary(d => d.Id, d => d, StringComparer.Ordinal);

        var touched = new List<int>();

        foreach (var source in _doc.Lessons)
        {
            stat.Found++;

            if (source.SubjectId is null || !_subjectByAscId.TryGetValue(source.SubjectId, out var subject))
            {
                stat.Skipped++;
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-SUBJECT",
                    $"Dars fani topilmadi (subjectid = {source.SubjectId ?? "yo'q"}) — dars o'tkazib yuborildi.",
                    source.Id);
                continue;
            }

            var periodsPerWeek = ResolvePeriodsPerWeek(source);
            if (periodsPerWeek <= 0)
            {
                stat.Skipped++;
                AddMessage(ImportSeverity.Warning, "ASC-LESSON-NO-PERIODS",
                    $"Darsning haftalik soati 0 ({source.PeriodsPerWeek}) — o'tkazib yuborildi.",
                    source.Id);
                continue;
            }

            var periodsPerCard = Math.Clamp(source.PeriodsPerCard, 1, 8);
            if (periodsPerCard > periodsPerWeek)
            {
                AddMessage(ImportSeverity.Warning, "ASC-INVALID-VALUE",
                    $"Kartochka uzunligi ({periodsPerCard}) haftalik soatdan ({periodsPerWeek}) katta — " +
                    $"{periodsPerWeek} ga tushirildi.", source.Id);
                periodsPerCard = periodsPerWeek;
            }

            var daysMask = source.DaysDefId is not null && daysDefs.TryGetValue(source.DaysDefId, out var dd)
                ? dd.Mask
                : 0;

            var weeksMask = source.WeeksDefId is not null && weeksDefs.TryGetValue(source.WeeksDefId, out var wd)
                ? wd.Mask
                : 0;

            byExternal.TryGetValue(source.Id, out var lesson);

            if (lesson is null)
            {
                lesson = new Lesson
                {
                    AcademicYearId = _year.Id,
                    SubjectId = subject.Id,
                    ExternalId = Cut(source.Id, 64)
                };
                _db.Lessons.Add(lesson);
                stat.Created++;
            }
            else
            {
                stat.Updated++;
            }

            lesson.SubjectId = subject.Id;
            lesson.PeriodsPerWeek = periodsPerWeek;
            lesson.PeriodsPerCard = periodsPerCard;
            lesson.AllowedDaysMask = daysMask;
            lesson.AllowedWeeksMask = weeksMask;
            lesson.ExternalId = Cut(source.Id, 64);

            _lessonByAscId[source.Id] = lesson;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        touched.AddRange(_lessonByAscId.Values.Select(l => l.Id));
        await ResetLessonJoinsAsync(touched, ct).ConfigureAwait(false);

        foreach (var source in _doc.Lessons)
        {
            if (!_lessonByAscId.TryGetValue(source.Id, out var lesson)) continue;
            AddLessonJoins(lesson, source);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>periodsperweek</c> kasr bo'lishi mumkin (ko'p haftalik siklda "2 haftada 3 soat").
    /// Bizning modelda u butun son — eng yaqin butunga yaxlitlanadi.
    /// </summary>
    private int ResolvePeriodsPerWeek(AscLesson source)
    {
        var value = source.PeriodsPerWeek;
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);

        if (value != rounded)
        {
            AddMessage(ImportSeverity.Warning, "ASC-FRACTIONAL-PERIODS",
                $"Haftalik soat kasr ({value.ToString(CultureInfo.InvariantCulture)}) — " +
                $"{rounded} ga yaxlitlandi.", source.Id);
        }

        return Math.Clamp(rounded, 0, 100);
    }

    /// <summary>
    /// Darsning bog'lanishlarini tozalaydi — idempotentlik uchun: ikkinchi importda
    /// eski o'qituvchi/sinf/guruh havolalari qolib ketmasin.
    /// </summary>
    private async Task ResetLessonJoinsAsync(List<int> lessonIds, CancellationToken ct)
    {
        if (lessonIds.Count == 0) return;

        await _db.LessonTeachers.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await _db.LessonClasses.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await _db.LessonGroups.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await _db.LessonClassrooms.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Darsning o'qituvchi / sinf / guruh / <b>ruxsat etilgan</b> xona bog'lanishlari.
    /// </summary>
    /// <remarks>
    /// <b>Diqqat:</b> <c>lessons.classroomids</c> — RUXSAT ETILGAN xonalar to'plami
    /// (<see cref="LessonClassroom"/>), <c>cards.classroomids</c> esa TAYINLANGAN xona
    /// (<see cref="CardClassroom"/>). Nomi bir xil, ma'nosi butunlay boshqa.
    /// </remarks>
    private void AddLessonJoins(Lesson lesson, AscLesson source)
    {
        var lessonTeachers = new List<int>();
        _teachersByLessonId[lesson.Id] = lessonTeachers;

        foreach (var teacherId in source.TeacherIds)
        {
            if (_teacherByAscId.TryGetValue(teacherId, out var teacher))
            {
                if (lessonTeachers.Contains(teacher.Id)) continue;
                lessonTeachers.Add(teacher.Id);
                _db.LessonTeachers.Add(new LessonTeacher { LessonId = lesson.Id, TeacherId = teacher.Id });
            }
            else
            {
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-TEACHER",
                    $"Darsning o'qituvchisi topilmadi (teacherid = {teacherId}).", source.Id);
            }
        }

        if (source.TeacherIds.Count == 0)
        {
            AddMessage(ImportSeverity.Info, "ASC-LESSON-NO-TEACHER",
                "Dars o'qituvchisiz (aSc 'Without teacher') — bandlik faqat guruh bo'yicha hisoblanadi.",
                source.Id);
        }

        var classes = new List<SchoolClass>();
        foreach (var classId in source.ClassIds)
        {
            if (_classByAscId.TryGetValue(classId, out var schoolClass))
            {
                classes.Add(schoolClass);
                _db.LessonClasses.Add(new LessonClass { LessonId = lesson.Id, SchoolClassId = schoolClass.Id });
            }
            else
            {
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-CLASS",
                    $"Darsning sinfi topilmadi (classid = {classId}).", source.Id);
            }
        }

        var groupIds = new HashSet<int>();

        foreach (var groupId in source.GroupIds)
        {
            if (_groupByAscId.TryGetValue(groupId, out var group))
            {
                groupIds.Add(group.Id);
            }
            else
            {
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-GROUP",
                    $"Darsning guruhi topilmadi (groupid = {groupId}).", source.Id);
            }
        }

        // groupids bo'sh — dars BUTUN sinf(lar)ga o'tiladi.
        if (groupIds.Count == 0)
        {
            foreach (var schoolClass in classes)
            {
                if (_entireClassGroupByClassId.TryGetValue(schoolClass.Id, out var entire))
                {
                    groupIds.Add(entire.Id);
                }
            }
        }

        _groupsByLessonId[lesson.Id] = groupIds.ToList();

        foreach (var groupId in groupIds)
        {
            _db.LessonGroups.Add(new LessonGroup { LessonId = lesson.Id, StudentGroupId = groupId });
        }

        if (groupIds.Count == 0)
        {
            AddMessage(ImportSeverity.Warning, "ASC-NO-GROUPS",
                "Dars hech qanday guruhga bog'lanmadi — bandlik hisoblanmaydi.", source.Id);
        }

        var priority = 0;
        foreach (var roomId in source.ClassroomIds)
        {
            if (_classroomByAscId.TryGetValue(roomId, out var classroom))
            {
                _db.LessonClassrooms.Add(new LessonClassroom
                {
                    LessonId = lesson.Id,
                    ClassroomId = classroom.Id,
                    Priority = priority++
                });
            }
            else
            {
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-CLASSROOM",
                    $"Darsning ruxsat etilgan xonasi topilmadi (classroomid = {roomId}).", source.Id);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Kartochkalar
    // -------------------------------------------------------------------------

    /// <summary>
    /// aSc <c>cards</c> → <see cref="Card"/>. Har kartochka o'z <c>terms</c> maskasiga
    /// qarab TEGISHLI CHORAK jadvaliga yoziladi; bir nechta chorakda amal qilsa —
    /// har biriga nusxalanadi.
    /// </summary>
    private async Task ImportCardsAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Card);
        stat.Found += _doc.Cards.Count;

        if (_schedules.Count == 0)
        {
            stat.Skipped += _doc.Cards.Count;
            AddMessage(ImportSeverity.Warning, "ASC-NO-SCHEDULE",
                "Jadval varianti yaratilmadi — kartochkalar import qilinmadi.");
            return;
        }

        var scheduleIds = _schedules.Select(s => s.Id).ToList();
        var lessonIds = _lessonByAscId.Values.Select(l => l.Id).ToList();

        // Idempotentlik: shu importda uchragan darslarning maqsad jadvallardagi ESKI
        // kartochkalari o'chiriladi va qaytadan yoziladi — aks holda kartochka
        // ko'chirilganda eskisi qolib ketardi.
        await ClearCardsAsync(scheduleIds, lessonIds, ct).ConfigureAwait(false);

        var occupied = await LoadOccupancyAsync(scheduleIds, ct).ConfigureAwait(false);

        var maxPeriodNo = _periodByNo.Count == 0 ? 0 : _periodByNo.Keys.Max();
        var cardsByLesson = _doc.Cards
            .Where(c => c.LessonId is not null)
            .GroupBy(c => c.LessonId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var orphan in _doc.Cards.Where(c => c.LessonId is null || !_lessonByAscId.ContainsKey(c.LessonId)))
        {
            stat.Skipped++;
            AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-LESSON",
                $"Kartochkaning darsi topilmadi (lessonid = {orphan.LessonId ?? "yo'q"}) — o'tkazib yuborildi.");
        }

        var created = new List<(Card Card, IReadOnlyList<string> Rooms)>();

        // UX_Cards_Schedule_Lesson_Day_Period_Weeks: bir dars bitta slotga ikki marta
        // qo'yilmaydi. aSc eksportida takroriy kartochka uchrashi mumkin.
        var placed = new HashSet<(int ScheduleId, int LessonId, int DayNo, int PeriodId, int WeeksMask)>();

        foreach (var source in _doc.Lessons)
        {
            if (!_lessonByAscId.TryGetValue(source.Id, out var lesson)) continue;
            if (!cardsByLesson.TryGetValue(source.Id, out var sourceCards)) continue;

            var placements = BuildPlacements(source, sourceCards, stat);

            foreach (var bySchedule in placements.GroupBy(p => p.ScheduleIndex))
            {
                var remaining = lesson.PeriodsPerWeek;

                foreach (var placement in bySchedule.OrderBy(p => p.DayNo).ThenBy(p => p.PeriodNo))
                {
                    var length = Math.Clamp(
                        Math.Min(lesson.PeriodsPerCard, remaining > 0 ? remaining : 1), 1, 8);

                    if (maxPeriodNo > 0 && placement.PeriodNo + length - 1 > maxPeriodNo)
                    {
                        var allowed = Math.Max(1, maxPeriodNo - placement.PeriodNo + 1);
                        if (allowed < length)
                        {
                            AddMessage(ImportSeverity.Warning, "ASC-CARD-OVERFLOW",
                                $"Kartochka oxirgi dars soatidan oshib ketardi — uzunligi {length} dan " +
                                $"{allowed} ga tushirildi.", source.Id);
                            length = allowed;
                        }
                    }

                    remaining -= length;

                    var schedule = _schedules[placement.ScheduleIndex];
                    var period = _periodByNo[placement.PeriodNo];

                    if (!placed.Add((schedule.Id, lesson.Id, placement.DayNo, period.Id, placement.WeeksMask)))
                    {
                        stat.Skipped++;
                        remaining += length;
                        AddMessage(ImportSeverity.Warning, "ASC-CARD-DUPLICATE",
                            $"Bir xil kartochka ikki marta uchradi ({schedule.Name}, " +
                            $"{placement.DayNo + 1}-kun, {period.PeriodNo}-soat) — dublikat tashlandi.",
                            source.Id);
                        continue;
                    }

                    var conflict = FindConflict(
                        occupied, schedule.Id, lesson, placement, period.PeriodNo, length);

                    if (conflict is not null)
                    {
                        stat.Skipped++;
                        remaining += length;
                        placed.Remove((schedule.Id, lesson.Id, placement.DayNo, period.Id, placement.WeeksMask));
                        AddMessage(ImportSeverity.Warning, "ASC-CARD-CONFLICT",
                            $"Kartochka bandlik bilan to'qnashdi ({conflict}) — o'tkazib yuborildi " +
                            $"({schedule.Name}, {placement.DayNo + 1}-kun, {period.PeriodNo}-soat).",
                            source.Id);
                        continue;
                    }

                    Occupy(occupied, schedule.Id, lesson, placement, period.PeriodNo, length);

                    var card = new Card
                    {
                        ScheduleId = schedule.Id,
                        LessonId = lesson.Id,
                        PeriodId = period.Id,
                        DayNo = placement.DayNo,
                        Length = length,
                        WeeksMask = placement.WeeksMask
                    };

                    _db.Cards.Add(card);
                    created.Add((card, placement.Rooms));
                    stat.Created++;
                }

                if (remaining < 0)
                {
                    AddMessage(ImportSeverity.Info, "ASC-CARD-OVERFLOW",
                        $"Darsning kartochkalari haftalik soatdan {-remaining} soat ko'p — " +
                        "ortiqchasi yakka dars sifatida qo'yildi.", source.Id);
                }
            }
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // cards.classroomids = TAYINLANGAN xona (lessons.classroomids dan farqli).
        foreach (var (card, rooms) in created)
        {
            foreach (var roomId in rooms)
            {
                if (_classroomByAscId.TryGetValue(roomId, out var classroom))
                {
                    _db.CardClassrooms.Add(new CardClassroom { CardId = card.Id, ClassroomId = classroom.Id });
                }
                else
                {
                    AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-CLASSROOM",
                        $"Kartochkaga tayinlangan xona topilmadi (classroomid = {roomId}).");
                }
            }
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Bitta kartochkaning bitta jadvaldagi joylashuvi.</summary>
    private readonly record struct Placement(
        int ScheduleIndex,
        int DayNo,
        int PeriodNo,
        int WeeksMask,
        IReadOnlyList<string> Rooms);

    /// <summary>
    /// aSc kartochkalarini <c>days</c> va <c>terms</c> maskalari bo'yicha yoyadi.
    /// </summary>
    private List<Placement> BuildPlacements(AscLesson lesson, List<AscCard> sourceCards, Counter stat)
    {
        var result = new List<Placement>();
        var allWeeks = BitMask.All(_weeksInCycle);

        foreach (var card in sourceCards)
        {
            var days = ResolveDays(card);
            if (days.Count == 0)
            {
                stat.Skipped++;
                AddMessage(ImportSeverity.Warning, "ASC-CARD-NO-DAY",
                    "Kartochkaning kuni aniqlanmadi — o'tkazib yuborildi.", lesson.Id);
                continue;
            }

            if (!_periodByNo.ContainsKey(card.Period))
            {
                stat.Skipped++;
                AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-PERIOD",
                    $"Kartochkaning dars soati topilmadi (period = {card.Period}) — o'tkazib yuborildi.",
                    lesson.Id);
                continue;
            }

            var weeksMask = AscBitmask.ToMask(card.Weeks);
            if (weeksMask == 0) weeksMask = allWeeks;
            weeksMask &= allWeeks;
            if (weeksMask == 0) weeksMask = 1;

            var termIndexes = AscBitmask.Selected(AscBitmask.ToMask(card.Terms), _schedules.Count);

            foreach (var termIndex in termIndexes)
            {
                if (termIndex >= _schedules.Count)
                {
                    AddMessage(ImportSeverity.Warning, "ASC-UNKNOWN-TERM",
                        $"Kartochka {termIndex + 1}-chorakka tegishli, lekin bunday chorak yo'q — " +
                        "shu nusxa o'tkazib yuborildi.", lesson.Id);
                    continue;
                }

                foreach (var dayNo in days)
                {
                    result.Add(new Placement(termIndex, dayNo, card.Period, weeksMask, card.ClassroomIds));
                }
            }

            if (days.Count > 1)
            {
                AddMessage(ImportSeverity.Info, "ASC-CARD-MULTIDAY",
                    $"Kartochka {days.Count} ta kunga tegishli — har kun uchun alohida kartochka yaratildi.",
                    lesson.Id);
            }
        }

        return result;
    }

    /// <summary>2012 <c>days</c> bit-satri yoki 2008 <c>day</c> raqamidan kun(lar)ni aniqlaydi.</summary>
    private List<int> ResolveDays(AscCard card)
    {
        var result = new List<int>();

        var mask = AscBitmask.ToMask(card.Days);
        if (mask != 0)
        {
            foreach (var bit in AscBitmask.Bits(mask))
            {
                if (bit is >= 0 and <= 13) result.Add(bit);
            }

            return result;
        }

        if (card.Day is { } day)
        {
            var dayNo = _doc.DayNumberingFromOne ? day - 1 : day;
            if (dayNo is >= 0 and <= 13) result.Add(dayNo);
        }

        return result;
    }

    /// <summary>
    /// Maqsad jadvallardagi eski kartochkalarni o'chiradi (faqat shu importda uchragan
    /// darslarniki). Kaskadga tayanmasdan, bola jadvaldan boshlab.
    /// </summary>
    private async Task ClearCardsAsync(List<int> scheduleIds, List<int> lessonIds, CancellationToken ct)
    {
        if (scheduleIds.Count == 0 || lessonIds.Count == 0) return;

        var cardIds = await _db.Cards
            .Where(c => scheduleIds.Contains(c.ScheduleId) && lessonIds.Contains(c.LessonId))
            .Select(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (cardIds.Count == 0) return;

        await _db.CardOccurrences.Where(o => cardIds.Contains(o.CardId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await _db.CardClassrooms.Where(cc => cardIds.Contains(cc.CardId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await _db.Cards.Where(c => cardIds.Contains(c.Id)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Maqsad jadvallarda ALLAQACHON band bo'lgan slotlarni o'qiydi — yangi kartochkalar
    /// shu to'plamga qarab tekshiriladi.
    /// </summary>
    /// <remarks>
    /// <b>Nega oldindan tekshiriladi.</b> <c>UX_CardOccurrences_Schedule_Resource_Slot</c>
    /// indeksi to'qnashuvni baribir ushlaydi, lekin u BUTUN importni yiqitardi.
    /// Talab esa boshqacha: to'qnashgan kartochka o'tkazib yuborilsin va ogohlantirishga
    /// tushsin.
    /// </remarks>
    private async Task<HashSet<OccupancyKey>> LoadOccupancyAsync(
        List<int> scheduleIds, CancellationToken ct)
    {
        var set = new HashSet<OccupancyKey>();
        if (scheduleIds.Count == 0) return set;

        var rows = await _db.CardOccurrences
            .AsNoTracking()
            .Where(o => scheduleIds.Contains(o.ScheduleId))
            .Select(o => new { o.ScheduleId, o.ResourceKind, o.ResourceId, o.DayNo, o.PeriodNo, o.WeekNo })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            set.Add(new OccupancyKey(
                row.ScheduleId, row.ResourceKind, row.ResourceId, row.DayNo, row.PeriodNo, row.WeekNo));
        }

        return set;
    }

    /// <summary>Kartochka egallaydigan barcha bandlik kalitlarini hisoblaydi.</summary>
    private IEnumerable<OccupancyKey> OccupancyKeys(
        int scheduleId, Lesson lesson, Placement placement, int startPeriodNo, int length)
    {
        var resources = new HashSet<(ResourceKind Kind, int Id)>();

        if (_teachersByLessonId.TryGetValue(lesson.Id, out var teacherIds))
        {
            foreach (var teacherId in teacherIds) resources.Add((ResourceKind.Teacher, teacherId));
        }

        if (_groupsByLessonId.TryGetValue(lesson.Id, out var groupIds))
        {
            foreach (var groupId in groupIds)
            {
                foreach (var expanded in ExpandGroup(groupId))
                {
                    resources.Add((ResourceKind.StudentGroup, expanded));
                }
            }
        }

        foreach (var roomId in placement.Rooms)
        {
            if (_classroomByAscId.TryGetValue(roomId, out var classroom))
            {
                resources.Add((ResourceKind.Classroom, classroom.Id));
            }
        }

        foreach (var week in BitMask.Bits(placement.WeeksMask))
        {
            for (var offset = 0; offset < length; offset++)
            {
                foreach (var (kind, id) in resources)
                {
                    yield return new OccupancyKey(
                        scheduleId, kind, id, placement.DayNo, startPeriodNo + offset, week);
                }
            }
        }
    }

    /// <summary>
    /// "Butun sinf" guruhi sinfning BARCHA guruhlarini band qiladi — bu qoida
    /// <c>CardOccurrenceProjector</c> dagi bilan bir xil bo'lishi SHART.
    /// </summary>
    private IEnumerable<int> ExpandGroup(int groupId)
    {
        if (!_groupById.TryGetValue(groupId, out var group) || !group.IsEntireClass)
        {
            yield return groupId;
            yield break;
        }

        if (!_groupsByClassId.TryGetValue(group.SchoolClassId, out var siblings))
        {
            yield return groupId;
            yield break;
        }

        foreach (var sibling in siblings)
        {
            if (!sibling.IsDeleted) yield return sibling.Id;
        }
    }

    private string? FindConflict(
        HashSet<OccupancyKey> occupied,
        int scheduleId,
        Lesson lesson,
        Placement placement,
        int startPeriodNo,
        int length)
    {
        foreach (var key in OccupancyKeys(scheduleId, lesson, placement, startPeriodNo, length))
        {
            if (!occupied.Contains(key)) continue;

            return key.Kind switch
            {
                ResourceKind.Teacher => "o'qituvchi band",
                ResourceKind.StudentGroup => "guruh band",
                ResourceKind.Classroom => "xona band",
                _ => "resurs band"
            };
        }

        return null;
    }

    private void Occupy(
        HashSet<OccupancyKey> occupied,
        int scheduleId,
        Lesson lesson,
        Placement placement,
        int startPeriodNo,
        int length)
    {
        foreach (var key in OccupancyKeys(scheduleId, lesson, placement, startPeriodNo, length))
        {
            occupied.Add(key);
        }
    }

    /// <summary>
    /// <c>CardOccurrence</c> ning yagona unikal indeksi bilan bir xil kalit —
    /// import bandlikni AYNAN baza kabi hisoblaydi.
    /// </summary>
    private readonly record struct OccupancyKey(
        int ScheduleId, ResourceKind Kind, int ResourceId, int DayNo, int PeriodNo, int WeekNo);
}
