using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Validation;

/// <summary>
/// Jadval tekshiruvi uchun kerak bo'ladigan barcha ma'lumotning xotiradagi nusxasi.
/// Bazadan bir marta yuklanadi, keyin barcha tekshiruvlar xotirada bajariladi.
/// </summary>
/// <remarks>
/// <b>PUBLIC (faqat o'qish uchun)</b>. Ilgari <c>internal</c> edi va Desktop uni
/// ishlata olmagani uchun <c>TimetableBoard.Evaluate</c> da qoidalar TAKRORLANGAN edi.
/// Endi prezentatsiya qatlami nusxani <see cref="IScheduleSnapshotProvider"/> orqali
/// oladi va <see cref="ScheduleValidator.Evaluate"/> bilan baholaydi — qoida yagona manbada.
/// <para>
/// Nusxani <b>o'zgartiradigan</b> a'zolar (<c>Add</c>, <c>ClearEntries</c>) ataylab
/// <c>internal</c> qoldirildi: ular faqat generatorning ichki "nima bo'lardi-agar"
/// siklida ishlatiladi, tashqi chaqiruvchi uchun nusxa o'zgarmas.
/// </para>
/// </remarks>
public sealed class ScheduleSnapshot
{
    private readonly Dictionary<int, Teacher> _teachers;
    private readonly Dictionary<int, Subject> _subjects;
    private readonly Dictionary<int, ClassGroup> _classGroups;
    private readonly Dictionary<(int TeacherId, int SubjectId, int ClassGroupId), TeacherAssignment> _assignmentMap;
    private readonly Dictionary<WeekDay, WorkDay> _workDays;
    private readonly Dictionary<(int TeacherId, WeekDay Day), List<TeacherAvailability>> _availabilities;
    private readonly Dictionary<int, LessonSlot> _slots;

    private readonly List<ScheduleEntry> _entries = new();
    private readonly Dictionary<(WeekDay Day, int Lesson), List<ScheduleEntry>> _bySlot = new();
    private readonly Dictionary<(int ClassGroupId, WeekDay Day), List<ScheduleEntry>> _byClassDay = new();
    private readonly Dictionary<(int TeacherId, int SubjectId, int ClassGroupId), List<ScheduleEntry>> _byTriple = new();

    private ScheduleSnapshot(
        int scheduleId,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<Subject> subjects,
        IReadOnlyList<ClassGroup> classGroups,
        IReadOnlyList<TeacherAssignment> assignments,
        IReadOnlyList<WorkDay> workDays,
        IReadOnlyList<TeacherAvailability> availabilities,
        IReadOnlyList<LessonSlot> slots,
        IReadOnlyList<ScheduleEntry> entries)
    {
        ScheduleId = scheduleId;

        _teachers = new Dictionary<int, Teacher>();
        foreach (var t in teachers)
        {
            _teachers[t.Id] = t;
        }

        _subjects = new Dictionary<int, Subject>();
        foreach (var s in subjects)
        {
            _subjects[s.Id] = s;
        }

        _classGroups = new Dictionary<int, ClassGroup>();
        foreach (var c in classGroups)
        {
            _classGroups[c.Id] = c;
        }

        Assignments = assignments;
        _assignmentMap = new Dictionary<(int, int, int), TeacherAssignment>();
        foreach (var a in assignments)
        {
            _assignmentMap.TryAdd((a.TeacherId, a.SubjectId, a.ClassGroupId), a);
        }

        _workDays = new Dictionary<WeekDay, WorkDay>();
        foreach (var w in workDays)
        {
            _workDays.TryAdd(w.DayOfWeek, w);
        }

        _availabilities = new Dictionary<(int, WeekDay), List<TeacherAvailability>>();
        foreach (var a in availabilities)
        {
            var key = (a.TeacherId, a.DayOfWeek);
            if (!_availabilities.TryGetValue(key, out var list))
            {
                list = new List<TeacherAvailability>();
                _availabilities[key] = list;
            }

            list.Add(a);
        }

        _slots = new Dictionary<int, LessonSlot>();
        foreach (var s in slots)
        {
            _slots.TryAdd(s.LessonNumber, s);
        }

        foreach (var e in entries)
        {
            Add(e);
        }
    }

