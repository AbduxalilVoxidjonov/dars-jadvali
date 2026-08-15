using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Dars soati (qo'ng'iroq jadvali qatori). Eski <see cref="LessonSlot"/> ning v2 varianti,
/// lekin har o'quv yili va smenaga bog'langan.
/// </summary>
/// <remarks>
/// <see cref="PeriodNo"/> — o'quv yili ichida <b>global</b> raqam: 1-smena 1..6,
/// 2-smena 7..12. Bu ataylab: <c>CardOccurrence</c> bandligi shu raqam ustida qurilgani
/// uchun o'qituvchining ikki smenadagi darslari bitta o'lchovda tekshiriladi.
/// </remarks>
public class Period : BaseEntity, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Smena Id (ixtiyoriy — bir smenali maktabda <c>null</c>).</summary>
    public int? ShiftId { get; set; }

    /// <summary>Smena.</summary>
    public Shift? Shift { get; set; }

    /// <summary>
    /// Dars soati raqami — o'quv yili ichida unikal va smenalar bo'ylab uzluksiz.
    /// <c>0</c> = "nolinchi soat".
    /// </summary>
    public int PeriodNo { get; set; }

    /// <summary>Boshlanish vaqti.</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>Tugash vaqti.</summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>Ko'rsatiladigan nomi, masalan "1-dars".</summary>
    public string? Name { get; set; }

    /// <summary>Qisqartma, masalan "1".</summary>
    public string? ShortName { get; set; }

    /// <summary>Bu qator tanaffusmi (unga dars qo'yilmaydi).</summary>
    public bool IsBreak { get; set; }
}
