using DarsJadvali.Application.Board;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Validation;
using DarsJadvali.Infrastructure.Export.Printing;

namespace DarsJadvali.Web.Dtos;

// ---------------------------------------------------------------------------
// Yangi Card/Lesson modelining yassi (flat) DTO'lari.
// Eski ScheduleEntryDto o'chirilmadi — Desktop hali ko'chmoqda.
// ---------------------------------------------------------------------------

/// <summary>
/// Jadval to'ridagi bitta kartochka.
/// </summary>
/// <remarks>
/// Eski <see cref="ScheduleEntryDto"/> dan farqi — bu yerda AYNI shu to'rt maydon bor:
/// <c>length</c> (juft dars), <c>weeksMask</c>/<c>weekLabel</c> (A/B hafta),
/// <c>groupName</c> (sinf bo'linmasi) va <c>isLocked</c> (qulf bazada saqlanadi).
/// </remarks>
public sealed record CardDto(
    int CardId,
    int ScheduleId,
    int LessonId,
    int SubjectId,
    string SubjectName,
    IReadOnlyList<int> TeacherIds,
    IReadOnlyList<string> TeacherNames,
    IReadOnlyList<int> ClassIds,
    string ClassName,
    IReadOnlyList<int> GroupIds,
    string GroupName,
    int DayNo,
    int PeriodId,
    int PeriodNo,
    IReadOnlyList<int> PeriodNumbers,
    int Length,
    bool IsDouble,
    int WeeksMask,
    string? WeekLabel,
    bool IsLocked,
    string? RoomNumber);

/// <summary>Joylashtirilmagan dars (reja − fakt).</summary>
public sealed record UnplacedLessonDto(
    int LessonId,
    int SubjectId,
    string SubjectName,
    string ClassName,
    string GroupName,
    IReadOnlyList<int> TeacherIds,
    IReadOnlyList<string> TeacherNames,
    int PeriodsPerWeek,
    int PlacedPeriods,
    int RemainingPeriods,
    int PeriodsPerCard);

/// <summary>To'r ustuni — ish kuni.</summary>
public sealed record BoardDayDto(int DayNo, string Name, string ShortName);

/// <summary>To'r qatori — dars soati (raqam smenalar bo'ylab UZLUKSIZ).</summary>
public sealed record BoardPeriodDto(
    int PeriodId,
    int PeriodNo,
    string Label,
    string StartTime,
    string EndTime,
    int? ShiftId,
    string? ShiftName);

/// <summary>Smena.</summary>
public sealed record BoardShiftDto(int Id, int ShiftNo, string Name);

/// <summary>Sinf (v2 <c>SchoolClass</c>).</summary>
public sealed record BoardClassDto(int Id, string Name, int? ShiftId, int StudentCount);

/// <summary>O'qituvchi.</summary>
public sealed record BoardTeacherDto(int Id, string FullName, string? ColorCode);

/// <summary>Fan.</summary>
public sealed record BoardSubjectDto(int Id, string Name, string? ShortName, string? ColorCode);

/// <summary>
/// To'rning o'qlari va ma'lumotnomalari — sahifa buni BIR MARTA oladi,
/// keyin kartochkalarni faqat <c>/api/board/cards</c> dan yangilaydi.
/// </summary>
public sealed record BoardAxesDto(
    int ScheduleId,
    string ScheduleName,
    int WeeksInCycle,
    IReadOnlyList<BoardDayDto> Days,
    IReadOnlyList<BoardPeriodDto> Periods,
    IReadOnlyList<BoardShiftDto> Shifts,
    IReadOnlyList<BoardClassDto> Classes,
    IReadOnlyList<BoardTeacherDto> Teachers,
    IReadOnlyList<BoardSubjectDto> Subjects);

/// <summary>Bitta kartochkani ko'chirish so'rovi.</summary>
public sealed record CardPlacementRequest(int CardId, int DayNo, int PeriodId, int? WeeksMask = null);

/// <summary>Ommaviy ko'chirish so'rovi — hammasi BITTA tranzaksiyada.</summary>
public sealed record CardPlaceRequest(
    IReadOnlyList<CardPlacementRequest>? Placements,
    bool Force = false,
    int? ScheduleId = null);

/// <summary>Qulflash so'rovi.</summary>
public sealed record CardLockRequest(int CardId, bool IsLocked);

/// <summary>Bitta kartochka bo'yicha natija.</summary>
public sealed record CardPlacementResultDto(
    int CardId,
    bool Placed,
    IReadOnlyList<ConflictDto> Conflicts);