    /// <summary>
    /// Nusxa qaysi dars jadvali (varianti) uchun olingan.
    /// Konflikt tekshiruvi FAQAT shu jadval ichida bajariladi —
    /// boshqa o'quv yili yoki boshqa variant konflikt bermaydi.
    /// </summary>
    public int ScheduleId { get; }

    /// <summary>Barcha biriktirmalar.</summary>
    public IReadOnlyList<TeacherAssignment> Assignments { get; }

    /// <summary>Joriy jadval yozuvlari.</summary>
    public IReadOnlyList<ScheduleEntry> Entries => _entries;

    /// <summary>Faol ish kunlari (kun tartibida).</summary>
    public IReadOnlyList<WorkDay> ActiveWorkDays =>
        _workDays.Values.Where(w => w.IsActive).OrderBy(w => (int)w.DayOfWeek).ToList();

    /// <summary>Barcha o'qituvchilar (Id tartibida).</summary>
    public IReadOnlyList<Teacher> Teachers => _teachers.Values.OrderBy(t => t.Id).ToList();

    /// <summary>Barcha fanlar (Id tartibida).</summary>
    public IReadOnlyList<Subject> Subjects => _subjects.Values.OrderBy(s => s.Id).ToList();

    /// <summary>Barcha sinflar (Id tartibida).</summary>
    public IReadOnlyList<ClassGroup> ClassGroups => _classGroups.Values.OrderBy(c => c.Id).ToList();

    /// <summary>Dars soatlari (raqam tartibida).</summary>
    public IReadOnlyList<LessonSlot> LessonSlots =>
        _slots.Values.OrderBy(s => s.LessonNumber).ToList();

    /// <summary>Shu kundagi eng katta dars raqami (kun faol bo'lmasa <c>0</c>).</summary>
    public int MaxLessonNumberOf(WeekDay day) =>
        _workDays.TryGetValue(day, out var w) && w.IsActive ? w.MaxLessonsPerDay : 0;

    /// <summary>Kun faol ish kunimi.</summary>
    public bool IsActiveDay(WeekDay day) => _workDays.TryGetValue(day, out var w) && w.IsActive;

    /// <summary>
    /// O'qituvchi shu (kun, dars raqami) da ishlay oladimi — <c>TEACHER_UNAVAILABLE</c>
    /// qoidasining dars soati o'lchovidagi ko'rinishi.
    /// </summary>
    /// <remarks>
    /// Prezentatsiya qatlami (drag paytidagi tez baholash) AYNAN shu metodga tayanishi
    /// kerak: qoida <see cref="LessonAvailabilityRules"/> dan olinadi, takrorlanmaydi.
    /// Dars soatining real vaqti noma'lum bo'lsa cheklov yo'q deb hisoblanadi.
    /// </remarks>
    public bool IsTeacherAvailableAt(int teacherId, WeekDay day, int lessonNumber)
    {
        if (!_slots.TryGetValue(lessonNumber, out var slot)) return true;
        if (!_availabilities.TryGetValue((teacherId, day), out var items) || items.Count == 0) return true;

        return LessonAvailabilityRules.IsAllowed(items, slot.StartTime, slot.EndTime);
    }

