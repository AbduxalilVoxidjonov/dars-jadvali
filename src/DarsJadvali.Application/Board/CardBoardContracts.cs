using DarsJadvali.Application.Validation;

namespace DarsJadvali.Application.Board;

/// <summary>
/// Jadval to'ridagi BITTA kartochkaning to'liq o'qish modeli (<c>Card</c> + <c>Lesson</c>).
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> Desktop hozir <c>ScheduleEntryCardAdapter</c> orqali eski
/// <c>ScheduleEntry</c> dan karta yasaydi va juft dars (<c>Length</c>), hafta maskasi
/// (<c>WeeksMask</c>), qulflash (<c>IsLocked</c>) hamda guruh bo'linmasi
/// (<c>GroupName</c>) uchun <b>standart qiymat</b> qo'yishga majbur — eski modelda bu
/// maydonlar umuman yo'q. Shu DTO o'sha to'rt maydonni ham, fan/o'qituvchi/sinf
/// nomlarini ham HAQIQIY manbadan beradi.
/// </remarks>
/// <param name="CardId">Kartochka Id (<c>Card.Id</c>).</param>
/// <param name="ScheduleId">Jadval varianti.</param>
/// <param name="LessonId">Dars ta'rifi Id (reja ↔ fakt bog'lanishi).</param>
/// <param name="SubjectId">Fan Id.</param>
/// <param name="SubjectName">Fan nomi.</param>
/// <param name="TeacherIds">O'qituvchilar (birgalikda o'qitishda bir nechta).</param>
/// <param name="TeacherNames">O'qituvchilar FIO si — <paramref name="TeacherIds"/> bilan bir tartibda.</param>
/// <param name="SchoolClassIds">Sinflar (birlashtirilgan darsda bir nechta).</param>
/// <param name="ClassName">Sinf(lar) nomi, ko'rsatish uchun.</param>
/// <param name="StudentGroupIds">Guruhlar.</param>
/// <param name="GroupName">
/// Guruh bo'linmasi nomi. Butun sinf darsi bo'lsa <b>bo'sh satr</b> — kartada
/// ortiqcha yozuv ko'rinmasligi uchun.
/// </param>
/// <param name="DayNo">Kun raqami (0-based, dushanba = 0).</param>
/// <param name="PeriodId">Boshlanish dars soati Id.</param>
/// <param name="PeriodNo">Boshlanish dars soati raqami.</param>
/// <param name="Length">Egallangan ketma-ket soatlar soni (juft dars).</param>
/// <param name="WeeksMask">Qaysi haftalarda turadi (A/B hafta).</param>
/// <param name="IsLocked">Qulflangan kartochkani generator ham, drag ham qimirlata olmaydi.</param>
/// <param name="RoomNumber">Xona (P1 gacha — <c>Card.LegacyRoomNumber</c>).</param>
public sealed record CardView(
    int CardId,
    int ScheduleId,
    int LessonId,
    int SubjectId,
    string SubjectName,
    IReadOnlyList<int> TeacherIds,
    IReadOnlyList<string> TeacherNames,
    IReadOnlyList<int> SchoolClassIds,
    string ClassName,
    IReadOnlyList<int> StudentGroupIds,
    string GroupName,
    int DayNo,
    int PeriodId,
    int PeriodNo,
    int Length,
    int WeeksMask,
    bool IsLocked,
    string? RoomNumber)
{
    /// <summary>
    /// Kartochkaga TAYINLANGAN xonalar (<c>CardClassroom</c>). <c>V2_07</c> gacha bu
    /// ro'yxat har doim bo'sh edi va xona faqat <see cref="RoomNumber"/> matni sifatida
    /// mavjud bo'lgani uchun bandlik tekshiruviga UMUMAN tushmasdi.
    /// </summary>
    /// <remarks>
    /// Ataylab pozitsion parametr emas, <c>init</c> xossa: mavjud
    /// <c>new CardView(...)</c> chaqiruvlari (Desktop/Web) o'zgarishsiz qoladi.
    /// Xona ishlatilmaydigan maktabda ro'yxat bo'sh bo'ladi va hech narsa buzilmaydi.
    /// </remarks>
    public IReadOnlyList<int> ClassroomIds { get; init; } = Array.Empty<int>();

    /// <summary>Juft (yoki undan uzun) darsmi.</summary>
    public bool IsDouble => Length > 1;

    /// <summary>Kartochka egallaydigan soat raqamlari.</summary>
    public IEnumerable<int> PeriodNumbers => Enumerable.Range(PeriodNo, Math.Max(1, Length));
}

