using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence.Backfill;

/// <summary>
/// Eski (v1) modeldan sxema v2 ga ma'lumot ko'chirish:
/// <c>ClassGroup</c> → <c>SchoolClass</c> (+ standart bo'linishlar va guruhlar),
/// <c>LessonSlot</c> → <c>Period</c>,
/// <c>TeacherAssignment</c> → <c>Lesson</c> (+ 3 ta join),
/// <c>ScheduleEntry</c> → <c>Card</c> → <c>CardOccurrence</c>.
/// </summary>
/// <remarks>
/// <b>Migratsiya ichida emas, alohida klass.</b> Sabab: ko'chirish mantiqi (yetim yozuvlar,
/// qisqartma dublikatlari, guruh yoyilishi) SQL'da yozib bo'lmaydigan darajada murakkab,
/// va u testlanadigan bo'lishi kerak.
/// <para>
/// <b>Idempotent:</b> takror ishga tushirilsa dublikat yaratmaydi — <c>LegacyClassGroupId</c>,
/// <c>LegacyTeacherAssignmentId</c>, <c>LegacyScheduleEntryId</c> ustunlaridagi filtrlangan
/// unikal indekslar shuni kafolatlaydi.
/// </para>
/// <para>
/// <b>Eski jadvallar O'CHIRILMAYDI.</b> 1-bosqich additiv: <c>ScheduleEntry</c>,
/// <c>TeacherAssignment</c>, <c>ClassGroup</c>, <c>LessonSlot</c> joyida qoladi va
/// Application/Desktop/Web ularni ishlatishda davom etadi.
/// </para>
/// </remarks>
public sealed class LegacyToV2Backfill
{
    private const int DefaultLessonMinutes = 45;
    private const int DefaultBreakMinutes = 10;

    private readonly AppDbContext _context;
    private readonly ICardOccurrenceProjector _projector;

    public LegacyToV2Backfill(AppDbContext context, ICardOccurrenceProjector projector)
    {
        _context = context;
        _projector = projector;
    }

