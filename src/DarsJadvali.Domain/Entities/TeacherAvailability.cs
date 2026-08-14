using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>O'qituvchining kun bo'yicha ish vaqti oralig'i.</summary>
public class TeacherAvailability : BaseEntity
{
    /// <summary>O'qituvchi Id.</summary>
    public int TeacherId { get; set; }

    /// <summary>O'qituvchi.</summary>
    public Teacher? Teacher { get; set; }

    /// <summary>Hafta kuni.</summary>
    public WeekDay DayOfWeek { get; set; }

    /// <summary>Oraliq boshlanish vaqti.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Oraliq tugash vaqti.</summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>Bu oraliqda ishlaydimi (false — band/ishlamaydi).</summary>
    public bool IsAvailable { get; set; } = true;
}
