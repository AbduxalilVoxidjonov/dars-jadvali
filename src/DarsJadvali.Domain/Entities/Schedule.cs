using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Bitta dars jadvali (varianti), masalan "Asosiy jadval" yoki "2-variant".
/// Butun bazada faqat bitta jadval faol bo'ladi — u dastur qayta ochilganda ham esda qoladi.
/// </summary>
public class Schedule : BaseEntity
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Jadval nomi (bitta o'quv yili ichida unikal).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Shu jadval faolmi (butun bazada faqat bittasi faol bo'ladi).</summary>
    public bool IsActive { get; set; }

    /// <summary>Yaratilgan vaqti (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Shu jadvalga tegishli dars yozuvlari.</summary>
    public ICollection<ScheduleEntry> Entries { get; set; } = new List<ScheduleEntry>();
}
