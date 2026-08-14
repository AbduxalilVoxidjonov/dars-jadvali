using System.Globalization;
using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Web.Dtos;

/// <summary>Entity ↔ DTO o'girish. Navigatsiya sikllarini JSON ga chiqarmaslik uchun.</summary>
public static class Mapper
{
    // ---------- TimeSpan yordamchilari ("HH:mm") ----------

    public static string ToHhMm(this TimeSpan value) =>
        $"{(int)value.TotalHours:D2}:{value.Minutes:D2}";

    public static TimeSpan ParseTime(string? value, TimeSpan fallback = default)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim();
        if (TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out var exact))
            return exact;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var loose))
            return loose;
        return fallback;
    }

    // ---------- Teacher ----------

    public static TeacherDto ToDto(this Teacher e) =>
        new(e.Id, e.FullName, e.Phone, e.ColorCode, e.IsActive);

    public static Teacher ToEntity(this TeacherDto d) => new()
    {
        Id = d.Id,
        FullName = d.FullName ?? string.Empty,
        Phone = d.Phone,
        ColorCode = string.IsNullOrWhiteSpace(d.ColorCode) ? "#1976D2" : d.ColorCode,
        IsActive = d.IsActive
    };

    // ---------- Subject ----------

    public static SubjectDto ToDto(this Subject e) =>
        new(e.Id, e.Name, e.Code, e.ColorCode);

    public static Subject ToEntity(this SubjectDto d) => new()
    {
        Id = d.Id,
        Name = d.Name ?? string.Empty,
        Code = d.Code ?? string.Empty,
        ColorCode = string.IsNullOrWhiteSpace(d.ColorCode) ? "#455A64" : d.ColorCode
    };

    // ---------- ClassGroup ----------

    public static ClassGroupDto ToDto(this ClassGroup e) =>
        new(e.Id, e.Name, e.RoomNumber, e.StudentCount);

    public static ClassGroup ToEntity(this ClassGroupDto d) => new()
    {
        Id = d.Id,
        Name = d.Name ?? string.Empty,
        RoomNumber = d.RoomNumber,
        StudentCount = d.StudentCount
    };

    // ---------- TeacherAssignment ----------

    public static AssignmentDto ToDto(this TeacherAssignment e) => new(
        e.Id,
        e.TeacherId, e.Teacher?.FullName, e.Teacher?.ColorCode,
        e.SubjectId, e.Subject?.Name, e.Subject?.ColorCode,
        e.ClassGroupId, e.ClassGroup?.Name,
        e.WeeklyHoursCount);

    public static TeacherAssignment ToEntity(this AssignmentDto d) => new()
    {
        Id = d.Id,
        TeacherId = d.TeacherId,
        SubjectId = d.SubjectId,
        ClassGroupId = d.ClassGroupId,
        WeeklyHoursCount = d.WeeklyHoursCount
    };

    // ---------- WorkDay ----------

    public static WorkDayDto ToDto(this WorkDay e) =>
        new(e.Id, e.DayOfWeek, e.DayOfWeek.ToUzbek(), e.IsActive, e.MaxLessonsPerDay);

    public static WorkDay ToEntity(this WorkDayDto d) => new()
    {
        Id = d.Id,
        DayOfWeek = d.DayOfWeek,
        IsActive = d.IsActive,
        MaxLessonsPerDay = d.MaxLessonsPerDay
    };

    // ---------- LessonSlot ----------

    public static LessonSlotDto ToDto(this LessonSlot e) =>
        new(e.Id, e.LessonNumber, e.StartTime.ToHhMm(), e.EndTime.ToHhMm());

    public static LessonSlot ToEntity(this LessonSlotDto d) => new()
    {
        Id = d.Id,
        LessonNumber = d.LessonNumber,
        StartTime = ParseTime(d.StartTime),
        EndTime = ParseTime(d.EndTime)
    };

    // ---------- TeacherAvailability ----------

    public static AvailabilityDto ToDto(this TeacherAvailability e) =>
        new(e.Id, e.TeacherId, e.DayOfWeek, e.StartTime.ToHhMm(), e.EndTime.ToHhMm(), e.IsAvailable);

    public static TeacherAvailability ToEntity(this AvailabilityDto d, int teacherId) => new()
    {
        Id = d.Id,
        TeacherId = teacherId,
        DayOfWeek = d.DayOfWeek,
        StartTime = ParseTime(d.StartTime),
        EndTime = ParseTime(d.EndTime),
        IsAvailable = d.IsAvailable
    };

    // ---------- TeacherDayAvailability (dars soati bo'yicha) ----------

    public static LessonAvailabilityDto ToDto(this TeacherDayAvailability d) => new(
        d.Day,
        d.Day.ToUzbek(),
        d.HasRestriction,
        (d.AllowedLessonNumbers ?? Array.Empty<int>()).ToList());

    public static TeacherDayAvailability ToModel(this LessonAvailabilityDto d) => new(
        d.Day,
        d.HasRestriction,
        (d.AllowedLessonNumbers ?? Array.Empty<int>())
            .Where(n => n > 0)
            .Distinct()
            .OrderBy(n => n)
            .ToList());

    // ---------- ScheduleEntry ----------

    public static ScheduleEntryDto ToDto(this ScheduleEntry e) => new(
        e.Id,
        e.ClassGroupId, e.ClassGroup?.Name,
        e.SubjectId, e.Subject?.Name, e.Subject?.ColorCode,
        e.TeacherId, e.Teacher?.FullName, e.Teacher?.ColorCode,
        e.DayOfWeek, e.DayOfWeek.ToUzbek(),
        e.LessonNumber,
        e.RoomNumber);

    public static ScheduleEntryDraft ToDraft(this ScheduleDraftRequest r) =>
        new(r.Id, r.ClassGroupId, r.SubjectId, r.TeacherId, r.DayOfWeek, r.LessonNumber, r.RoomNumber);

    // ---------- AcademicYear / Schedule (jadval variantlari) ----------

    /// <summary>O'quv yili DTO si. <paramref name="scheduleCount"/> alohida hisoblanadi.</summary>
    public static AcademicYearDto ToDto(this AcademicYear e, int scheduleCount) =>
        new(e.Id, e.Name, e.StartYear, e.Note, scheduleCount);

    /// <summary>
    /// Dars jadvali DTO si. Yozuvlar soni va o'quv yili nomi tashqaridan beriladi —
    /// navigatsiya har doim ham yuklanmagan bo'ladi.
    /// </summary>
    public static ScheduleSetDto ToDto(this Schedule e, int entryCount, string? academicYearName = null) =>
        new(e.Id,
            e.AcademicYearId,
            academicYearName ?? e.AcademicYear?.Name,
            e.Name,
            e.IsActive,
            e.CreatedAt,
            entryCount);

    // ---------- Validatsiya ----------

    public static ConflictDto ToDto(this Conflict c) =>
        new(c.Severity.ToString(), c.Code, c.Message);

    public static ValidationResultDto ToDto(this ValidationResult v) => new(
        v.IsValid,
        v.HasWarnings,
        v.Conflicts.Select(ToDto).ToList(),
        v.ToDisplayText());

    public static PlacementResultDto ToDto(this PlacementResult p) =>
        new(p.Placed, p.Entry?.ToDto(), p.Validation.ToDto());

    // ---------- Generatsiya ----------

    public static GenerationResultDto ToDto(this GenerationResult r) => new(
        r.Success, r.PlacedCount, r.UnplacedCount, r.Messages.ToList(), r.Elapsed.TotalSeconds);

    public static GenerationOptions ToOptions(this GenerationOptionsRequest? r)
    {
        var options = new GenerationOptions();
        if (r is null) return options;
        return options with
        {
            ClearExisting = r.ClearExisting ?? options.ClearExisting,
            MaxIterations = r.MaxIterations ?? options.MaxIterations,
            PopulationSize = r.PopulationSize ?? options.PopulationSize,
            MutationRate = r.MutationRate ?? options.MutationRate,
            RandomSeed = r.RandomSeed
        };
    }
}
