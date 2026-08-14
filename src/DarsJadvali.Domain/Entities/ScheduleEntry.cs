using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>Jadvaldagi bitta dars yozuvi.</summary>
public class ScheduleEntry : BaseEntity
{
    /// <summary>Yozuv tegishli bo'lgan dars jadvali (varianti) Id — majburiy.</summary>
    public int ScheduleId { get; set; }

    /// <summary>Dars jadvali (varianti).</summary>
    public Schedule? Schedule { get; set; }

    /// <summary>Sinf Id.</summary>
    public int ClassGroupId { get; set; }

    /// <summary>Sinf.</summary>
    public ClassGroup? ClassGroup { get; set; }

    /// <summary>Fan Id.</summary>
    public int SubjectId { get; set; }

    /// <summary>Fan.</summary>
    public Subject? Subject { get; set; }

    /// <summary>O'qituvchi Id.</summary>
    public int TeacherId { get; set; }

    /// <summary>O'qituvchi.</summary>
    public Teacher? Teacher { get; set; }

    /// <summary>Hafta kuni.</summary>
    public WeekDay DayOfWeek { get; set; }

    /// <summary>Dars soati raqami (1..N).</summary>
    public int LessonNumber { get; set; }

    /// <summary>Xona raqami.</summary>
    public string? RoomNumber { get; set; }
}
