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

    // ---------------------------------------------------------------------
    // Sxema v2 kengaytmalari
    // ---------------------------------------------------------------------

    /// <summary>
    /// O'quv yili Id. <c>null</c> — eski, yilga bog'lanmagan global yozuv.
    /// (Eski model bo'yicha ish kunlari global edi — P0-6.)
    /// </summary>
    public int? AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>
    /// Kun raqami, 0-based (dushanba = 0). <see cref="DayOfWeek"/> dan hosila, lekin
    /// DB va generator faqat shu ustunni ishlatadi — <c>DayNumbering</c> ga qarang.
    /// </summary>
    public int DayNo { get; set; }

    /// <summary>Kun nomi, masalan "Dushanba".</summary>
    public string? Name { get; set; }

    /// <summary>Qisqartma, masalan "Du".</summary>
    public string? ShortName { get; set; }

    /// <summary>Kunlik minimal dars soati (0 = cheklov yo'q).</summary>
    public int MinLessonsPerDay { get; set; }
}
