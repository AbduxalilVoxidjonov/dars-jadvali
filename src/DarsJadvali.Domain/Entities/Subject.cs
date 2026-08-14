using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>Fan.</summary>
public class Subject : BaseEntity
{
    /// <summary>Fan nomi.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Qisqa kodi.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Jadvalda ko'rsatiladigan rang (HEX).</summary>
    public string ColorCode { get; set; } = "#455A64";

    /// <summary>Biriktirmalar.</summary>
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();

    /// <summary>Jadval yozuvlari.</summary>
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}