/// <summary>Ommaviy ko'chirish natijasi.</summary>
public sealed record CardBulkResultDto(
    bool Applied,
    IReadOnlyList<CardPlacementResultDto> Results,
    int OccurrenceRows,
    IReadOnlyList<ConflictDto> Rejections);

/// <summary>Generatsiya so'rovi.</summary>
public sealed record BoardGenerationRequest(
    int? ScheduleId,
    int? Seed,
    string? Complexity,
    int? TimeLimitSeconds,
    bool? SavePartial,
    bool? AllowRelaxation,
    bool? KeepLocked);

/// <summary>Generatsiya jarayonining holati.</summary>
public sealed record BoardGenerationStatusDto(
    string JobId,
    string State,
    string Phase,
    double Percent,
    int PlacedCards,
    int TotalCards,
    long SoftCost,
    double ElapsedSeconds,
    string? Error,
    BoardGenerationReportDto? Report);

/// <summary>Generatsiya hisoboti.</summary>
public sealed record BoardGenerationReportDto(
    bool Success,
    bool Applied,
    bool Cancelled,
    int ScheduleId,
    int PlacedCards,
    int TotalCards,
    int UnplacedCards,
    int OccurrenceRows,
    long SoftCost,
    IReadOnlyList<string> HardViolations,
    IReadOnlyList<string> VerificationFaults,
    IReadOnlyList<string> RelaxationSuggestions,
    IReadOnlyList<ConflictDto> Conflicts,
    IReadOnlyList<string> Messages,
    IReadOnlyList<UnplacedLessonDto> UnplacedLessons,
    double ElapsedSeconds);

/// <summary>Chop etish dizayni.</summary>
public sealed record PrintDesignDto(string Key, string Name, string Scope);

/// <summary>Card modelining DTO o'girmalari.</summary>
public static class BoardMapper
{
    /// <summary>Kartochka → DTO.</summary>
    public static CardDto ToDto(this CardView c, int weeksInCycle = 1)
    {
        var printMask = CardPrintableAdapter.ToPrintWeeksMask(c.WeeksMask, weeksInCycle);
        var label = printMask == PrintableCard.AllWeeks
            ? null
            : (printMask & PrintableCard.WeekA) != 0 ? "A"
            : (printMask & PrintableCard.WeekB) != 0 ? "B" : null;

        return new CardDto(
            c.CardId, c.ScheduleId, c.LessonId,
            c.SubjectId, c.SubjectName,
            c.TeacherIds, c.TeacherNames,
            c.SchoolClassIds, c.ClassName,
            c.StudentGroupIds, c.GroupName,
            c.DayNo, c.PeriodId, c.PeriodNo, c.PeriodNumbers.ToList(),
            Math.Max(1, c.Length), c.IsDouble,
            c.WeeksMask, label,
            c.IsLocked, c.RoomNumber);
    }

    /// <summary>Joylashtirilmagan dars → DTO.</summary>
    public static UnplacedLessonDto ToDto(this UnplacedLessonView u) => new(
        u.LessonId, u.SubjectId, u.SubjectName, u.ClassName, u.GroupName,
        u.TeacherIds, u.TeacherNames,
        u.PeriodsPerWeek, u.PlacedPeriods, u.RemainingPeriods, u.PeriodsPerCard);

    /// <summary>Ko'chirish natijasi → DTO.</summary>
    public static CardBulkResultDto ToDto(this CardBulkResult r) => new(
        r.Applied,
        r.Results.Select(x => new CardPlacementResultDto(
            x.CardId, x.Placed, x.Conflicts.Select(ToDto).ToList())).ToList(),
        r.OccurrenceRows,
        r.Rejections.Select(ToDto).ToList());

    /// <summary>Konflikt → DTO (yagona ta'rif <see cref="Mapper"/> da).</summary>
    private static ConflictDto ToDto(Conflict c) => Mapper.ToDto(c);

    /// <summary>Generatsiya hisoboti → DTO.</summary>
    public static BoardGenerationReportDto ToDto(this ScheduleGenerationReport r) => new(
        r.Success, r.Applied, r.Cancelled, r.ScheduleId,
        r.PlacedCards, r.TotalCards, r.UnplacedCards, r.OccurrenceRows, r.SoftCost,
        r.HardViolations, r.VerificationFaults, r.RelaxationSuggestions,
        r.Conflicts.Select(ToDto).ToList(),
        r.Messages,
        r.UnplacedLessons.Select(ToDto).ToList(),
        r.Elapsed.TotalSeconds);
}
