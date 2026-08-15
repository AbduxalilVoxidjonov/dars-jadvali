using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Denormallashgan BANDLIK qatori — sxemaning eng muhim mexanizmi.
/// </summary>
/// <remarks>
/// <para><b>Nima uchun kerak.</b> <see cref="Card"/> bitta qator bo'lsa ham bir nechta
/// slotni egallaydi: <c>PeriodsPerCard</c> (juft dars) va <c>WeeksMask</c> (A/B hafta)
/// tufayli. Bunday holatni unikal indeks bilan to'g'ridan-to'g'ri ushlab bo'lmaydi.
/// Yechim — kartezian ko'paytmasiga yoyish:</para>
/// <code>
/// CardOccurrence = Card
///   × { PeriodNo : Period.PeriodNo .. +Lesson.PeriodsPerCard-1 }
///   × { WeekNo   : Card.WeeksMask dagi yoqilgan bitlar }
///   × { (ResourceKind, ResourceId) : LessonTeacher ∪ yoyilgan LessonGroup ∪ CardClassroom }
/// </code>
///
/// <para><b>Unikal indeks:</b>
/// <c>(ScheduleId, ResourceKind, ResourceId, DayNo, PeriodNo, WeekNo)</c>.
/// Spetsifikatsiyadagi <c>TermNo</c> ustuni QURILMADI — chunki tasdiqlangan qaror bo'yicha
/// chorak alohida <see cref="Schedule"/> varianti, ya'ni chorak allaqachon
/// <c>ScheduleId</c> ichida. <c>TermNo</c> qo'shilsa indeks kengayib, hech qanday
/// qo'shimcha kafolat bermasdi.</para>
///
/// <para><b>Smena.</b> <c>PeriodNo</c> o'quv yili ichida smenalar bo'ylab uzluksiz
/// raqamlangani uchun (1-smena 1..6, 2-smena 7..12) bitta o'qituvchining ikki smenadagi
/// darslari ham shu indeks bilan tekshiriladi — alohida mexanizm kerak emas.</para>
///
/// <para><b>Guruh aniqligi.</b> Bandlik guruh darajasida yoziladi, sinf darajasida emas.
/// Shu tufayli bir sinfning ikki guruhi bir vaqtda dars o'ta oladi (7a va 7b stsenariylari),
/// lekin "butun sinf" darsi sinfning BARCHA guruhlariga qator yozgani uchun
/// "butun sinf + guruh" bir slotda DB darajasida rad etiladi.</para>
///
/// <para><b>Hosila jadval.</b> Qo'lda tahrirlanmaydi. Yagona egasi —
/// <c>ICardOccurrenceProjector</c>.</para>
/// </remarks>
public class CardOccurrence
{
    /// <summary>Yagona identifikator. <c>long</c> — bandlik qatorlari eng ko'p bo'ladi.</summary>
    public long Id { get; set; }

    /// <summary>Jadval varianti Id (chorak shu yerda).</summary>
    public int ScheduleId { get; set; }

    /// <summary>Jadval varianti.</summary>
    public Schedule? Schedule { get; set; }

    /// <summary>Manba kartochka Id.</summary>
    public int CardId { get; set; }

    /// <summary>Manba kartochka.</summary>
    public Card? Card { get; set; }

    /// <summary>Kun raqami, 0-based.</summary>
    public int DayNo { get; set; }

    /// <summary>Dars soati raqami — juft dars bo'yicha yoyilgan (smenalar bo'ylab global).</summary>
    public int PeriodNo { get; set; }

    /// <summary>Hafta indeksi sikl ichida (0-based): <c>0</c> = toq hafta, <c>1</c> = juft.</summary>
    public int WeekNo { get; set; }

    /// <summary>Band qilinayotgan resurs turi.</summary>
    public ResourceKind ResourceKind { get; set; }

    /// <summary>Band qilinayotgan resurs Id (turi <see cref="ResourceKind"/> ga qarab).</summary>
    public int ResourceId { get; set; }
}
