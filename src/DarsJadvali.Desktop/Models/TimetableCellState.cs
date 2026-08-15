namespace DarsJadvali.Desktop.Models;

/// <summary>
/// Jadval katagining <b>semantik</b> holati.
/// </summary>
/// <remarks>
/// ViewModel rang (<c>IBrush</c>) qaytarmaydi — faqat shu holatni beradi (M-06).
/// Rangni <c>Styles/AppStyles.axaml</c> dagi resurslar va
/// <c>Converters/</c> dagi konverterlar hal qiladi. Shu tufayli ViewModel'ni
/// Avalonia'siz test qilish va kelajakda qora mavzu qo'shish mumkin bo'ladi.
/// </remarks>
public enum TimetableCellState
{
    /// <summary>Sarlavha katagi (kun nomi, dars raqami yoki burchak).</summary>
    Header,

    /// <summary>Bo'sh katak — dars qo'yish mumkin.</summary>
    Empty,

    /// <summary>Band katak — dars qo'yilgan.</summary>
    Occupied,

    /// <summary>Ogohlantirish bor (masalan o'qituvchining noqulay vaqti).</summary>
    Warning,

    /// <summary>To'qnashuv bor (o'qituvchi yoki sinf band).</summary>
    Conflict,
}
