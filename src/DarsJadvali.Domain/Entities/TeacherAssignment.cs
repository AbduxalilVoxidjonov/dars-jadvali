using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>O'qituvchi–fan–sinf biriktirmasi.</summary>
public class TeacherAssignment : BaseEntity
{
    /// <summary>O'qituvchi Id.</summary>
    public int TeacherId { get; set; }

    /// <summary>O'qituvchi.</summary>
    public Teacher? Teacher { get; set; }

    /// <summary>Fan Id.</summary>
    public int SubjectId { get; set; }

    /// <summary>Fan.</summary>
    public Subject? Subject { get; set; }

    /// <summary>Sinf Id.</summary>
    public int ClassGroupId { get; set; }

    /// <summary>Sinf.</summary>
    public ClassGroup? ClassGroup { get; set; }

    /// <summary>Haftalik soatlar soni.</summary>
    public int WeeklyHoursCount { get; set; }
}