/// <summary>
/// Rejada bor, lekin to'liq joylashtirilmagan dars — "Joylashtirilmagan darslar" paneli uchun.
/// </summary>
/// <remarks>
/// Ilgari bunday ro'yxat umuman yo'q edi: generatsiya hisoboti faqat
/// <c>UnplacedCards</c> sonini berardi, panel esa "haftalik me'yor − qo'yilgan soat"
/// bilan TAXMIN qilardi. Bu yerda son <c>Lesson.PeriodsPerWeek</c> bilan
/// <c>SUM(Card.Length)</c> ayirmasidan — aniq manbadan — olinadi.
/// </remarks>
/// <param name="LessonId">Dars ta'rifi Id.</param>
/// <param name="SubjectId">Fan Id.</param>
/// <param name="SubjectName">Fan nomi.</param>
/// <param name="ClassName">Sinf(lar) nomi.</param>
/// <param name="GroupName">Guruh bo'linmasi (butun sinf bo'lsa bo'sh satr).</param>
/// <param name="TeacherIds">O'qituvchilar.</param>
/// <param name="TeacherNames">O'qituvchilar FIO si.</param>
/// <param name="PeriodsPerWeek">Haftalik me'yor (reja).</param>
/// <param name="PlacedPeriods">Allaqachon qo'yilgan soat (kartochkalar uzunliklari yig'indisi).</param>
/// <param name="PeriodsPerCard">Bitta kartochka uchun istalgan uzunlik (juft dars istagi).</param>
public sealed record UnplacedLessonView(
    int LessonId,
    int SubjectId,
    string SubjectName,
    string ClassName,
    string GroupName,
    IReadOnlyList<int> TeacherIds,
    IReadOnlyList<string> TeacherNames,
    int PeriodsPerWeek,
    int PlacedPeriods,
    int PeriodsPerCard)
{
    /// <summary>
    /// Darsga tegishli sinf(lar) Id si. Ilgari bu ro'yxat yo'q edi va prezentatsiya
    /// qatlami sinfni <see cref="ClassName"/> MATNI bo'yicha izlashga majbur edi —
    /// bir xil nomli yoki qayta nomlangan sinfda bu yo'l sinardi.
    /// </summary>
    /// <remarks>
    /// Ataylab pozitsion parametr emas, <c>init</c> xossa: mavjud
    /// <c>new UnplacedLessonView(...)</c> chaqiruvlari o'zgarishsiz qoladi.
    /// </remarks>
    public IReadOnlyList<int> SchoolClassIds { get; init; } = Array.Empty<int>();

    /// <summary>Darsga tegishli o'quvchi guruhlari Id si (butun sinf guruhi ham shu yerda).</summary>
    public IReadOnlyList<int> StudentGroupIds { get; init; } = Array.Empty<int>();

    /// <summary>Qolgan soat (manfiy bo'lmaydi).</summary>
    public int RemainingPeriods => Math.Max(0, PeriodsPerWeek - PlacedPeriods);
}

