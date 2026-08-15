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

    // ---------------------------------------------------------------------
    // Sxema v2 kengaytmalari
    // ---------------------------------------------------------------------

    /// <summary>Ixtiyoriy izoh.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Shu jadval varianti qaysi chorak uchun. <c>null</c> — chorakka bog'lanmagan
    /// (eski yozuvlar va "butun yil" variantlari).
    /// </summary>
    /// <remarks>
    /// Tasdiqlangan qaror: <b>chorak = alohida jadval varianti</b>. Har chorak uchun
    /// o'z <see cref="Schedule"/> yozuvi bo'ladi, <c>TermsMask</c> ISHLATILMAYDI.
    /// </remarks>
    public int? TermId { get; set; }

    /// <summary>Chorak.</summary>
    public Term? Term { get; set; }

    /// <summary>
    /// Shu variant qaysi jadvaldan nusxa olingan — chorakni oldingisidan nusxa olib
    /// boshlash uchun. Nusxa olish tarixi shu ustunda saqlanadi.
    /// </summary>
    public int? CopiedFromScheduleId { get; set; }

    /// <summary>Nusxa olingan manba jadval.</summary>
    public Schedule? CopiedFromSchedule { get; set; }

    /// <summary>
    /// Shu variantdagi hafta sikli: <c>1</c> — har hafta bir xil, <c>2</c> — juft/toq hafta.
    /// <c>Card.WeeksMask</c> bitlari shu songa nisbatan talqin qilinadi.
    /// </summary>
    public int WeeksInCycle { get; set; } = 1;

    /// <summary>Shu jadvalga tegishli dars yozuvlari (eski model).</summary>
    public ICollection<ScheduleEntry> Entries { get; set; } = new List<ScheduleEntry>();
}
