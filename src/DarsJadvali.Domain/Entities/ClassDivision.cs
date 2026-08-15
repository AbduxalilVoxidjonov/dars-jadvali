using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Sinfning bo'linish usuli (aSc <c>groups.divisiontag</c> ning normalizatsiyasi).
/// </summary>
/// <remarks>
/// Asosiy qoida (aSc #1895): <b>bir vaqtda dars o'tishi mumkin bo'lgan guruhlar — faqat
/// bitta <see cref="ClassDivision"/> ichidagi turli guruhlar.</b> Ya'ni "1-guruh" va
/// "o'g'illar" bir vaqtda dars o'ta olmaydi, chunki ular turli bo'linishlarga tegishli
/// va o'quvchilari kesishadi.
/// <para>
/// aSc'dagi standart 5 bo'linish har sinf uchun avtomatik yaratiladi:
/// <c>tag=0</c> butun sinf, <c>tag=1</c> 1/2 guruh, <c>tag=2</c> o'g'il/qiz.
/// </para>
/// </remarks>
public class ClassDivision : BaseEntity, IConcurrencyAware
{
    /// <summary>Sinf Id.</summary>
    public int SchoolClassId { get; set; }

    /// <summary>Sinf.</summary>
    public SchoolClass? SchoolClass { get; set; }

    /// <summary>Bo'linish tegi: <c>0</c> = butun sinf, <c>1</c> = 1/2 guruh, <c>2</c> = o'g'il/qiz.</summary>
    public int DivisionTag { get; set; }

    /// <summary>Bo'linish nomi, masalan "Guruhlar".</summary>
    public string? Name { get; set; }

    /// <summary>Shu bo'linishdagi guruhlar.</summary>
    public ICollection<StudentGroup> StudentGroups { get; set; } = new List<StudentGroup>();
}
