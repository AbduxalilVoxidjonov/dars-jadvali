using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Scheduling.Model;
using DomainPeriod = DarsJadvali.Domain.Entities.Period;

namespace DarsJadvali.Application.Scheduling;

/// <summary>
/// EF entity'laridan yadro masalasini quradi va yadro yechimini kartochkalarga qaytaradi.
/// </summary>
/// <remarks>
/// <b>Asosiy o'girishlar:</b>
/// <list type="bullet">
/// <item><b>Vaqt panjarasi:</b> <c>TimeGrid(daysPerWeek, periods, weeks)</c>.
/// Kun indeksi = <c>hafta × kunlar + WorkDay.DayNo</c>; soat indeksi —
/// <c>Period.PeriodNo</c> ning zich (0-based) tartibi. <c>PeriodNo</c> smenalar bo'ylab
/// uzluksiz bo'lgani uchun ikkala smena BITTA o'lchovda ko'riladi.</item>
/// <item><b>Smena:</b> sinf o'z smenasining soatlaridan tashqarida band qilinmaydi —
/// <c>ClassDef.Availability</c> ga hard taqiq qo'yiladi.</item>
/// <item><b>A/B hafta:</b> <c>Schedule.WeeksInCycle</c> (yo'q bo'lsa
/// <c>AcademicYear.WeeksInCycle</c>) → <c>TimeGrid.Weeks</c>; natijada har karta
/// aniq bitta haftaga tushadi va <c>Card.WeeksMask = 1 &lt;&lt; hafta</c> bo'ladi.</item>
/// <item><b>TimeOff 3 holati:</b> <c>Allowed → Allowed</c>,
/// <c>NotRecommended → Questioned</c> (C-AVL-06 jarimasi),
/// <c>Forbidden → Forbidden</c> (domain'dan chiqadi).
/// <c>Penalty</c> ning roli — <c>ResolveState</c> izohiga qarang.</item>
/// </list>
/// </remarks>
public sealed class SchedulingMapper : ISchedulingMapper
{
    /// <inheritdoc />
    public MappedProblem BuildProblem(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var notes = new List<string>();
        var map = new SchedulingIdMap();

        var periods = BuildPeriodMap(input, map, notes);
        var (dayCount, dayNoOf) = ResolveDays(input);
        var weeks = ResolveWeeks(input);

        EnsureGridFits(dayCount, weeks, periods.Count);

        var grid = new TimeGrid(dayCount, periods.Count, weeks);
        map.DaysPerWeek = dayCount;
        map.Weeks = weeks;

        var activeDays = ResolveActiveDays(input, dayCount, dayNoOf, notes);
        map.ActiveDayNumbers = activeDays;

        var builder = new ProblemBuilder(grid) { UseRoomCapacities = true };

        var teacherDefs = AddTeachers(input, builder, map, grid, notes);
        var classDefs = AddClasses(input, builder, map, grid, periods, activeDays, dayCount, weeks, notes);
        var groupDefs = AddGroups(input, builder, map, classDefs, notes);
        var roomDefs = AddRooms(input, builder, map);
        var subjectDefs = AddSubjects(input, builder, map);

        ApplyTimeOffs(input, map, grid, dayCount, weeks,
                      teacherDefs, classDefs, groupDefs, roomDefs, subjectDefs, notes);

        var lessonDefs = AddLessons(input, builder, map, grid, dayCount, weeks, activeDays,
                                    teacherDefs, groupDefs, subjectDefs, notes);

        ApplyLockedCards(input, map, lessonDefs, dayCount, notes);

        Problem problem;
        try
        {
            problem = builder.Build();
        }
        catch (ArgumentException ex)
        {
            throw new SchedulingMappingException(
                $"Jadval masalasini qurib bo'lmadi: {ex.Message}", ex);
        }

        return new MappedProblem(problem, map, notes);
    }

    /// <inheritdoc />
    public IReadOnlyList<CardWrite> BuildCards(
        SchedulingInput input, MappedProblem mapped, Solution solution)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(mapped);
        ArgumentNullException.ThrowIfNull(solution);

        var map = mapped.Map;
        var result = new List<CardWrite>(solution.PlacedCount);

