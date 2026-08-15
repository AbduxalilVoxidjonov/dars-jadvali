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

    // ---------------------------------------------------------------------
    // Sxema v2 kengaytmalari — hammasi standart qiymatli, eski kod buzilmaydi.
    // ---------------------------------------------------------------------

    /// <summary>Haftadagi ish kunlari soni (odatda 6: dushanba–shanba).</summary>
    public int DaysPerWeek { get; set; } = 6;

    /// <summary>
    /// Hafta siklidagi haftalar soni: <c>1</c> — har hafta bir xil,
    /// <c>2</c> — juft/toq (A/B) hafta.
    /// </summary>
    public int WeeksInCycle { get; set; } = 1;

    /// <summary>Choraklar soni (odatda 4).</summary>
    public int TermsCount { get; set; } = 4;

    /// <summary>O'quv yili boshlanish sanasi.</summary>
    public DateOnly? StartsOn { get; set; }

    /// <summary>O'quv yili tugash sanasi.</summary>
    public DateOnly? EndsOn { get; set; }

    /// <summary>Shu o'quv yiliga tegishli dars jadvallari.</summary>
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    /// <summary>Shu o'quv yilining choraklari.</summary>
    public ICollection<Term> Terms { get; set; } = new List<Term>();

    /// <summary>Shu o'quv yilining smenalari.</summary>
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    /// <summary>Shu o'quv yilining dars soatlari (qo'ng'iroq jadvali).</summary>
    public ICollection<Period> Periods { get; set; } = new List<Period>();
}
