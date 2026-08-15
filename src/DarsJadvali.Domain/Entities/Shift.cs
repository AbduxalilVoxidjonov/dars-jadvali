using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Smena (1-smena, 2-smena).
/// </summary>
/// <remarks>
/// <b>Tasdiqlangan qaror:</b> maktabda ikki smena bor. Har <see cref="SchoolClass"/> o'z
/// smenasiga tegishli, har <see cref="Period"/> esa smenaga bog'lanadi.
/// <para>
/// <b>Muhim modellashtirish qarori:</b> <see cref="Period.PeriodNo"/> smenalar bo'ylab
/// <i>uzluksiz</i> (global) raqamlanadi — masalan 1-smena 1..6, 2-smena 7..12. Shu sababli
/// bitta o'qituvchi ikkala smenada ishlasa ham uning bandligi va "oyna" hisobi yaxlit
/// ko'riladi: <c>CardOccurrence</c> ning yagona unikal indeksi (u <c>PeriodNo</c> ni
/// o'z ichiga oladi) smenalararo to'qnashuvni ham avtomatik ushlaydi.
/// </para>
/// </remarks>
public class Shift : BaseEntity, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Smena raqami (1, 2, ...).</summary>
    public int ShiftNo { get; set; }

    /// <summary>Smena nomi, masalan "1-smena".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Qisqartma, masalan "I".</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Shu smenaga tegishli dars soatlari.</summary>
    public ICollection<Period> Periods { get; set; } = new List<Period>();

    /// <summary>Shu smenada o'qiydigan sinflar.</summary>
    public ICollection<SchoolClass> SchoolClasses { get; set; } = new List<SchoolClass>();
}
