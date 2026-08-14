using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// O'quv yili, masalan "2025–2026". Eski o'quv yillari saqlanib qoladi,
/// har bir yil ichida bir nechta dars jadvali (variant) bo'lishi mumkin.
/// </summary>
public class AcademicYear : BaseEntity
{
    /// <summary>O'quv yili nomi, masalan "2025–2026" (unikal).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Boshlanish yili, masalan 2025 — tartiblash uchun.</summary>
    public int StartYear { get; set; }

    /// <summary>Ixtiyoriy izoh.</summary>
    public string? Note { get; set; }

    /// <summary>Shu o'quv yiliga tegishli dars jadvallari.</summary>
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
