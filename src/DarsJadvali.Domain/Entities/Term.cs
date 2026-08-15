using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// O'quv choragi (I–IV).
/// </summary>
/// <remarks>
/// <b>Tasdiqlangan qaror:</b> chorak — <i>alohida jadval varianti</i>. Ya'ni har chorak uchun
/// o'z <see cref="Schedule"/> yozuvi bo'ladi (<see cref="Schedule.TermId"/>), va
/// <c>TermsMask</c> yondashuvi RAD ETILGAN. Shu sababli <c>Card</c> va
/// <c>CardOccurrence</c> da chorak o'lchovi umuman yo'q — u <c>ScheduleId</c> ichida.
/// </remarks>
public class Term : BaseEntity, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Chorak tartib raqami (1..N).</summary>
    public int Ordinal { get; set; }

    /// <summary>Chorak nomi, masalan "I chorak".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Qisqartma, masalan "I".</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Chorak boshlanish sanasi.</summary>
    public DateOnly? StartsOn { get; set; }

    /// <summary>Chorak tugash sanasi.</summary>
    public DateOnly? EndsOn { get; set; }

    /// <summary>Shu chorak uchun tuzilgan jadval variantlari.</summary>
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
