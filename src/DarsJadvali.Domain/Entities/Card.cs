using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Kartochka — darsning JOYLASHTIRILISHI: qaysi kun, qaysi soatdan boshlab, qaysi haftalarda.
/// </summary>
/// <remarks>
/// Chorak o'lchovi bu yerda YO'Q va bu ataylab: tasdiqlangan qaror bo'yicha
/// <b>chorak = alohida jadval varianti</b>, ya'ni chorak <see cref="ScheduleId"/> ichida.
/// Shu sababli spetsifikatsiyadagi <c>Card.TermsMask</c> ustuni qurilmadi.
/// </remarks>
public class Card : BaseEntity, IConcurrencyAware
{
    /// <summary>Jadval varianti Id (chorak shu yerda).</summary>
    public int ScheduleId { get; set; }

    /// <summary>Jadval varianti.</summary>
    public Schedule? Schedule { get; set; }

    /// <summary>Dars ta'rifi Id — reja ↔ fakt bog'lanishi (eng muhim yangi FK).</summary>
    public int LessonId { get; set; }

    /// <summary>Dars ta'rifi.</summary>
    public Lesson? Lesson { get; set; }

    /// <summary>Boshlanish dars soati Id. Kartochka <see cref="Length"/> ta soatni egallaydi.</summary>
    public int PeriodId { get; set; }

    /// <summary>Boshlanish dars soati.</summary>
    public Period? Period { get; set; }

    /// <summary>Kun raqami, 0-based (dushanba = 0).</summary>
    public int DayNo { get; set; }

    /// <summary>
    /// Kartochka egallaydigan ketma-ket dars soatlari soni: <c>1</c> — oddiy dars,
    /// <c>2</c> — juft dars, <c>3</c> — uchlik.
    /// </summary>
    /// <remarks>
    /// <b>Nega bu ustun kerak</b> (ilgari uzunlik <see cref="Entities.Lesson.PeriodsPerCard"/>
    /// dan olinardi): haftalik soat juft dars uzunligiga bo'linmasa
    /// (<c>PeriodsPerWeek % PeriodsPerCard != 0</c>, masalan "haftasiga 5 soat = 2 + 2 + 1"),
    /// bitta darsning kartochkalari <b>turli uzunlikda</b> bo'ladi. Uzunlik darsda
    /// saqlanganda bunday taqsimotni umuman ifodalab bo'lmasdi va mapper hammasini
    /// yakka soatga tushirib yuborardi.
    /// <para>
    /// <see cref="Entities.Lesson.PeriodsPerCard"/> — bu <b>istak</b> (reja), shu ustun esa
    /// <b>fakt</b>. Qoldiq kartochka faqat shu yerda 1 bo'lib ko'rinadi.
    /// </para>
    /// </remarks>
    public int Length { get; set; } = 1;

    /// <summary>
    /// Kartochka qaysi haftalarda turadi (A/B hafta): <c>0b01</c> = faqat toq hafta,
    /// <c>0b10</c> = faqat juft, <c>0b11</c> = har ikkalasi.
    /// </summary>
    public int WeeksMask { get; set; } = 1;

    /// <summary>Qulflangan kartochkani generator ko'chira olmaydi.</summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Eski <c>ScheduleEntry.RoomNumber</c> matni. Xona moduli (P1) to'liq yoqilgach
    /// <see cref="CardClassroom"/> ga ko'chiriladi va bu ustun o'chadi.
    /// </summary>
    public string? LegacyRoomNumber { get; set; }

    /// <summary>Eski <see cref="ScheduleEntry"/> Id — ma'lumot ko'chirish izi.</summary>
    public int? LegacyScheduleEntryId { get; set; }

    /// <summary>Shu kartochkaga tayinlangan xonalar (P1).</summary>
    public ICollection<CardClassroom> Classrooms { get; set; } = new List<CardClassroom>();

    /// <summary>Shu kartochkadan hosil bo'lgan bandlik qatorlari.</summary>
    public ICollection<CardOccurrence> Occurrences { get; set; } = new List<CardOccurrence>();
}