    /// <summary>
    /// Barcha o'qituvchilar uchun "ishlamaydigan" (o'qituvchi, kun, soat) uchliklari.
    /// Desktop shu ro'yxatdan o'z keshini quradi — bazaga 40 ta so'rov ketmaydi.
    /// </summary>
    public IReadOnlyList<(int TeacherId, WeekDay Day, int LessonNumber)> BlockedTeacherSlots()
    {
        var result = new List<(int, WeekDay, int)>();

        foreach (var teacherId in _teachers.Keys)
        {
            foreach (var workDay in _workDays.Values.Where(w => w.IsActive))
            {
                foreach (var slot in _slots.Values)
                {
                    if (!IsTeacherAvailableAt(teacherId, workDay.DayOfWeek, slot.LessonNumber))
                    {
                        result.Add((teacherId, workDay.DayOfWeek, slot.LessonNumber));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Bazadan barcha kerakli ma'lumotni bir marta yuklaydi.
    /// Dars yozuvlari FAQAT bitta jadvaldan olinadi (<paramref name="scheduleId"/>,
    /// <c>null</c> bo'lsa — faol jadvaldan).
    /// </summary>
    public static async Task<ScheduleSnapshot> LoadAsync(
        IUnitOfWork uow, int? scheduleId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uow);

        var targetScheduleId = await ActiveScheduleResolver
            .ResolveIdAsync(uow, scheduleId, ct).ConfigureAwait(false);

        // Nusxa FAQAT o'qish uchun — barcha so'rovlar kuzatuvsiz (05-audit K-07).
        var teachers = await uow.Teachers.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var subjects = await uow.Subjects.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var classGroups = await uow.ClassGroups.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var assignments = await uow.Assignments.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var workDays = await uow.WorkDays.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var availabilities = await uow.Availabilities.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var slots = await uow.LessonSlots.GetAllReadOnlyAsync(ct).ConfigureAwait(false);

        // Boshqa jadvallardagi (boshqa yildagi yoki boshqa variantdagi) yozuvlar butunlay chetlab o'tiladi.
        // 05-audit K-07/K-19: ilgari BARCHA yillarning yozuvlari o'qilib, keyin xotirada
        // filtrlanardi. Endi shart SQL'ga tushadi va natija kuzatilmaydi (AsNoTracking).
        var entries = await uow.ScheduleEntries
            .GetWhereAsync(e => e.ScheduleId == targetScheduleId, ct).ConfigureAwait(false);

        return new ScheduleSnapshot(
            targetScheduleId, teachers, subjects, classGroups, assignments, workDays, availabilities, slots, entries);
    }

    /// <summary>Xotiradagi jadvalga yozuv qo'shadi (indekslar bilan). Faqat ichki foydalanish.</summary>
    internal void Add(ScheduleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entries.Add(entry);
        Index(_bySlot, (entry.DayOfWeek, entry.LessonNumber), entry);
        Index(_byClassDay, (entry.ClassGroupId, entry.DayOfWeek), entry);
        Index(_byTriple, (entry.TeacherId, entry.SubjectId, entry.ClassGroupId), entry);
    }

    /// <summary>Xotiradagi jadvalni butunlay tozalaydi. Faqat ichki foydalanish.</summary>
    internal void ClearEntries()
    {
        _entries.Clear();
        _bySlot.Clear();
        _byClassDay.Clear();
        _byTriple.Clear();
    }

    /// <summary>
    /// Loyihani xotiradagi nusxaga qo'llaydi: shu Id li eski yozuv olib tashlanadi va
    /// yangi holat qo'yiladi. Ommaviy joylashtirishda (<c>PlaceManyAsync</c>) keyingi
    /// loyihalar oldingilarini KO'RISHI uchun kerak.
    /// </summary>
    internal void Apply(ScheduleEntryDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.Id.HasValue)
        {
            _entries.RemoveAll(e => e.Id == draft.Id.Value);
        }

        _entries.Add(new ScheduleEntry
        {
            Id = draft.Id ?? 0,
            ScheduleId = ScheduleId,
            ClassGroupId = draft.ClassGroupId,
            SubjectId = draft.SubjectId,
            TeacherId = draft.TeacherId,
            DayOfWeek = draft.DayOfWeek,
            LessonNumber = draft.LessonNumber,
            RoomNumber = draft.RoomNumber,
        });

        Reindex();
    }

    /// <summary>Qidiruv indekslarini <see cref="_entries"/> dan qaytadan quradi.</summary>
    private void Reindex()
    {
        _bySlot.Clear();
        _byClassDay.Clear();
        _byTriple.Clear();

        foreach (var entry in _entries)
        {
            Index(_bySlot, (entry.DayOfWeek, entry.LessonNumber), entry);
            Index(_byClassDay, (entry.ClassGroupId, entry.DayOfWeek), entry);
            Index(_byTriple, (entry.TeacherId, entry.SubjectId, entry.ClassGroupId), entry);
        }
    }

    /// <summary>Loyihani barcha qoidalar bo'yicha tekshiradi (CONTRACT 2.2 tartibida).</summary>
    public List<Conflict> Validate(ScheduleEntryDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var conflicts = new List<Conflict>();
        var dayName = draft.DayOfWeek.ToUzbek();
        var teacherName = TeacherName(draft.TeacherId);
        var className = ClassName(draft.ClassGroupId);
        var subjectName = SubjectName(draft.SubjectId);

        // 1. DAY_INACTIVE
        _workDays.TryGetValue(draft.DayOfWeek, out var workDay);
        if (workDay is null)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.DayInactive,
                $"{dayName} kuni ish kunlari ro'yxatida yo'q."));
        }
        else if (!workDay.IsActive)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.DayInactive,
                $"{dayName} kuni dam olish kuni deb belgilangan, dars qo'yib bo'lmaydi."));
        }

        // 2. LESSON_OUT_OF_RANGE
        var maxLessons = workDay?.MaxLessonsPerDay ?? 0;
        if (draft.LessonNumber < 1 || (workDay is not null && draft.LessonNumber > maxLessons))
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.LessonOutOfRange,
                workDay is null
                    ? $"{draft.LessonNumber}-soat noto'g'ri: dars raqami 1 dan kichik bo'lishi mumkin emas."
                    : $"{dayName} kuni {draft.LessonNumber}-soat yo'q: ruxsat etilgan oraliq 1–{maxLessons}."));
        }

        // 3. TEACHER_INACTIVE
        if (!_teachers.TryGetValue(draft.TeacherId, out var teacher))
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.TeacherInactive,
                $"O'qituvchi topilmadi (ID: {draft.TeacherId})."));
        }
        else if (!teacher.IsActive)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.TeacherInactive,
                $"{teacherName} faol emas, unga dars qo'yib bo'lmaydi."));
        }

        // 4. NO_ASSIGNMENT
        var hasAssignment = _assignmentMap.TryGetValue(
            (draft.TeacherId, draft.SubjectId, draft.ClassGroupId), out var assignment);
        if (!hasAssignment)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.NoAssignment,
                $"{teacherName} uchun {className} sinfida {subjectName} fani biriktirilmagan."));
        }

        var slotEntries = _bySlot.TryGetValue((draft.DayOfWeek, draft.LessonNumber), out var atSlot)
            ? atSlot
            : (IReadOnlyList<ScheduleEntry>)Array.Empty<ScheduleEntry>();

        // 5. TEACHER_BUSY
        var teacherBusy = slotEntries.FirstOrDefault(e => e.TeacherId == draft.TeacherId && !IsSame(e, draft));
        if (teacherBusy is not null)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.TeacherBusy,
                $"{dayName} kuni {draft.LessonNumber}-soatda {teacherName} allaqachon " +
                $"{ClassName(teacherBusy.ClassGroupId)} sinfida dars o'tadi."));
        }

        // 6. CLASS_BUSY
        var classBusy = slotEntries.FirstOrDefault(e => e.ClassGroupId == draft.ClassGroupId && !IsSame(e, draft));
        if (classBusy is not null)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.ClassBusy,
                $"{className} sinfida {dayName} {draft.LessonNumber}-soat allaqachon band " +
                $"({SubjectName(classBusy.SubjectId)})."));
        }

        // 7. ROOM_BUSY
        if (!string.IsNullOrWhiteSpace(draft.RoomNumber))
        {
            var room = draft.RoomNumber.Trim();
            var roomBusy = slotEntries.FirstOrDefault(e =>
                !IsSame(e, draft) &&
                !string.IsNullOrWhiteSpace(e.RoomNumber) &&
                string.Equals(e.RoomNumber.Trim(), room, StringComparison.OrdinalIgnoreCase));
            if (roomBusy is not null)
            {
                conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.RoomBusy,
                    $"{room}-xona {dayName} kuni {draft.LessonNumber}-soatda band " +
                    $"({ClassName(roomBusy.ClassGroupId)} sinfi, {SubjectName(roomBusy.SubjectId)})."));
            }
        }

        // 8. TEACHER_UNAVAILABLE
        var unavailable = CheckAvailability(draft, teacherName, dayName);
        if (unavailable is not null)
        {
            conflicts.Add(unavailable);
        }

        // 9. WEEKLY_HOURS_EXCEEDED (Warning)
        if (assignment is not null)
        {
            var placed = _byTriple.TryGetValue(
                (draft.TeacherId, draft.SubjectId, draft.ClassGroupId), out var tripleEntries)
                ? tripleEntries.Count(e => !IsSame(e, draft))
                : 0;

            if (placed + 1 > assignment.WeeklyHoursCount)
            {
                conflicts.Add(new Conflict(ConflictSeverity.Warning, ConflictCodes.WeeklyHoursExceeded,
                    $"{teacherName} — {className} sinfi, {subjectName}: haftalik me'yor " +
                    $"{assignment.WeeklyHoursCount} soat, bu dars bilan {placed + 1} soat bo'ladi."));
            }
        }

        // 10. SUBJECT_REPEATED_IN_DAY (Warning)
        if (_byClassDay.TryGetValue((draft.ClassGroupId, draft.DayOfWeek), out var dayEntries))
        {
            var repeated = dayEntries.FirstOrDefault(e => e.SubjectId == draft.SubjectId && !IsSame(e, draft));
            if (repeated is not null)
            {
                conflicts.Add(new Conflict(ConflictSeverity.Warning, ConflictCodes.SubjectRepeatedInDay,
                    $"{className} sinfida {subjectName} fani {dayName} kuni allaqachon " +
                    $"{repeated.LessonNumber}-soatda o'tiladi."));
            }
        }

        return conflicts;
    }

    /// <summary>CONTRACT 2.2 §8 — o'qituvchining ish vaqti tekshiruvi.</summary>
    private Conflict? CheckAvailability(ScheduleEntryDraft draft, string teacherName, string dayName)
    {
        // Dars soatining real vaqti noma'lum bo'lsa — tekshiruv o'tkazib yuboriladi.
        if (!_slots.TryGetValue(draft.LessonNumber, out var slot))
        {
            return null;
        }

        // Bu kun uchun umuman yozuv bo'lmasa — cheklov yo'q.
        if (!_availabilities.TryGetValue((draft.TeacherId, draft.DayOfWeek), out var items) || items.Count == 0)
        {
            return null;
        }

        var lessonTime = $"{Format(slot.StartTime)}-{Format(slot.EndTime)}";

        // Qora ro'yxat: "band" oraliq bilan kesishsa — konflikt (har doim ustun).
        var blocking = LessonAvailabilityRules.FindBlocking(items, slot.StartTime, slot.EndTime);
        if (blocking is not null)
        {
            return new Conflict(ConflictSeverity.Error, ConflictCodes.TeacherUnavailable,
                $"{teacherName} {dayName} kuni {Format(blocking.StartTime)}-{Format(blocking.EndTime)} " +
                $"oralig'ida band, {lessonTime} darsini qo'yib bo'lmaydi.");
        }

        // Oq ro'yxat faqat kamida bitta "ishlayman" oralig'i bo'lsa qo'llanadi.
        var free = LessonAvailabilityRules.WhiteList(items);
        if (free.Count == 0)
        {
            return null;
        }

        if (free.Any(a => LessonAvailabilityRules.Covers(a, slot.StartTime, slot.EndTime)))
        {
            return null;
        }

        var workHours = string.Join(", ", free
            .OrderBy(a => a.StartTime)
            .Select(a => $"{Format(a.StartTime)}-{Format(a.EndTime)}"));

        return new Conflict(ConflictSeverity.Error, ConflictCodes.TeacherUnavailable,
            $"{teacherName} {dayName} kuni {lessonTime} vaqtida ishlamaydi (ish vaqti: {workHours}).");
    }

    /// <summary>Yozuv loyihaning o'zimi (ko'chirishda o'zini istisno qilish uchun).</summary>
    private static bool IsSame(ScheduleEntry entry, ScheduleEntryDraft draft) =>
        draft.Id.HasValue && entry.Id == draft.Id.Value;

    private static string Format(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}";

    private static void Index<TKey>(Dictionary<TKey, List<ScheduleEntry>> index, TKey key, ScheduleEntry entry)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<ScheduleEntry>();
            index[key] = list;
        }

        list.Add(entry);
    }

    /// <summary>O'qituvchi FIO si.</summary>
    public string TeacherName(int id) =>
        _teachers.TryGetValue(id, out var t) && !string.IsNullOrWhiteSpace(t.FullName)
            ? t.FullName
            : $"O'qituvchi #{id}";

    /// <summary>Sinf nomi.</summary>
    public string ClassName(int id) =>
        _classGroups.TryGetValue(id, out var c) && !string.IsNullOrWhiteSpace(c.Name)
            ? c.Name
            : $"Sinf #{id}";

    /// <summary>Fan nomi.</summary>
    public string SubjectName(int id) =>
        _subjects.TryGetValue(id, out var s) && !string.IsNullOrWhiteSpace(s.Name)
            ? s.Name
            : $"Fan #{id}";

    /// <summary>Sinfning asosiy xonasi.</summary>
    public string? ClassRoom(int id) =>
        _classGroups.TryGetValue(id, out var c) ? c.RoomNumber : null;
}
