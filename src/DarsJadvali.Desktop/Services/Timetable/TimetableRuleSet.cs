using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// Baholash uchun kerak bo'ladigan barcha "sekin" ma'lumotning UI tomonidagi keshi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kesh kerak:</b> <c>IScheduleValidator.ValidateAsync</c> har chaqiruvda butun
/// bazani qaytadan o'qiydi. Drag paytida kursor har siljiganda uni chaqirib bo'lmaydi —
/// 60 fps uchun baholash &lt;16 ms bo'lishi shart (<c>03-asc-features-ux.md</c> §4.6).
/// </para>
/// <para>
/// <b>Qoida endi takrorlanmaydi.</b> Ilgari bu sinf ish kunlari, kunlik chegaralar va
/// ayniqsa o'qituvchining ish vaqti qoidasini (<c>LessonAvailabilityRules</c>) o'zi
/// qaytadan hisoblardi, chunki <c>ScheduleSnapshot</c> <c>internal</c> edi. Endi ular
/// public va <see cref="FromSnapshot"/> hamma qiymatni <b>Application nusxasidan</b>
/// oladi: <c>IsActiveDay</c>, <c>MaxLessonNumberOf</c>, <c>BlockedTeacherSlots</c> va
/// biriktirmalardagi haftalik me'yor. Bu yerda faqat <b>nusxa olish</b> qoladi —
/// hech qanday qoida qayta yozilmaydi.
/// </para>
/// <para>
/// Nusxa <see cref="IScheduleSnapshotProvider.LoadAsync"/> bilan <b>bir marta</b>
/// yuklanadi; keyingi barcha baholash faqat xotirada bajariladi.
/// </para>
/// </remarks>
public sealed class TimetableRuleSet
{
    private readonly HashSet<WeekDay> _activeDays;
    private readonly Dictionary<WeekDay, int> _maxPeriods;
    private readonly HashSet<(int TeacherId, WeekDay Day, int Period)> _teacherBlocked;
    private readonly Dictionary<(int TeacherId, int SubjectId, int ClassGroupId), int> _weeklyQuota;

    /// <summary>Yangi qoidalar to'plami yaratadi.</summary>
    /// <param name="days">Faol ish kunlari (ko'rsatish tartibida).</param>
    /// <param name="maxPeriods">Har kun uchun maksimal dars raqami.</param>
    /// <param name="teacherBlocked">O'qituvchi ishlamaydigan (kun, soat) juftliklari.</param>
    /// <param name="weeklyQuota">Biriktirmadagi haftalik soat me'yori.</param>
    /// <param name="periodNumbers">To'rda ko'rinadigan dars soati raqamlari (ikki smena — 1..12).</param>
    public TimetableRuleSet(
        IReadOnlyList<WeekDay> days,
        IReadOnlyDictionary<WeekDay, int> maxPeriods,
        IEnumerable<(int TeacherId, WeekDay Day, int Period)>? teacherBlocked = null,
        IReadOnlyDictionary<(int TeacherId, int SubjectId, int ClassGroupId), int>? weeklyQuota = null,
        IReadOnlyList<int>? periodNumbers = null)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(maxPeriods);

        Days = days;
        _activeDays = new HashSet<WeekDay>(days);
        _maxPeriods = new Dictionary<WeekDay, int>(maxPeriods);
        _teacherBlocked = teacherBlocked is null
            ? new HashSet<(int, WeekDay, int)>()
            : new HashSet<(int, WeekDay, int)>(teacherBlocked);
        _weeklyQuota = weeklyQuota is null
            ? new Dictionary<(int, int, int), int>()
            : new Dictionary<(int, int, int), int>(weeklyQuota);

        PeriodNumbers = periodNumbers is { Count: > 0 }
            ? periodNumbers.Distinct().OrderBy(n => n).ToList()
            : Enumerable.Range(1, _maxPeriods.Count == 0 ? 0 : _maxPeriods.Values.Max()).ToList();