    /// <summary>
    /// Jadval yozuvlari bor barcha o'quv yillari uchun ko'chirishni bajaradi.
    /// </summary>
    public async Task<LegacyBackfillResult> RunAsync(CancellationToken ct = default)
    {
        var result = new LegacyBackfillResult();

        var yearIds = await _context.Schedules
            .AsNoTracking()
            .Select(s => s.AcademicYearId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (yearIds.Count == 0)
        {
            var newest = await _context.AcademicYears
                .AsNoTracking()
                .OrderByDescending(y => y.StartYear).ThenByDescending(y => y.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (newest is null)
            {
                result.Messages.Add("O'quv yili topilmadi — ko'chirish o'tkazilmadi.");
                return result;
            }

            yearIds.Add(newest.Id);
        }

        foreach (var yearId in yearIds.OrderBy(x => x))
        {
            await BackfillYearAsync(yearId, result, ct).ConfigureAwait(false);
        }

        return result;
    }

    // =====================================================================

    private async Task BackfillYearAsync(int yearId, LegacyBackfillResult result, CancellationToken ct)
    {
        var year = await _context.AcademicYears.FirstAsync(y => y.Id == yearId, ct).ConfigureAwait(false);

        await EnsureTermsAsync(year, result, ct).ConfigureAwait(false);
        var shifts = await EnsureShiftsAsync(year, result, ct).ConfigureAwait(false);
        var periods = await EnsurePeriodsAsync(year, shifts, result, ct).ConfigureAwait(false);
        await EnsureReferenceYearLinksAsync(year, ct).ConfigureAwait(false);
        var classes = await EnsureSchoolClassesAsync(year, shifts, result, ct).ConfigureAwait(false);
        var lessons = await EnsureLessonsAsync(year, classes, result, ct).ConfigureAwait(false);
        await EnsureCardsAsync(year, classes, lessons, periods, result, ct).ConfigureAwait(false);

        // V2_06 — vaqt cheklovlari (eski 2 holatli oraliqlardan 3 holatli katakchalarga).
        await EnsureTimeOffsAsync(year, periods, result, ct).ConfigureAwait(false);

        // V2_07 — xonalar. Bandlik qayta qurilishidan OLDIN bo'lishi shart: xona
        // qatorlari ham proyeksiyaga tushishi kerak.
        await EnsureClassroomsAsync(year, periods, result, ct).ConfigureAwait(false);

        // Bandlik qatorlari — projector orqali, qo'lda emas.
        var scheduleIds = await _context.Schedules
            .AsNoTracking()
            .Where(s => s.AcademicYearId == yearId)
            .Select(s => s.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var scheduleId in scheduleIds)
        {
            result.CardOccurrences += await _projector
                .RebuildForScheduleAsync(scheduleId, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Chorak yozuvlari (I–IV). Har chorak keyinchalik o'z jadval variantiga ega bo'ladi.</summary>
    private async Task EnsureTermsAsync(AcademicYear year, LegacyBackfillResult result, CancellationToken ct)
    {
        var existing = await _context.Terms
            .Where(t => t.AcademicYearId == year.Id)
            .Select(t => t.Ordinal)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        string[] roman = { "I", "II", "III", "IV", "V", "VI" };

        for (var ordinal = 1; ordinal <= Math.Max(1, year.TermsCount); ordinal++)
        {
            if (existing.Contains(ordinal)) continue;

            var shortName = ordinal <= roman.Length ? roman[ordinal - 1] : ordinal.ToString();
            _context.Terms.Add(new Term
            {
                AcademicYearId = year.Id,
                Ordinal = ordinal,
                Name = $"{shortName} chorak",
                ShortName = shortName
            });
            result.Terms++;
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Ikki smena. Tasdiqlangan qaror: maktabda 2 smena bor.</summary>
    private async Task<List<Shift>> EnsureShiftsAsync(
        AcademicYear year, LegacyBackfillResult result, CancellationToken ct)
    {
        var existing = await _context.Shifts
            .Where(s => s.AcademicYearId == year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        for (var no = 1; no <= 2; no++)
        {
            if (existing.Any(s => s.ShiftNo == no)) continue;

            var shift = new Shift
            {
                AcademicYearId = year.Id,
                ShiftNo = no,
                Name = $"{no}-smena",
                ShortName = no.ToString()
            };
            _context.Shifts.Add(shift);
            existing.Add(shift);
            result.Shifts++;
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing.OrderBy(s => s.ShiftNo).ToList();
    }

    /// <summary>
    /// <c>LessonSlot</c> → <c>Period</c>. Bundan tashqari jadval yozuvlarida ishlatilgan,
    /// lekin <c>LessonSlots</c> da yo'q dars soatlari ham avtomatik yaratiladi.
    /// </summary>
    private async Task<Dictionary<int, Period>> EnsurePeriodsAsync(
        AcademicYear year, List<Shift> shifts, LegacyBackfillResult result, CancellationToken ct)
    {
        var periods = await _context.Periods
            .Where(p => p.AcademicYearId == year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var slots = await _context.LessonSlots
            .AsNoTracking()
            .OrderBy(s => s.LessonNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Jadval yozuvlarida uchraydigan barcha dars soati raqamlari.
        var usedNumbers = await _context.ScheduleEntries
            .AsNoTracking()
            .Where(e => e.Schedule!.AcademicYearId == year.Id)
            .Select(e => e.LessonNumber)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var wanted = slots.Select(s => s.LessonNumber)
            .Concat(usedNumbers)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // 1-smena birinchi yarmini, 2-smena ikkinchi yarmini oladi. PeriodNo esa
        // smenalar bo'ylab UZLUKSIZ qoladi — bandlik indeksi shunga tayanadi.
        var firstShift = shifts.FirstOrDefault(s => s.ShiftNo == 1);
        var half = wanted.Count == 0 ? 0 : (int)Math.Ceiling(wanted.Count / 2.0);

        foreach (var number in wanted)
        {
            if (periods.Any(p => p.PeriodNo == number)) continue;

            var slot = slots.FirstOrDefault(s => s.LessonNumber == number);
            var (start, end) = slot is not null
                ? (ToTimeOnly(slot.StartTime), ToTimeOnly(slot.EndTime))
                : EstimateTimes(number);

            var period = new Period
            {
                AcademicYearId = year.Id,
                // Barcha eski dars soatlari 1-smenaga tegishli deb hisoblanadi;
                // 2-smena sinflari keyin qo'lda taqsimlanadi.
                ShiftId = firstShift?.Id,
                PeriodNo = number,
                StartTime = start,
                EndTime = end,
                Name = $"{number}-dars",
                ShortName = number.ToString()
            };

            _context.Periods.Add(period);
            periods.Add(period);
            result.Periods++;

            if (slot is null)
            {
                result.Messages.Add($"Yetishmayotgan dars soati yaratildi: {number}.");
            }
        }

        _ = half;
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return periods.ToDictionary(p => p.PeriodNo, p => p);
    }

    /// <summary>
    /// Fan va o'qituvchi ma'lumotnomalarini o'quv yiliga bog'laydi va v2 qisqartmalarini to'ldiradi.
    /// </summary>
    private async Task EnsureReferenceYearLinksAsync(AcademicYear year, CancellationToken ct)
    {
        var subjects = await _context.Subjects.ToListAsync(ct).ConfigureAwait(false);
        var takenSubjectShort = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var subject in subjects)
        {
            subject.AcademicYearId ??= year.Id;
            if (string.IsNullOrWhiteSpace(subject.ShortName))
            {
                subject.ShortName = Unique(
                    string.IsNullOrWhiteSpace(subject.Code) ? subject.Name : subject.Code,
                    24, takenSubjectShort);
            }
            else
            {
                takenSubjectShort.Add(subject.ShortName);
            }
        }

        var teachers = await _context.Teachers.ToListAsync(ct).ConfigureAwait(false);
        var takenTeacherShort = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var teacher in teachers)
        {
            teacher.AcademicYearId ??= year.Id;
            if (string.IsNullOrWhiteSpace(teacher.ShortName))
            {
                teacher.ShortName = Unique(ShortenFullName(teacher.FullName), 24, takenTeacherShort);
            }
            else
            {
                takenTeacherShort.Add(teacher.ShortName);
            }
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>ClassGroup</c> → <c>SchoolClass</c> + har sinf uchun standart 3 bo'linish / 5 guruh.
    /// </summary>
    private async Task<Dictionary<int, SchoolClass>> EnsureSchoolClassesAsync(
        AcademicYear year, List<Shift> shifts, LegacyBackfillResult result, CancellationToken ct)
    {
        var legacyGroups = await _context.ClassGroups
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existing = await _context.SchoolClasses
            .Where(c => c.AcademicYearId == year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var takenShort = new HashSet<string>(
            existing.Select(c => c.ShortName), StringComparer.OrdinalIgnoreCase);

        var firstShift = shifts.FirstOrDefault(s => s.ShiftNo == 1);
        var map = existing
            .Where(c => c.LegacyClassGroupId.HasValue)
            .ToDictionary(c => c.LegacyClassGroupId!.Value, c => c);

        foreach (var legacy in legacyGroups)
        {
            if (map.ContainsKey(legacy.Id)) continue;

            var schoolClass = new SchoolClass
            {
                AcademicYearId = year.Id,
                Name = legacy.Name,
                ShortName = Unique(legacy.Name, 24, takenShort),
                // Barcha sinflar dastlab 1-smenaga; 2-smena keyin qo'lda tayinlanadi.
                ShiftId = firstShift?.Id,
                StudentCount = legacy.StudentCount,
                LegacyClassGroupId = legacy.Id
            };

            _context.SchoolClasses.Add(schoolClass);
            map[legacy.Id] = schoolClass;
            result.SchoolClasses++;
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        // Standart bo'linishlar va guruhlar.
        foreach (var schoolClass in map.Values)
        {
            var tags = await _context.ClassDivisions
                .Where(d => d.SchoolClassId == schoolClass.Id)
                .Select(d => d.DivisionTag)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var created = ClassStructureFactory.AddStandardStructure(_context, schoolClass, tags);
            result.StudentGroups += created;
            result.ClassDivisions += created == 0 ? 0 : 3 - tags.Count;
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return map;
    }

    /// <summary><c>TeacherAssignment</c> → <c>Lesson</c> + <c>LessonTeacher/Class/Group</c>.</summary>
    private async Task<Dictionary<(int TeacherId, int SubjectId, int ClassGroupId), Lesson>>
        EnsureLessonsAsync(
            AcademicYear year,
            Dictionary<int, SchoolClass> classes,
            LegacyBackfillResult result,
            CancellationToken ct)
    {
        var map = new Dictionary<(int, int, int), Lesson>();

        var existing = await _context.Lessons
            .Where(l => l.AcademicYearId == year.Id && l.LegacyTeacherAssignmentId != null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var assignments = await _context.TeacherAssignments
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var entireGroups = await LoadEntireClassGroupsAsync(classes, ct).ConfigureAwait(false);

        foreach (var assignment in assignments)
        {
            if (!classes.TryGetValue(assignment.ClassGroupId, out var schoolClass)) continue;

            var key = (assignment.TeacherId, assignment.SubjectId, assignment.ClassGroupId);
            var already = existing.FirstOrDefault(l => l.LegacyTeacherAssignmentId == assignment.Id);

            if (already is not null)
            {
                map[key] = already;
                continue;
            }

            var lesson = CreateLesson(
                year.Id, assignment.SubjectId, Math.Max(1, assignment.WeeklyHoursCount),
                assignment.TeacherId, schoolClass, entireGroups[schoolClass.Id]);

            lesson.LegacyTeacherAssignmentId = assignment.Id;
            map[key] = lesson;
            result.Lessons++;
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return map;
    }

    /// <summary><c>ScheduleEntry</c> → <c>Card</c>. Yetim yozuvlar uchun dars avtomatik yaratiladi.</summary>
    private async Task EnsureCardsAsync(
        AcademicYear year,
        Dictionary<int, SchoolClass> classes,
        Dictionary<(int TeacherId, int SubjectId, int ClassGroupId), Lesson> lessons,
        Dictionary<int, Period> periods,
        LegacyBackfillResult result,
        CancellationToken ct)
    {
        var entries = await _context.ScheduleEntries
            .AsNoTracking()
            .Where(e => e.Schedule!.AcademicYearId == year.Id)
            .OrderBy(e => e.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (entries.Count == 0) return;

        var alreadyMigrated = await _context.Cards
            .AsNoTracking()
            .Where(c => c.LegacyScheduleEntryId != null)
            .Select(c => c.LegacyScheduleEntryId!.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var migrated = alreadyMigrated.ToHashSet();
        var entireGroups = await LoadEntireClassGroupsAsync(classes, ct).ConfigureAwait(false);

        foreach (var entry in entries)
        {
            if (migrated.Contains(entry.Id)) continue;
            if (!classes.TryGetValue(entry.ClassGroupId, out var schoolClass)) continue;
            if (!periods.TryGetValue(entry.LessonNumber, out var period)) continue;

            var key = (entry.TeacherId, entry.SubjectId, entry.ClassGroupId);

            if (!lessons.TryGetValue(key, out var lesson))
            {
                // YETIM YOZUV: biriktirmasiz dars. Ma'lumot YO'QOTILMAYDI —
                // shu uchlik uchun avtomatik Lesson yaratiladi.
                var count = entries.Count(e =>
                    e.TeacherId == entry.TeacherId &&
                    e.SubjectId == entry.SubjectId &&
                    e.ClassGroupId == entry.ClassGroupId);

                lesson = CreateLesson(
                    year.Id, entry.SubjectId, Math.Max(1, count),
                    entry.TeacherId, schoolClass, entireGroups[schoolClass.Id]);

                lessons[key] = lesson;
                result.Lessons++;
                result.OrphanLessons++;
                result.Messages.Add(
                    $"Yetim yozuv uchun avtomatik dars yaratildi: {schoolClass.Name}, " +
                    $"fan #{entry.SubjectId}, o'qituvchi #{entry.TeacherId} ({count} soat).");

                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            _context.Cards.Add(new Card
            {
                ScheduleId = entry.ScheduleId,
                Lesson = lesson,
                PeriodId = period.Id,
                DayNo = DayNumbering.ToDayNo(entry.DayOfWeek),
                // Eski modelda hafta o'lchovi yo'q edi — "har hafta".
                WeeksMask = 1,
                // Eski modelda juft dars yo'q edi — har yozuv bir soatlik.
                Length = 1,
                IsLocked = false,
                LegacyRoomNumber = entry.RoomNumber,
                LegacyScheduleEntryId = entry.Id
            });

            result.Cards++;
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // =====================================================================
    // V2_06 — TeacherAvailability → TimeOff
    // =====================================================================

    /// <summary>
    /// Eski <b>2 holatli</b> <c>TeacherAvailability</c> vaqt oraliqlarini yangi
    /// <b>3 holatli</b> <c>TimeOff</c> katakchalariga yoyadi.
    /// </summary>
    /// <remarks>
    /// <b>Qoida takrorlanmaydi:</b> "shu dars soati shu kunda ruxsatmi" savoliga
    /// <see cref="LessonAvailabilityRules"/> javob beradi (qora ro'yxat har doim ustun;
    /// oq ro'yxat faqat kamida bitta "ishlayman" oralig'i bo'lsa qo'llanadi).
    /// <para>
    /// <b>Nima hosil bo'ladi.</b> Eski modelda faqat ikki holat bor edi, shuning uchun
    /// ko'chirishdan faqat <c>Forbidden</c> katakchalari chiqadi;
    /// <c>NotRecommended</c> ("?", jarimali) — bu YANGI imkoniyat va faqat yangi
    /// ma'lumotdan paydo bo'ladi. Ruxsat etilgan katakcha uchun qator YOZILMAYDI
    /// (yo'qlik = ruxsat), shuning uchun jadval kerak bo'lganidan katta bo'lmaydi.
    /// </para>
    /// <para>
    /// <b>Idempotent:</b> <c>UX_TimeOffs_Owner_Slot</c> unikal indeksi bilan bir xil
    /// kalit oldindan o'qiladi va mavjud katakcha ikkinchi marta yozilmaydi.
    /// Mavjud qatorning holati ATAYLAB o'zgartirilmaydi — foydalanuvchi uni qo'lda
    /// tahrirlagan bo'lishi mumkin.
    /// </para>
    /// </remarks>
    private async Task EnsureTimeOffsAsync(
        AcademicYear year,
        Dictionary<int, Period> periods,
        LegacyBackfillResult result,
        CancellationToken ct)
    {
        var lessonPeriods = periods.Values.Where(p => !p.IsBreak).OrderBy(p => p.PeriodNo).ToList();
        if (lessonPeriods.Count == 0) return;

        // O'chirilgan o'qituvchi global filtr bilan chetlab o'tiladi — uning
        // cheklovini ko'chirishning ma'nosi yo'q.
        var teacherIds = await _context.Teachers
            .AsNoTracking()
            .Where(t => t.AcademicYearId == null || t.AcademicYearId == year.Id)
            .Select(t => t.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (teacherIds.Count == 0) return;

        var known = teacherIds.ToHashSet();

        var availabilities = await _context.TeacherAvailabilities
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (availabilities.Count == 0) return;

        // Mavjud katakchalar — takror yozmaslik uchun (idempotentlik).
        var existing = (await _context.TimeOffs
                .AsNoTracking()
                .Where(t => t.AcademicYearId == year.Id)
                .Select(t => new { t.OwnerKind, t.OwnerId, t.DayNo, t.PeriodNo, t.WeeksMask })
                .ToListAsync(ct).ConfigureAwait(false))
            .Select(t => (t.OwnerKind, t.OwnerId, t.DayNo, t.PeriodNo, t.WeeksMask))
            .ToHashSet();

        var added = 0;

        foreach (var group in availabilities
                     .Where(a => known.Contains(a.TeacherId))
                     .GroupBy(a => (a.TeacherId, a.DayOfWeek))
                     .OrderBy(g => g.Key.TeacherId).ThenBy(g => g.Key.DayOfWeek))
        {
            var dayNo = DayNumbering.ToDayNo(group.Key.DayOfWeek);
            if (dayNo < 0 || dayNo > 13) continue;

            var items = group.ToList();

            foreach (var period in lessonPeriods)
            {
                var start = period.StartTime.ToTimeSpan();
                var end = period.EndTime.ToTimeSpan();

                if (LessonAvailabilityRules.IsAllowed(items, start, end)) continue;

                // WeeksMask = 0 → "barcha haftalar": eski modelda hafta o'lchovi yo'q edi.
                var key = (ResourceOwnerKind.Teacher, group.Key.TeacherId, dayNo, period.PeriodNo, 0);
                if (!existing.Add(key)) continue;

                // Sababchi qatorni izlab qoldiramiz: qora ro'yxat bo'lsa aynan u,
                // aks holda kunning birinchi oq ro'yxat qatori.
                var source = LessonAvailabilityRules.FindBlocking(items, start, end)
                             ?? items.FirstOrDefault(a => a.IsAvailable)
                             ?? items[0];

                _context.TimeOffs.Add(new TimeOff
                {
                    AcademicYearId = year.Id,
                    OwnerKind = ResourceOwnerKind.Teacher,
                    OwnerId = group.Key.TeacherId,
                    DayNo = dayNo,
                    PeriodNo = period.PeriodNo,
                    WeeksMask = 0,
                    Availability = AvailabilityLevel.Forbidden,
                    Penalty = 0,
                    LegacyTeacherAvailabilityId = source.Id,
                });

                added++;
            }
        }

        if (added == 0) return;

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        result.TimeOffs += added;
        result.Messages.Add(
            $"Vaqt cheklovi ko'chirildi: {availabilities.Count} ta eski oraliqdan " +
            $"{added} ta taqiqlangan katakcha hosil bo'ldi.");
    }

    // =====================================================================
    // V2_07 — Card.LegacyRoomNumber → Classroom + CardClassroom
    // =====================================================================

    /// <summary>
    /// Erkin matnli xona nomlaridan <c>Classroom</c> yozuvlarini yaratadi va
    /// kartochkalarga <c>CardClassroom</c> orqali tayinlaydi.
    /// </summary>
    /// <remarks>
    /// <b>Nega bu muhim.</b> <c>LegacyRoomNumber</c> shunchaki matn — u bandlik
    /// proyeksiyasiga umuman tushmasdi, ya'ni "bitta xonada ikki dars" holati baza
    /// darajasida UMUMAN ushlanmasdi. Bog'lanish <c>CardClassroom</c> ga o'tgach xona
    /// <c>ResourceKind.Classroom</c> qatori sifatida <c>CardOccurrence</c> ga tushadi va
    /// unikal indeks uni rad etadi.
    /// <para>
    /// <b>To'qnashuvlar.</b> Eski modelda xona bandligi tekshirilmagani uchun bazada
    /// bir xonaga ikki dars yozilgan bo'lishi mumkin. Bunday holatda ko'chirish
    /// YIQILMAYDI: birinchi kartochka xonani oladi, qolganlari xonasiz qoladi va
    /// <see cref="LegacyBackfillResult.RoomConflicts"/> da sanaladi. Ma'lumot
    /// yo'qolmaydi — <c>Card.LegacyRoomNumber</c> matni joyida qoladi.
    /// </para>
    /// <para>
    /// <b>Xona ro'yxati bo'sh maktab.</b> Hech bir kartochkada xona matni bo'lmasa
    /// (foydalanuvchi bazasidagi holat) bu metod hech narsa yaratmaydi va butun tizim
    /// avvalgidek ishlaydi — xona hech qayerda majburiy emas.
    /// </para>
    /// </remarks>
    private async Task EnsureClassroomsAsync(
        AcademicYear year,
        Dictionary<int, Period> periods,
        LegacyBackfillResult result,
        CancellationToken ct)
    {
        var cards = await _context.Cards
            .AsNoTracking()
            .Where(c => c.Schedule!.AcademicYearId == year.Id && c.LegacyRoomNumber != null)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.ScheduleId,
                c.DayNo,
                c.PeriodId,
                c.WeeksMask,
                c.Length,
                c.LegacyRoomNumber,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Bo'sh va faqat probeldan iborat nomlar o'tkazib yuboriladi.
        var withRoom = cards
            .Select(c => new { Card = c, Room = (c.LegacyRoomNumber ?? string.Empty).Trim() })
            .Where(x => x.Room.Length > 0)
            .ToList();

        if (withRoom.Count == 0) return;

        var classrooms = await EnsureClassroomRowsAsync(year, withRoom.Select(x => x.Room), result, ct)
            .ConfigureAwait(false);

        // Mavjud tayinlashlar (idempotentlik) va ular egallagan bandlik kalitlari.
        var cardIds = withRoom.Select(x => x.Card.Id).ToList();
        var alreadyLinked = (await _context.CardClassrooms
                .AsNoTracking()
                .Where(cc => cardIds.Contains(cc.CardId))
                .Select(cc => cc.CardId)
                .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet();

        var periodNoById = periods.Values.ToDictionary(p => p.Id, p => p.PeriodNo);
        var taken = await LoadRoomOccupancyAsync(year, ct).ConfigureAwait(false);

        var linked = 0;
        var conflicts = 0;

        foreach (var item in withRoom)
        {
            if (alreadyLinked.Contains(item.Card.Id)) continue;
            if (!classrooms.TryGetValue(item.Room, out var classroom)) continue;
            if (!periodNoById.TryGetValue(item.Card.PeriodId, out var startPeriodNo)) continue;

            var keys = OccupancyKeys(
                item.Card.ScheduleId, classroom.Id, item.Card.DayNo,
                startPeriodNo, item.Card.Length, item.Card.WeeksMask);

            if (keys.Any(taken.Contains))
            {
                conflicts++;
                continue;
            }

            foreach (var key in keys) taken.Add(key);

            _context.CardClassrooms.Add(new CardClassroom
            {
                CardId = item.Card.Id,
                ClassroomId = classroom.Id,
            });

            linked++;
        }

        if (linked > 0) await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        result.CardClassrooms += linked;
        result.RoomConflicts += conflicts;

        if (linked > 0)
        {
            result.Messages.Add($"Xona tayinlandi: {linked} ta kartochka.");
        }

        if (conflicts > 0)
        {
            result.Messages.Add(
                $"{conflicts} ta kartochkaga xona tayinlanmadi — o'sha xona o'sha soatda " +
                "allaqachon band edi (eski modelda xona bandligi tekshirilmagan). " +
                "Xona nomi matni kartochkada saqlanib qoldi.");
        }
    }

    /// <summary>Matn xona nomlaridan <c>Classroom</c> yozuvlarini yaratadi (dublikatsiz).</summary>
    private async Task<Dictionary<string, Classroom>> EnsureClassroomRowsAsync(
        AcademicYear year, IEnumerable<string> roomNames, LegacyBackfillResult result, CancellationToken ct)
    {
        // Yumshoq o'chirilgan xona ham hisobga olinadi: aks holda uni qayta yaratishga
        // urinib, UX_Classrooms_AcademicYearId_LegacySourceName ni buzardik.
        var existing = await _context.Classrooms
            .IgnoreQueryFilters()
            .Where(c => c.AcademicYearId == year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var byLegacyName = new Dictionary<string, Classroom>(StringComparer.OrdinalIgnoreCase);
        foreach (var classroom in existing.Where(c => c.LegacySourceName is not null))
        {
            byLegacyName.TryAdd(classroom.LegacySourceName!, classroom);
        }

        // Qisqartma unikal indeksi faqat o'chirilmagan qatorlarga qo'llanadi.
        var takenShort = new HashSet<string>(
            existing.Where(c => !c.IsDeleted).Select(c => c.ShortName),
            StringComparer.OrdinalIgnoreCase);

        var created = 0;

        foreach (var name in roomNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (byLegacyName.ContainsKey(name)) continue;

            var classroom = new Classroom
            {
                AcademicYearId = year.Id,
                Name = name.Length > 128 ? name[..128] : name,
                ShortName = Unique(name, 24, takenShort),
                Kind = ClassroomKind.Regular,
                LegacySourceName = name.Length > 50 ? name[..50] : name,
            };

            _context.Classrooms.Add(classroom);
            byLegacyName[name] = classroom;
            created++;
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            result.Classrooms += created;
            result.Messages.Add($"Matn xona nomlaridan {created} ta xona yozuvi yaratildi.");
        }

        return byLegacyName;
    }

    /// <summary>Shu o'quv yilida xonalar allaqachon egallagan bandlik kalitlari.</summary>
    private async Task<HashSet<(int ScheduleId, int ClassroomId, int DayNo, int PeriodNo, int WeekNo)>>
        LoadRoomOccupancyAsync(AcademicYear year, CancellationToken ct)
    {
        var rows = await _context.CardOccurrences
            .AsNoTracking()
            .Where(o => o.ResourceKind == ResourceKind.Classroom &&
                        o.Schedule!.AcademicYearId == year.Id)
            .Select(o => new { o.ScheduleId, o.ResourceId, o.DayNo, o.PeriodNo, o.WeekNo })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .Select(o => (o.ScheduleId, o.ResourceId, o.DayNo, o.PeriodNo, o.WeekNo))
            .ToHashSet();
    }

    /// <summary>
    /// Kartochka xonani qaysi (jadval, kun, soat, hafta) katakchalarida band qilishi —
    /// <c>CardOccurrenceProjector</c> dagi yoyish formulasi bilan AYNAN bir xil.
    /// </summary>
    private static List<(int ScheduleId, int ClassroomId, int DayNo, int PeriodNo, int WeekNo)> OccupancyKeys(
        int scheduleId, int classroomId, int dayNo, int startPeriodNo, int length, int weeksMask)
    {
        var weeks = weeksMask == 0 ? new[] { 0 } : BitMask.Bits(weeksMask).ToArray();
        if (weeks.Length == 0) weeks = new[] { 0 };

        var keys = new List<(int, int, int, int, int)>();
        foreach (var week in weeks)
        {
            for (var offset = 0; offset < Math.Max(1, length); offset++)
            {
                keys.Add((scheduleId, classroomId, dayNo, startPeriodNo + offset, week));
            }
        }

        return keys;
    }

    // =====================================================================
    // Yordamchilar
    // =====================================================================

    private Lesson CreateLesson(
        int yearId, int subjectId, int periodsPerWeek,
        int teacherId, SchoolClass schoolClass, StudentGroup entireGroup)
    {
        var lesson = new Lesson
        {
            AcademicYearId = yearId,
            SubjectId = subjectId,
            PeriodsPerWeek = periodsPerWeek,
            // Ko'chirishda har doim 1: juft darslar keyin qo'lda belgilanadi.
            PeriodsPerCard = 1
        };

        _context.Lessons.Add(lesson);
        _context.LessonTeachers.Add(new LessonTeacher { Lesson = lesson, TeacherId = teacherId });
        _context.LessonClasses.Add(new LessonClass { Lesson = lesson, SchoolClass = schoolClass });
        _context.LessonGroups.Add(new LessonGroup { Lesson = lesson, StudentGroup = entireGroup });

        return lesson;
    }

    /// <summary>Har sinf uchun "Butun sinf" guruhini qaytaradi.</summary>
    private async Task<Dictionary<int, StudentGroup>> LoadEntireClassGroupsAsync(
        Dictionary<int, SchoolClass> classes, CancellationToken ct)
    {
        var classIds = classes.Values.Select(c => c.Id).ToHashSet();

        return await _context.StudentGroups
            .Where(g => classIds.Contains(g.SchoolClassId) && g.IsEntireClass)
            .ToDictionaryAsync(g => g.SchoolClassId, g => g, ct)
            .ConfigureAwait(false);
    }

    private static TimeOnly ToTimeOnly(TimeSpan value)
        => new(value.Hours, value.Minutes);

    /// <summary>Ma'lum bo'lmagan dars soati uchun vaqt taxmini: 08:30 dan 55 daqiqalik qadam.</summary>
    private static (TimeOnly Start, TimeOnly End) EstimateTimes(int number)
    {
        var startMinutes = 8 * 60 + 30
            + Math.Max(0, number - 1) * (DefaultLessonMinutes + DefaultBreakMinutes);

        startMinutes = Math.Min(startMinutes, 23 * 60);
        var endMinutes = Math.Min(startMinutes + DefaultLessonMinutes, 23 * 60 + 59);

        return (new TimeOnly(startMinutes / 60, startMinutes % 60),
                new TimeOnly(endMinutes / 60, endMinutes % 60));
    }

    /// <summary>"Aliyev Vali Anvarovich" → "Aliyev V.A."</summary>
    private static string ShortenFullName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0];

        var initials = string.Concat(parts.Skip(1).Select(p => $"{char.ToUpperInvariant(p[0])}."));
        return $"{parts[0]} {initials}";
    }

    /// <summary>Uzunlikni cheklaydi va to'plam ichida noyob qiladi (dublikatga raqam qo'shadi).</summary>
    private static string Unique(string candidate, int maxLength, HashSet<string> taken)
    {
        var baseValue = string.IsNullOrWhiteSpace(candidate) ? "?" : candidate.Trim();
        if (baseValue.Length > maxLength) baseValue = baseValue[..maxLength];

        var value = baseValue;
        var suffix = 2;

        while (!taken.Add(value))
        {
            var tail = suffix.ToString();
            var head = baseValue.Length + tail.Length > maxLength
                ? baseValue[..(maxLength - tail.Length)]
                : baseValue;
            value = head + tail;
            suffix++;
        }

        return value;
    }
}
