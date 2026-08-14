using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Web.Dtos;

// ---------------------------------------------------------------------------
// Yassi (flat) DTO'lar. Entity navigatsiyalari sikl hosil qilgani uchun
// JSON ga to'g'ridan-to'g'ri entity berilmaydi — faqat shu DTO'lar.
// TimeSpan maydonlari "HH:mm" satri ko'rinishida uzatiladi.
// ---------------------------------------------------------------------------

public sealed record TeacherDto(
    int Id,
    string FullName,
    string? Phone,
    string ColorCode,
    bool IsActive);

public sealed record SubjectDto(
    int Id,
    string Name,
    string Code,
    string ColorCode);

public sealed record ClassGroupDto(
    int Id,
    string Name,
    string? RoomNumber,
    int StudentCount);

public sealed record AssignmentDto(
    int Id,
    int TeacherId,
    string? TeacherName,
    string? TeacherColor,
    int SubjectId,
    string? SubjectName,
    string? SubjectColor,
    int ClassGroupId,
    string? ClassGroupName,
    int WeeklyHoursCount);

public sealed record WorkDayDto(
    int Id,
    WeekDay DayOfWeek,
    string DayName,
    bool IsActive,
    int MaxLessonsPerDay);

public sealed record LessonSlotDto(
    int Id,
    int LessonNumber,
    string StartTime,
    string EndTime);

public sealed record AvailabilityDto(
    int Id,
    int TeacherId,
    WeekDay DayOfWeek,
    string StartTime,
    string EndTime,
    bool IsAvailable);

/// <summary>
/// Bir kun uchun o'qituvchining bandligi — DARS SOATI raqamlari bilan.
/// <c>hasRestriction == false</c> — o'sha kuni cheklov yo'q (barcha soatlarda ishlaydi).
/// <c>hasRestriction == true</c> — FAQAT <c>allowedLessonNumbers</c> dagi soatlarda ishlaydi.
/// </summary>
public sealed record LessonAvailabilityDto(
    WeekDay Day,
    string DayName,
    bool HasRestriction,
    IReadOnlyList<int> AllowedLessonNumbers);

public sealed record ScheduleEntryDto(
    int Id,
    int ClassGroupId,
    string? ClassGroupName,
    int SubjectId,
    string? SubjectName,
    string? SubjectColor,
    int TeacherId,
    string? TeacherName,
    string? TeacherColor,
    WeekDay DayOfWeek,
    string DayName,
    int LessonNumber,
    string? RoomNumber);

public sealed record ConflictDto(
    string Severity,
    string Code,
    string Message);

public sealed record ValidationResultDto(
    bool IsValid,
    bool HasWarnings,
    IReadOnlyList<ConflictDto> Conflicts,
    string DisplayText);

public sealed record PlacementResultDto(
    bool Placed,
    ScheduleEntryDto? Entry,
    ValidationResultDto Validation);

public sealed record HoursSummaryDto(int Weekly, int Placed, int Remaining);

public sealed record GenerationResultDto(
    bool Success,
    int PlacedCount,
    int UnplacedCount,
    IReadOnlyList<string> Messages,
    double ElapsedSeconds);

/// <summary>O'quv yili. <c>scheduleCount</c> — shu yil ichidagi dars jadvallari soni.</summary>
public sealed record AcademicYearDto(
    int Id,
    string Name,
    int StartYear,
    string? Note,
    int ScheduleCount);

/// <summary>
/// Dars jadvali (varianti). <c>entryCount</c> — jadvaldagi dars yozuvlari soni.
/// Dars yozuvining o'zi <see cref="ScheduleEntryDto"/> — chalkashtirmang.
/// </summary>
public sealed record ScheduleSetDto(
    int Id,
    int AcademicYearId,
    string? AcademicYearName,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    int EntryCount);

public sealed record AboutDto(
    string AppName,
    string Version,
    string Author,
    string Description,
    string TelegramUrl,
    string TelegramHandle,
    string DonateCardNumber,
    string DonateCardType,
    string DonateCardHolder,
    string DbPath);

// ---------------------------------------------------------------------------
// So'rov (request) modellari
// ---------------------------------------------------------------------------

public sealed record ScheduleDraftRequest(
    int? Id,
    int ClassGroupId,
    int SubjectId,
    int TeacherId,
    WeekDay DayOfWeek,
    int LessonNumber,
    string? RoomNumber);

public sealed record MoveRequest(
    int EntryId,
    WeekDay DayOfWeek,
    int LessonNumber,
    bool Force = false);

/// <summary>O'quv yili qo'shish / nomini o'zgartirish so'rovi.</summary>
public sealed record AcademicYearRequest(
    string? Name,
    int? StartYear,
    string? Note);

/// <summary>Dars jadvali qo'shish / nomini o'zgartirish so'rovi.</summary>
public sealed record ScheduleSetRequest(
    int? AcademicYearId,
    string? Name);

public sealed record GenerationOptionsRequest(
    bool? ClearExisting,
    int? MaxIterations,
    int? PopulationSize,
    double? MutationRate,
    int? RandomSeed);
