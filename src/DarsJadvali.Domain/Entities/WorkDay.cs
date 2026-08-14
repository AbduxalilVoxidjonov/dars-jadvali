using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>Ish kuni sozlamasi.</summary>
public class WorkDay : BaseEntity
{
    /// <summary>Hafta kuni.</summary>
    public WeekDay DayOfWeek { get; set; }

    /// <summary>Bu kun ish kunimi.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Kunlik maksimal dars soati.</summary>
    public int MaxLessonsPerDay { get; set; } = 7;
}