        foreach (var placement in solution.Placements)
        {
            var coreCard = mapped.Problem.Cards[placement.CardId];
            var weekNo = map.WeekNoOf(placement.DayIndex);
            var dayNo = map.DayNoOf(placement.DayIndex);

            var rooms = placement.RoomId >= 0 && map.Rooms.Count > 0
                ? new[] { map.Rooms.DbIdOf(placement.RoomId) }
                : Array.Empty<int>();

            result.Add(new CardWrite(
                CoreCardId: placement.CardId,
                ScheduleId: input.Schedule.Id,
                LessonId: map.Lessons.DbIdOf(placement.LessonId),
                PeriodId: map.Periods.DbIdOf(placement.Period),
                DayNo: dayNo,
                // Karta aniq bitta haftaga tushadi; bir haftali siklda bu har doim 1.
                WeeksMask: 1 << weekNo,
                IsLocked: coreCard.IsLocked,
                ClassroomIds: rooms,
                // Uzunlik AYNAN yadrodagi kartadan olinadi: "2 + 2 + 1" holatida
                // oxirgi kartaning uzunligi 1 bo'ladi va shu holicha saqlanadi.
                Length: Math.Max(1, coreCard.Length)));
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<PlacedCardView> BuildPlacedViews(
        SchedulingInput input, IReadOnlyList<CardWrite> cards)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(cards);

        var periodNoById = input.Periods.ToDictionary(p => p.Id, p => p.PeriodNo);
        var subjectById = input.Subjects.ToDictionary(s => s.Id, s => s.Name);
        var lessonById = input.Lessons.ToDictionary(l => l.Id, l => l);
        var groupRefs = BuildGroupRefs(input);

        var groupsByLesson = input.LessonGroups
            .GroupBy(lg => lg.LessonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentGroupId).ToArray());

        var views = new List<PlacedCardView>(cards.Count);
        foreach (var card in cards)
        {
            lessonById.TryGetValue(card.LessonId, out var lesson);
            var subjectName = lesson is not null && subjectById.TryGetValue(lesson.SubjectId, out var sn)
                ? sn
                : "Fan";

            var refs = new List<PlacedGroupRef>();
            if (groupsByLesson.TryGetValue(card.LessonId, out var groupIds))
            {
                foreach (var gid in groupIds)
                {
                    if (groupRefs.TryGetValue(gid, out var reference)) refs.Add(reference);
                }
            }

            views.Add(new PlacedCardView(
                CardId: card.CoreCardId,
                SubjectName: subjectName,
                DayNo: card.DayNo,
                StartPeriodNo: periodNoById.TryGetValue(card.PeriodId, out var pn) ? pn : 0,
                // Uzunlik kartochkaning o'zidan (Lesson.PeriodsPerCard dan EMAS).
                Length: Math.Max(1, card.Length),
                WeeksMask: card.WeeksMask,
                Groups: refs));
        }