/// <summary>
/// Yangi kartochka yaratish so'rovi — <see cref="ICardBoardService.CreateCardAsync"/> uchun.
/// </summary>
/// <remarks>
/// Alohida record: xona (<see cref="ClassroomIds"/>, <see cref="RoomNumber"/>) va
/// keyingi maydonlar imzoni buzmasdan qo'shiladi.
/// </remarks>
/// <param name="LessonId">Qaysi dars ta'rifi uchun kartochka yaratiladi.</param>
/// <param name="DayNo">Kun raqami (0-based, dushanba = 0).</param>
/// <param name="PeriodId">Boshlanish dars soati Id.</param>
/// <param name="Length">Ketma-ket soatlar soni (juft dars uchun 2). Kamida 1.</param>
/// <param name="WeeksMask">Hafta maskasi (A/B hafta). 0 yoki manfiy bo'lsa 1 ga tenglashtiriladi.</param>
public sealed record CardCreateRequest(
    int LessonId,
    int DayNo,
    int PeriodId,
    int Length = 1,
    int WeeksMask = 1)
{
    /// <summary>Kartochkaga tayinlanadigan xona(lar). Bo'sh bo'lsa xona tayinlanmaydi.</summary>
    public IReadOnlyList<int> ClassroomIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Xona nomi MATN sifatida (<c>Card.LegacyRoomNumber</c>). Xonalar ma'lumotnomasi
    /// to'ldirilmagan maktabda foydalanuvchi xonani shu maydonga qo'lda kiritadi.
    /// </summary>
    public string? RoomNumber { get; init; }

    /// <summary>Qulflangan holda yaratilsinmi (generator qimirlatmaydi).</summary>
    public bool IsLocked { get; init; }
}

/// <summary>Kartochka yaratish natijasi.</summary>
/// <param name="Created">Yaratildimi.</param>
/// <param name="CardId">Yaratilgan kartochka Id (yaratilmagan bo'lsa 0).</param>
/// <param name="Conflicts">Rad etilgan bo'lsa — sabab(lar).</param>
/// <param name="OccurrenceRows">Qurilgan bandlik qatorlari soni.</param>
public sealed record CardCreateResult(
    bool Created,
    int CardId,
    IReadOnlyList<Conflict> Conflicts,
    int OccurrenceRows = 0);

/// <summary>Bitta kartochkani ko'chirish so'rovi.</summary>
/// <param name="CardId">Kartochka Id.</param>
/// <param name="DayNo">Yangi kun raqami (0-based).</param>
/// <param name="PeriodId">Yangi boshlanish dars soati Id.</param>
/// <param name="WeeksMask">
/// Yangi hafta maskasi. <c>null</c> — o'zgarmaydi (odatiy holat: drag faqat kun/soatni o'zgartiradi).
/// </param>
public sealed record CardPlacement(int CardId, int DayNo, int PeriodId, int? WeeksMask = null);

/// <summary>Bitta kartochka bo'yicha joylashtirish natijasi.</summary>
/// <param name="CardId">Kartochka Id.</param>
/// <param name="Placed">Qabul qilindimi.</param>
/// <param name="Conflicts">Rad etilgan bo'lsa — sabab(lar).</param>
public sealed record CardPlacementResult(int CardId, bool Placed, IReadOnlyList<Conflict> Conflicts);

/// <summary>Ommaviy kartochka joylashtirishning natijasi.</summary>
/// <param name="Applied">Tranzaksiya commit bo'ldimi.</param>
/// <param name="Results">Har bir so'rov natijasi — kirish bilan bir tartibda.</param>
/// <param name="OccurrenceRows">Qayta qurilgan bandlik qatorlari soni.</param>
public sealed record CardBulkResult(
    bool Applied,
    IReadOnlyList<CardPlacementResult> Results,
    int OccurrenceRows)
{
    /// <summary>Barcha rad etish sabablari.</summary>
    public IReadOnlyList<Conflict> Rejections =>
        Results.Where(r => !r.Placed).SelectMany(r => r.Conflicts).ToList();
}

/// <summary>
/// Bandlik qatorining o'qish ko'rinishi — Application darajasidagi to'qnashuv tekshiruvi uchun.
/// </summary>
/// <param name="CardId">Qaysi kartochkadan hosil bo'lgan.</param>
/// <param name="DayNo">Kun raqami.</param>
/// <param name="PeriodNo">Dars soati raqami.</param>
/// <param name="WeekNo">Hafta raqami (0-based).</param>
/// <param name="ResourceKind">Resurs turi (o'qituvchi / guruh / xona).</param>
/// <param name="ResourceId">Resurs Id.</param>
public readonly record struct CardOccupancy(
    int CardId,
    int DayNo,
    int PeriodNo,
    int WeekNo,
    Domain.Enums.ResourceKind ResourceKind,
    int ResourceId);