        MaxPeriod = PeriodNumbers.Count == 0
            ? (_maxPeriods.Count == 0 ? 0 : _maxPeriods.Values.Max())
            : PeriodNumbers[^1];
    }

    /// <summary>Faol ish kunlari (ustunlar tartibi).</summary>
    public IReadOnlyList<WeekDay> Days { get; }

    /// <summary>To'rdagi eng katta dars raqami (ikki smena uchun — masalan 12).</summary>
    public int MaxPeriod { get; }

    /// <summary>
    /// To'rda ko'rinadigan dars soati raqamlari. Ikki smenada raqamlar uzluksiz:
    /// 1-smena 1..6, 2-smena 7..12 (<c>Period.PeriodNo</c> o'quv yili ichida global).
    /// </summary>
    public IReadOnlyList<int> PeriodNumbers { get; }

    /// <summary>Bo'sh (ma'lumotsiz) to'plam — sinovlar va boshlang'ich holat uchun.</summary>
    public static TimetableRuleSet Empty { get; } =
        new(Array.Empty<WeekDay>(), new Dictionary<WeekDay, int>());

    /// <summary>
    /// Application nusxasidan qoidalarni <b>ko'chirib</b> oladi — hech biri qayta hisoblanmaydi.
    /// </summary>
    /// <param name="snapshot">Bir marta yuklangan jadval nusxasi.</param>
    /// <param name="periodNumbers">
    /// To'rda ko'rinadigan dars soati raqamlari (yangi modeldagi <c>Period.PeriodNo</c>).
    /// Bo'sh bo'lsa nusxadagi eski <c>LessonSlot</c> raqamlari ishlatiladi.
    /// </param>
    /// <param name="blockedTeacherSlots">
    /// O'qituvchi ishlamaydigan uchliklar. <c>null</c> bo'lsa
    /// <see cref="ScheduleSnapshot.BlockedTeacherSlots"/> ishlatiladi — ikkalasi ham
    /// AYNAN bitta qoidaga (<c>LessonAvailabilityRules</c>) tayanadi.
    /// </param>
    public static TimetableRuleSet FromSnapshot(
        ScheduleSnapshot snapshot,
        IReadOnlyList<int>? periodNumbers = null,
        IEnumerable<(int TeacherId, WeekDay Day, int Period)>? blockedTeacherSlots = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var numbers = periodNumbers is { Count: > 0 }
            ? periodNumbers.Distinct().OrderBy(n => n).ToList()
            : snapshot.LessonSlots.Select(s => s.LessonNumber).Distinct().OrderBy(n => n).ToList();

        var lastNumber = numbers.Count == 0 ? 0 : numbers[^1];

        var days = snapshot.ActiveWorkDays.Select(w => w.DayOfWeek).ToList();

        var maxPeriods = new Dictionary<WeekDay, int>();
        foreach (var day in days)
        {
            // Ikki smenada qo'ng'iroq jadvali (Period) eski WorkDay chegarasidan uzunroq
            // bo'lishi mumkin — chegara ikkisining kattasi bo'ladi.
            maxPeriods[day] = Math.Max(snapshot.MaxLessonNumberOf(day), lastNumber);
        }

        var quota = new Dictionary<(int, int, int), int>();
        foreach (var assignment in snapshot.Assignments)
        {
            quota[(assignment.TeacherId, assignment.SubjectId, assignment.ClassGroupId)] =
                assignment.WeeklyHoursCount;
        }

        var blocked = blockedTeacherSlots ?? snapshot
            .BlockedTeacherSlots()
            .Select(x => (x.TeacherId, x.Day, x.LessonNumber));

        return new TimetableRuleSet(days, maxPeriods, blocked, quota, numbers);
    }

    /// <summary>Kun faol ish kunimi.</summary>
    public bool IsActiveDay(WeekDay day) => _activeDays.Contains(day);

    /// <summary>Shu kunning oxirgi dars raqami.</summary>
    public int MaxPeriodOf(WeekDay day) => _maxPeriods.TryGetValue(day, out var value) ? value : 0;

    /// <summary>O'qituvchi shu (kun, soat) da ishlamaydimi.</summary>
    public bool IsTeacherBlocked(int teacherId, WeekDay day, int period)
        => _teacherBlocked.Contains((teacherId, day, period));

    /// <summary>Biriktirmadagi haftalik me'yor (topilmasa 0 — tekshirilmaydi).</summary>
    public int WeeklyQuota(int teacherId, int subjectId, int classGroupId)
        => _weeklyQuota.TryGetValue((teacherId, subjectId, classGroupId), out var value) ? value : 0;
}