        return views;
    }

    /// <summary>Guruh Id → tekshiruv uchun kerakli ma'lumot (sinf + bo'linish tegi).</summary>
    public static Dictionary<int, PlacedGroupRef> BuildGroupRefs(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var divisionTagById = input.Divisions.ToDictionary(d => d.Id, d => d.DivisionTag);
        var classNameById = input.Classes.ToDictionary(c => c.Id, c => c.Name);

        var refs = new Dictionary<int, PlacedGroupRef>();
        foreach (var group in input.Groups)
        {
            var tag = ResolveDivisionTag(group, divisionTagById);
            refs[group.Id] = new PlacedGroupRef(
                group.Id,
                group.Name,
                group.SchoolClassId,
                classNameById.TryGetValue(group.SchoolClassId, out var cn) ? cn : $"Sinf #{group.SchoolClassId}",
                tag);
        }

        return refs;
    }

    // =====================================================================
    // Vaqt o'lchamlari
    // =====================================================================

    private static List<DomainPeriod> BuildPeriodMap(
        SchedulingInput input, SchedulingIdMap map, List<string> notes)
    {
        var periods = new List<DomainPeriod>();
        foreach (var period in input.Periods.Where(p => !p.IsBreak)
                                            .OrderBy(p => p.PeriodNo).ThenBy(p => p.Id))
        {
            if (map.IndexOfPeriodNo.ContainsKey(period.PeriodNo))
            {
                notes.Add($"{period.PeriodNo}-dars soati bir necha marta ta'riflangan — birinchisi olindi.");
                continue;
            }

            var index = map.Periods.Add(period.Id);
            map.IndexOfPeriodNo[period.PeriodNo] = index;
            periods.Add(period);
        }

        if (periods.Count == 0)
        {
            throw new SchedulingMappingException(
                "Dars soatlari (qo'ng'iroq jadvali) sozlanmagan — generatsiya uchun kamida bitta dars soati kerak.");
        }

        map.PeriodCount = periods.Count;
        map.PeriodNoOfIndex = periods.Select(p => p.PeriodNo).ToArray();
        return periods;
    }

    private static (int DayCount, Func<WorkDay, int> DayNoOf) ResolveDays(SchedulingInput input)
    {
        var workDays = input.WorkDays;

        // Eski (backfill qilinmagan) yozuvlarda DayNo hammasida 0 bo'lishi mumkin —
        // u holda kun raqami WeekDay dan hosil qilinadi (DayNumbering — yagona nuqta).
        var hasDayNo = workDays.Any(w => w.DayNo > 0);
        int DayNoOf(WorkDay w) => hasDayNo ? w.DayNo : DayNumbering.ToDayNo(w.DayOfWeek);

        var fromWorkDays = workDays.Count == 0 ? 0 : workDays.Max(DayNoOf) + 1;
        var dayCount = Math.Max(input.Year.DaysPerWeek, fromWorkDays);
        if (dayCount <= 0) dayCount = 6;

        return (dayCount, DayNoOf);
    }

    private static int ResolveWeeks(SchedulingInput input)
    {
        if (input.Schedule.WeeksInCycle > 0) return input.Schedule.WeeksInCycle;
        if (input.Year.WeeksInCycle > 0) return input.Year.WeeksInCycle;
        return 1;
    }

    private static void EnsureGridFits(int dayCount, int weeks, int periodCount)
    {
        if (periodCount > 64)
        {
            throw new SchedulingMappingException(
                $"Bir kundagi dars soatlari soni {periodCount} — chegara 64 ta.");
        }

        var slots = (long)dayCount * weeks * periodCount;
        if (slots > SlotMask.Capacity)
        {
            throw new SchedulingMappingException(
                $"Jadval panjarasi juda katta: {dayCount} kun × {weeks} hafta × {periodCount} soat = " +
                $"{slots} pozitsiya, chegara {SlotMask.Capacity}.");
        }
    }

    private static List<int> ResolveActiveDays(
        SchedulingInput input, int dayCount, Func<WorkDay, int> dayNoOf, List<string> notes)
    {
        if (input.WorkDays.Count == 0)
        {
            notes.Add("Ish kunlari sozlanmagan — haftaning barcha kunlari ish kuni deb olindi.");
            return Enumerable.Range(0, dayCount).ToList();
        }

        var active = input.WorkDays
            .Where(w => w.IsActive)
            .Select(dayNoOf)
            .Where(d => d >= 0 && d < dayCount)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (active.Count == 0)
        {
            throw new SchedulingMappingException(
                "Faol ish kuni yo'q — avval «Hafta kunlari» bo'limidan ish kunlarini belgilang.");
        }

        return active;
    }

    // =====================================================================
    // Resurslar
    // =====================================================================

    private static List<TeacherDef> AddTeachers(
        SchedulingInput input, ProblemBuilder builder, SchedulingIdMap map,
        TimeGrid grid, List<string> notes)
    {
        var defs = new List<TeacherDef>();
        foreach (var teacher in input.Teachers.Where(t => !t.IsDeleted).OrderBy(t => t.Id))
        {
            var def = builder.AddTeacher(teacher.FullName);
            EnsureIndex(def.Id, map.Teachers.Add(teacher.Id), "o'qituvchi");

            def.MaxPeriodsPerDay = teacher.MaxLessonsPerDay is > 0 ? teacher.MaxLessonsPerDay.Value : -1;
            def.MaxGapsPerDay = teacher.MaxGapsPerDay is >= 0 ? teacher.MaxGapsPerDay.Value : -1;

            if (!teacher.IsActive)
            {
                for (var d = 0; d < grid.TotalDays; d++) def.Availability.SetDay(grid, d, AvailabilityState.Forbidden);
                notes.Add($"«{teacher.FullName}» faol emas — unga dars qo'yilmaydi.");
            }

            defs.Add(def);
        }

        return defs;
    }

    private static List<ClassDef> AddClasses(
        SchedulingInput input, ProblemBuilder builder, SchedulingIdMap map, TimeGrid grid,
        List<DomainPeriod> periods, List<int> activeDays, int dayCount, int weeks, List<string> notes)
    {
        var (maxPerDay, minPerDay) = ResolveDailyLoad(input);
        var inactiveDays = Enumerable.Range(0, dayCount).Where(d => !activeDays.Contains(d)).ToList();

        var defs = new List<ClassDef>();
        foreach (var schoolClass in input.Classes.Where(c => !c.IsDeleted).OrderBy(c => c.Id))
        {
            var def = builder.AddClass(schoolClass.Name, schoolClass.StudentCount);
            EnsureIndex(def.Id, map.Classes.Add(schoolClass.Id), "sinf");

            def.MaxLessonsPerDay = maxPerDay;
            def.MinLessonsPerDay = minPerDay;

            // Nofaol kunlar — hard taqiq (eski DAY_INACTIVE qoidasi).
            foreach (var dayNo in inactiveDays)
            {
                for (var w = 0; w < weeks; w++) def.Availability.SetDay(grid, w * dayCount + dayNo, AvailabilityState.Forbidden);
            }

            // Smena: sinf o'z smenasining soatlarida o'qiydi (PeriodNo uzluksiz bo'lgani uchun
            // boshqa smenaning soatlari shunchaki taqiqlanadi).
            if (schoolClass.ShiftId is int shiftId && periods.Any(p => p.ShiftId.HasValue))
            {
                for (var pi = 0; pi < periods.Count; pi++)
                {
                    if (periods[pi].ShiftId is null || periods[pi].ShiftId == shiftId) continue;
                    for (var d = 0; d < grid.TotalDays; d++)
                    {
                        def.Availability.Set(grid, d, pi, AvailabilityState.Forbidden);
                    }
                }
            }

            defs.Add(def);
        }

        if (defs.Count == 0) notes.Add("Sinflar ro'yxati bo'sh.");
        return defs;
    }

    private static (int Max, int Min) ResolveDailyLoad(SchedulingInput input)
    {
        var active = input.WorkDays.Where(w => w.IsActive).ToList();
        if (active.Count == 0) return (-1, -1);

        var max = active.Where(w => w.MaxLessonsPerDay > 0)
                        .Select(w => w.MaxLessonsPerDay)
                        .DefaultIfEmpty(-1)
                        .Min();

        var min = active.Select(w => w.MinLessonsPerDay).DefaultIfEmpty(0).Min();

        return (max > 0 ? max : -1, min > 0 ? min : -1);
    }

    private static List<GroupDef> AddGroups(
        SchedulingInput input, ProblemBuilder builder, SchedulingIdMap map,
        List<ClassDef> classDefs, List<string> notes)
    {
        var divisionTagById = input.Divisions.ToDictionary(d => d.Id, d => d.DivisionTag);
        var defs = new List<GroupDef>();

        foreach (var group in input.Groups.Where(g => !g.IsDeleted).OrderBy(g => g.Id))
        {
            if (!map.Classes.TryIndexOf(group.SchoolClassId, out var classIndex))
            {
                notes.Add($"«{group.Name}» guruhi mavjud bo'lmagan sinfga tegishli — e'tiborsiz qoldirildi.");
                continue;
            }

            var tag = ResolveDivisionTag(group, divisionTagById);
            var def = builder.AddGroup(classDefs[classIndex], group.Name, tag, group.StudentCount ?? 0);
            EnsureIndex(def.Id, map.Groups.Add(group.Id), "guruh");
            defs.Add(def);
        }

        return defs;
    }

    /// <summary>
    /// Guruhning bo'linish tegi. "Butun sinf" har doim <c>0</c>; qolganlari kamida <c>1</c>
    /// (aks holda ular butun sinf bilan bir xil tegda bo'lib, parallel o'ta olardi).
    /// </summary>
    private static int ResolveDivisionTag(StudentGroup group, Dictionary<int, int> divisionTagById)
    {
        if (group.IsEntireClass) return 0;
        var tag = divisionTagById.TryGetValue(group.ClassDivisionId, out var t) ? t : 1;
        return tag <= 0 ? 1 : tag;
    }

    private static List<RoomDef> AddRooms(SchedulingInput input, ProblemBuilder builder, SchedulingIdMap map)
    {
        var defs = new List<RoomDef>();
        foreach (var room in input.Classrooms.Where(c => !c.IsDeleted).OrderBy(c => c.Id))
        {
            var def = builder.AddRoom(room.Name, room.Capacity ?? 0);
            EnsureIndex(def.Id, map.Rooms.Add(room.Id), "xona");
            if (room.IsShared) def.ParallelLessons = int.MaxValue;
            defs.Add(def);
        }

        return defs;
    }

    private static List<SubjectDef> AddSubjects(
        SchedulingInput input, ProblemBuilder builder, SchedulingIdMap map)
    {
        var defs = new List<SubjectDef>();
        foreach (var subject in input.Subjects.Where(s => !s.IsDeleted).OrderBy(s => s.Id))
        {
            var def = builder.AddSubject(subject.Name);
            EnsureIndex(def.Id, map.Subjects.Add(subject.Id), "fan");

            def.Distribution = subject.Distribution switch
            {
                SubjectDistribution.Low => DistributionLevel.Low,
                SubjectDistribution.Medium => DistributionLevel.Medium,
                SubjectDistribution.Ideal => DistributionLevel.Ideal,
                SubjectDistribution.IdealNoConsecutive => DistributionLevel.Ideal,
                _ => DistributionLevel.None,
            };

            // "Talab yo'q" bo'lsa bir kunda takrorlanishi ham cheklanmaydi.
            def.OncePerDay = subject.Distribution != SubjectDistribution.None;
            defs.Add(def);
        }

        return defs;
    }

    // =====================================================================
    // TimeOff (3 holat)
    // =====================================================================

    private static void ApplyTimeOffs(
        SchedulingInput input, SchedulingIdMap map, TimeGrid grid, int dayCount, int weeks,
        List<TeacherDef> teachers, List<ClassDef> classes, List<GroupDef> groups,
        List<RoomDef> rooms, List<SubjectDef> subjects, List<string> notes)
    {
        var gradeOfClass = input.Classes.ToDictionary(c => c.Id, c => c.GradeId);

        // Yadro "?" jarimasini bitta QAT'IY og'irlik bilan hisoblaydi (C-AVL-06, w=100),
        // shuning uchun qator bo'yicha turli og'irliklar bitta darajaga tushadi —
        // buni foydalanuvchi bilishi kerak.
        var flattened = 0;
        var escalated = 0;

        foreach (var timeOff in input.TimeOffs.OrderBy(t => t.Id))
        {
            if (!map.IndexOfPeriodNo.TryGetValue(timeOff.PeriodNo, out var periodIndex))
            {
                notes.Add($"Vaqt cheklovi {timeOff.PeriodNo}-soatga tegishli, lekin bunday dars soati yo'q.");
                continue;
            }

            if (timeOff.DayNo < 0 || timeOff.DayNo >= dayCount)
            {
                notes.Add($"Vaqt cheklovidagi kun raqami ({timeOff.DayNo}) hafta chegarasidan tashqarida.");
                continue;
            }

            var state = ResolveState(timeOff, ref flattened, ref escalated);

            foreach (var target in ResolveTimeOffTargets(timeOff, map, gradeOfClass, teachers, classes, groups, rooms, subjects))
            {
                foreach (var week in ExpandWeeks(timeOff.WeeksMask, weeks))
                {
                    target.Availability.Set(grid, week * dayCount + timeOff.DayNo, periodIndex, state);
                }
            }
        }

        if (escalated > 0)
        {
            notes.Add($"{escalated} ta «tavsiya etilmaydi» cheklovi eng yuqori jarima " +
                      $"({TimeOff.HardThreshold}) bilan belgilangan — ular TAQIQ sifatida qo'llanildi.");
        }

        if (flattened > 0)
        {
            notes.Add($"{flattened} ta «tavsiya etilmaydi» cheklovining jarima og'irligi " +
                      "yagona darajaga tushirildi: generatsiya yadrosi «?» katakchalarini " +
                      "bitta qat'iy og'irlik bilan hisoblaydi.");
        }
    }

    /// <summary>
    /// <c>TimeOff</c> ning 3 holatini yadro holatiga o'giradi va <c>Penalty</c> ni
    /// hisobga oladi.
    /// </summary>
    /// <remarks>
    /// <b>Nima uchun jarima og'irligi yadroga TO'G'RIDAN-TO'G'RI uzatilmaydi.</b>
    /// <c>DarsJadvali.Scheduling</c> da "?" holati bitta bitmask
    /// (<c>Card.QuestionMarked</c>) va bitta qat'iy og'irlik (C-AVL-06, w = 100) bilan
    /// ifodalanadi — resurs/katakcha bo'yicha ALOHIDA og'irlik uchun joy yo'q.
    /// Shu sababli jarima faqat DARAJA tanlashda ishlatiladi:
    /// <list type="bullet">
    /// <item><c>Penalty &gt;= TimeOff.HardThreshold</c> (cheklovning yuqori chegarasi) —
    /// "shunchalik yomonki, amalda mumkin emas" → <c>Forbidden</c>;</item>
    /// <item>qolgan barcha musbat qiymatlar → <c>Questioned</c> (yagona og'irlik).</item>
    /// </list>
    /// Og'irlikni haqiqatda uzatish uchun yadroning <c>Card.QuestionMarked</c> maskasi
    /// og'irlikli tuzilmaga almashtirilishi kerak — bu <c>DarsJadvali.Scheduling</c>
    /// o'zgarishi, alohida bosqich.
    /// </remarks>
    private static AvailabilityState ResolveState(TimeOff timeOff, ref int flattened, ref int escalated)
    {
        switch (timeOff.Availability)
        {
            case AvailabilityLevel.Forbidden:
                return AvailabilityState.Forbidden;

            case AvailabilityLevel.NotRecommended when timeOff.Penalty >= TimeOff.HardThreshold:
                escalated++;
                return AvailabilityState.Forbidden;

            case AvailabilityLevel.NotRecommended:
                if (timeOff.Penalty > 0) flattened++;
                return AvailabilityState.Questioned;

            default:
                return AvailabilityState.Allowed;
        }
    }

    private static IEnumerable<ResourceDef> ResolveTimeOffTargets(
        TimeOff timeOff, SchedulingIdMap map, Dictionary<int, int?> gradeOfClass,
        List<TeacherDef> teachers, List<ClassDef> classes, List<GroupDef> groups,
        List<RoomDef> rooms, List<SubjectDef> subjects)
    {
        switch (timeOff.OwnerKind)
        {
            case ResourceOwnerKind.Teacher:
                if (map.Teachers.TryIndexOf(timeOff.OwnerId, out var ti)) yield return teachers[ti];
                break;

            case ResourceOwnerKind.StudentGroup:
                if (map.Groups.TryIndexOf(timeOff.OwnerId, out var gi)) yield return groups[gi];
                break;

            case ResourceOwnerKind.SchoolClass:
                if (map.Classes.TryIndexOf(timeOff.OwnerId, out var ci)) yield return classes[ci];
                break;

            case ResourceOwnerKind.Classroom:
                if (map.Rooms.TryIndexOf(timeOff.OwnerId, out var ri)) yield return rooms[ri];
                break;

            case ResourceOwnerKind.Subject:
                if (map.Subjects.TryIndexOf(timeOff.OwnerId, out var si)) yield return subjects[si];
                break;

            case ResourceOwnerKind.Grade:
                for (var i = 0; i < map.Classes.Count; i++)
                {
                    var dbId = map.Classes.DbIdOf(i);
                    if (gradeOfClass.TryGetValue(dbId, out var gradeId) && gradeId == timeOff.OwnerId)
                    {
                        yield return classes[i];
                    }
                }

                break;

            case ResourceOwnerKind.Global:
                foreach (var t in teachers) yield return t;
                foreach (var c in classes) yield return c;
                break;
        }
    }

    // =====================================================================
    // Darslar va qulflangan kartochkalar
    // =====================================================================

    private static Dictionary<(int LessonId, int Week), LessonDef> AddLessons(
        SchedulingInput input, ProblemBuilder builder, SchedulingIdMap map, TimeGrid grid,
        int dayCount, int weeks, List<int> activeDays,
        List<TeacherDef> teachers, List<GroupDef> groups, List<SubjectDef> subjects,
        List<string> notes)
    {
        var lessonDefByWeek = new Dictionary<(int LessonId, int Week), LessonDef>();
        var teachersByLesson = input.LessonTeachers
            .GroupBy(x => x.LessonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TeacherId).Distinct().OrderBy(x => x).ToArray());

        var groupsByLesson = input.LessonGroups
            .GroupBy(x => x.LessonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentGroupId).Distinct().OrderBy(x => x).ToArray());

        var classesByLesson = input.LessonClasses
            .GroupBy(x => x.LessonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SchoolClassId).Distinct().OrderBy(x => x).ToArray());

        var roomsByLesson = input.LessonClassrooms
            .GroupBy(x => x.LessonId)
            .ToDictionary(g => g.Key,
                          g => g.OrderBy(x => x.Priority).ThenBy(x => x.ClassroomId)
                                .Select(x => x.ClassroomId).Distinct().ToArray());

        var entireClassGroupOfClass = input.Groups
            .Where(g => g.IsEntireClass && !g.IsDeleted)
            .GroupBy(g => g.SchoolClassId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First().Id);

        var subjectNames = input.Subjects.ToDictionary(s => s.Id, s => s.Name);
        var classNames = input.Classes.ToDictionary(c => c.Id, c => c.Name);

        foreach (var lesson in input.Lessons.OrderBy(l => l.Id))
        {
            if (!map.Subjects.TryIndexOf(lesson.SubjectId, out var subjectIndex))
            {
                notes.Add($"Dars #{lesson.Id}: fan topilmadi — o'tkazib yuborildi.");
                continue;
            }

            var groupDefs = ResolveLessonGroups(
                lesson, groupsByLesson, classesByLesson, entireClassGroupOfClass, map, groups, notes);
            if (groupDefs.Count == 0) continue;

            var teacherDefs = new List<TeacherDef>();
            if (teachersByLesson.TryGetValue(lesson.Id, out var teacherIds))
            {
                foreach (var id in teacherIds)
                {
                    if (map.Teachers.TryIndexOf(id, out var index)) teacherDefs.Add(teachers[index]);
                }
            }

            var allowedWeeks = ExpandWeeks(lesson.AllowedWeeksMask, weeks);
            var perCard = NormalizePeriodsPerCard(lesson, map.PeriodCount, notes, subjectNames);
            var lessonName = BuildLessonName(lesson, subjectNames, classesByLesson, classNames);

            // C-CYC-03 — ruxsat etilgan kunlar: faol ish kunlari ∩ darsning kun maskasi.
            var dayNumbers = lesson.AllowedDaysMask == 0
                ? activeDays
                : activeDays.Where(d => BitMask.Has(lesson.AllowedDaysMask, d)).ToList();

            if (dayNumbers.Count == 0)
            {
                notes.Add($"«{lessonName}»: ruxsat etilgan kunlar ish kunlari bilan kesishmadi — cheklov olib tashlandi.");
                dayNumbers = activeDays;
            }

            var roomIndexes = roomsByLesson.TryGetValue(lesson.Id, out var roomIds)
                ? roomIds.Where(id => map.Rooms.ContainsDbId(id)).Select(id => map.Rooms.IndexOf(id)).ToArray()
                : Array.Empty<int>();

            if (lesson.RequiredClassroomCount > 0 && roomIndexes.Length == 0)
            {
                notes.Add($"«{lessonName}» xona talab qiladi, lekin ruxsat etilgan xona ko'rsatilmagan.");
            }

            // A/B hafta: HAR bir hafta uchun alohida yadro darsi quriladi. Aks holda
            // "haftasiga N soat" siklda 2N soatga aylanib, hammasi bitta haftaga
            // to'planib qolishi mumkin edi.
            foreach (var week in allowedWeeks)
            {
                LessonDef def;
                try
                {
                    def = builder.AddLesson(
                        subjects[subjectIndex], teacherDefs, groupDefs, lesson.PeriodsPerWeek, perCard);
                }
                catch (ArgumentException ex)
                {
                    throw new SchedulingMappingException(
                        $"Dars #{lesson.Id} ni qo'shib bo'lmadi: {ex.Message}", ex);
                }

                EnsureIndex(def.Id, map.Lessons.Add(lesson.Id), "dars");
                lessonDefByWeek[(lesson.Id, week)] = def;

                def.Name = weeks > 1 ? $"{lessonName} ({week + 1}-hafta)" : lessonName;
                def.AllowedRoomIds = roomIndexes;
                def.AllowedDays = dayNumbers
                    .Select(d => week * dayCount + d)
                    .Where(d => d >= 0 && d < grid.TotalDays)
                    .OrderBy(d => d)
                    .ToArray();
            }
        }

        return lessonDefByWeek;
    }

    private static List<GroupDef> ResolveLessonGroups(
        Lesson lesson,
        Dictionary<int, int[]> groupsByLesson,
        Dictionary<int, int[]> classesByLesson,
        Dictionary<int, int> entireClassGroupOfClass,
        SchedulingIdMap map,
        List<GroupDef> groups,
        List<string> notes)
    {
        var result = new List<GroupDef>();

        if (groupsByLesson.TryGetValue(lesson.Id, out var groupIds))
        {
            foreach (var id in groupIds)
            {
                if (map.Groups.TryIndexOf(id, out var index)) result.Add(groups[index]);
            }
        }

        // Guruh ko'rsatilmagan bo'lsa — dars butun sinfga o'tiladi deb hisoblanadi.
        if (result.Count == 0 && classesByLesson.TryGetValue(lesson.Id, out var classIds))
        {
            foreach (var classId in classIds)
            {
                if (entireClassGroupOfClass.TryGetValue(classId, out var groupId) &&
                    map.Groups.TryIndexOf(groupId, out var index))
                {
                    result.Add(groups[index]);
                }
            }
        }

        if (result.Count == 0)
        {
            notes.Add($"Dars #{lesson.Id} hech qanday guruhga biriktirilmagan — o'tkazib yuborildi.");
        }

        return result;
    }

    /// <summary>
    /// Juft dars uzunligi. <c>Card.Length</c> ustuni qo'shilgach bo'linmaydigan qoldiq
    /// ("haftasiga 5 soat = 2 + 2 + 1") ham saqlanadi: yadro oxirgi kartani qoldiq
    /// uzunligi bilan quradi va u shu holicha bazaga tushadi. Shu sababli bu yerda
    /// uzunlik endi 1 ga TUSHIRILMAYDI — faqat foydalanuvchiga izoh qoldiriladi.
    /// </summary>
    private static int NormalizePeriodsPerCard(
        Lesson lesson, int periodCount, List<string> notes, Dictionary<int, string> subjectNames)
    {
        var perCard = lesson.PeriodsPerCard <= 0 ? 1 : lesson.PeriodsPerCard;
        var name = subjectNames.TryGetValue(lesson.SubjectId, out var sn) ? sn : $"Dars #{lesson.Id}";

        // Kundagi soatlar sonidan uzun karta hech qachon joylasha olmaydi — bu haqiqiy xato.
        if (perCard > periodCount)
        {
            notes.Add($"«{name}»: juft dars uzunligi ({perCard}) kundagi soatlar sonidan katta — 1 ga tushirildi.");
            return 1;
        }

        if (perCard > 1 && lesson.PeriodsPerWeek % perCard != 0)
        {
            var full = lesson.PeriodsPerWeek / perCard;
            var rest = lesson.PeriodsPerWeek % perCard;
            notes.Add($"«{name}»: haftalik soat ({lesson.PeriodsPerWeek}) juft dars uzunligiga ({perCard}) " +
                      $"bo'linmaydi — {full} ta {perCard} soatlik va bitta {rest} soatlik kartochka quriladi.");
        }

        return perCard;
    }

    private static string BuildLessonName(
        Lesson lesson, Dictionary<int, string> subjectNames,
        Dictionary<int, int[]> classesByLesson, Dictionary<int, string> classNames)
    {
        var subject = subjectNames.TryGetValue(lesson.SubjectId, out var sn) ? sn : $"Fan #{lesson.SubjectId}";
        if (!classesByLesson.TryGetValue(lesson.Id, out var classIds) || classIds.Length == 0) return subject;

        var names = classIds.Select(id => classNames.TryGetValue(id, out var cn) ? cn : $"#{id}");
        return $"{string.Join(", ", names)} — {subject}";
    }

    /// <summary>
    /// C-GBL-06 — qulflangan kartochkalar. <c>ProblemBuilder</c> qulflarni
    /// <c>LessonDef.Locked</c> dan oladi, shuning uchun bu darslar qo'shilgandan keyin,
    /// <c>Build()</c> dan oldin bajariladi.
    /// </summary>
    private static void ApplyLockedCards(
        SchedulingInput input, SchedulingIdMap map,
        Dictionary<(int LessonId, int Week), LessonDef> lessonDefs, int dayCount, List<string> notes)
    {
        if (input.LockedCards.Count == 0) return;

        foreach (var card in input.LockedCards.OrderBy(c => c.Id))
        {
            var lockedWeek = FirstWeek(card.WeeksMask);
            if (!lessonDefs.TryGetValue((card.LessonId, lockedWeek), out var def))
            {
                notes.Add($"Qulflangan kartochka #{card.Id} uchun dars topilmadi — qulf e'tiborsiz qoldirildi.");
                continue;
            }

            if (!map.Periods.TryIndexOf(card.PeriodId, out var periodIndex))
            {
                notes.Add($"Qulflangan kartochka #{card.Id} uchun dars soati topilmadi — qulf e'tiborsiz qoldirildi.");
                continue;
            }

            var week = FirstWeek(card.WeeksMask);
            def.Locked.Add(new FixedPlacement(week * dayCount + card.DayNo, periodIndex));
        }
    }

    // =====================================================================
    // Yordamchilar
    // =====================================================================

    /// <summary>Hafta maskasidagi yoqilgan bitlar; <c>0</c> = sikldagi barcha haftalar.</summary>
    private static List<int> ExpandWeeks(int weeksMask, int weeks)
    {
        if (weeksMask <= 0) return Enumerable.Range(0, weeks).ToList();

        var result = BitMask.Bits(weeksMask).Where(b => b < weeks).ToList();
        return result.Count == 0 ? Enumerable.Range(0, weeks).ToList() : result;
    }

    private static int FirstWeek(int weeksMask)
    {
        if (weeksMask <= 0) return 0;
        foreach (var bit in BitMask.Bits(weeksMask)) return bit;
        return 0;
    }

    private static void EnsureIndex(int coreIndex, int mapIndex, string resource)
    {
        if (coreIndex != mapIndex)
        {
            throw new SchedulingMappingException(
                $"Ichki xato: {resource} indekslari mos kelmadi ({coreIndex} ≠ {mapIndex}).");
        }
    }
}
