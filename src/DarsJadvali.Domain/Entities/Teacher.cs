using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>O'qituvchi.</summary>
public class Teacher : BaseEntity
{
    /// <summary>To'liq ism-sharifi.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Telefon raqami.</summary>
    public string? Phone { get; set; }

    /// <summary>Jadvalda ko'rsatiladigan rang (HEX).</summary>
    public string ColorCode { get; set; } = "#1976D2";

    /// <summary>Faol yoki faol emas.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Biriktirmalar.</summary>
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();

    /// <summary>Ish vaqti oraliqlari.</summary>
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();

    /// <summary>Jadval yozuvlari.</summary>
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}
