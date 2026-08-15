using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// O'quvchilar guruhi — dars <b>kimga</b> o'tilishining eng mayda birligi.
/// </summary>
/// <remarks>
/// Real masshtab: 30 sinf × 5 guruh = 150 guruh. Bu ixtiyoriy qo'shimcha emas, P0 talab.
/// <para>
/// <see cref="IsEntireClass"/> = <c>true</c> guruh ("Butun sinf") hech qanday boshqa guruh
/// bilan parallel bo'la olmaydi. Bu DB darajasida shunday kafolatlanadi:
/// <c>CardOccurrenceProjector</c> butun sinf darsi uchun sinfning <b>barcha</b> guruhlariga
/// bandlik qatori yozadi, shuning uchun "butun sinf + 1-guruh" bir slotda unikal indeksni
/// buzadi.
/// </para>
/// </remarks>
public class StudentGroup : BaseEntity, ISoftDeletable, IConcurrencyAware
{
    /// <summary>Sinf Id (denormallashgan — <c>ClassDivision.SchoolClassId</c> bilan bir xil).</summary>
    public int SchoolClassId { get; set; }

    /// <summary>Sinf.</summary>
    public SchoolClass? SchoolClass { get; set; }

    /// <summary>Bo'linish Id.</summary>
    public int ClassDivisionId { get; set; }

    /// <summary>Bo'linish.</summary>
    public ClassDivision? ClassDivision { get; set; }

    /// <summary>Guruh nomi, masalan "1-guruh" yoki "Butun sinf".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bu guruh butun sinfmi (har sinfda AYNAN BITTA bo'ladi).</summary>
    public bool IsEntireClass { get; set; }

    /// <summary>Guruhdagi o'quvchilar soni.</summary>
    public int? StudentCount { get; set; }

    /// <summary>Tashqi tizim identifikatori.</summary>
    public string? ExternalId { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }
}
