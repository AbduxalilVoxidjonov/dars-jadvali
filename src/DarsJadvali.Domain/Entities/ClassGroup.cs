using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>Sinf (guruh).</summary>
public class ClassGroup : BaseEntity
{
    /// <summary>Sinf nomi, masalan "5-A".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Asosiy xona raqami.</summary>
    public string? RoomNumber { get; set; }

    /// <summary>O'quvchilar soni.</summary>
    public int StudentCount { get; set; }

    /// <summary>Biriktirmalar.</summary>
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();

    /// <summary>Jadval yozuvlari.</summary>
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}
