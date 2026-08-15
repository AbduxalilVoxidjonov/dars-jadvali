using DarsJadvali.Application.Board;
using DarsJadvali.Application.Scheduling;

namespace DarsJadvali.Application.Abstractions;

/// <summary>
/// Generatsiya uchun ma'lumot o'qish/yozishning yagona nuqtasi (EF detallari Infrastructure'da).
/// </summary>
/// <remarks>
/// <b>Tranzaksiya qoidasi:</b> bu servis hech qachon o'z tranzaksiyasini ochmaydi —
/// barcha metodlari chaqiruvchining (<c>IUnitOfWork.ExecuteInTransactionAsync</c>)
/// tranzaksiyasi ichida bajariladi (00 §6.4).
/// <para>
/// Barcha so'rovlar <b>bitta jadval varianti</b> va uning o'quv yili bilan cheklanadi
/// hamda <c>AsNoTracking</c> bilan bajariladi (05-audit K-07/K-19).
/// </para>
/// </remarks>
public interface ISchedulingStore
{
    /// <summary>Jadval varianti uchun barcha kirish ma'lumotini o'qiydi.</summary>
    /// <exception cref="SchedulingMappingException">Jadval yoki o'quv yili topilmasa.</exception>
    Task<SchedulingInput> LoadAsync(int scheduleId, CancellationToken ct = default);

    /// <summary>
    /// Jadval variantining kartochkalarini o'chiradi (bandlik qatorlari kaskad bilan ketadi).
    /// </summary>
    /// <param name="keepLocked">Qulflangan kartochkalar saqlanib qolsinmi.</param>
    /// <returns>O'chirilgan kartochkalar soni.</returns>
    Task<int> DeleteCardsAsync(int scheduleId, bool keepLocked, CancellationToken ct = default);

    /// <summary>
    /// Kartochkalarni yozadi va yaratilgan <c>Card.Id</c> larni <b>kirish tartibida</b> qaytaradi.
    /// </summary>
    Task<IReadOnlyList<int>> InsertCardsAsync(
        IReadOnlyList<CardWrite> cards, CancellationToken ct = default);

    /// <summary>
    /// Jadvaldagi joylashtirilgan kartochkalarni bo'linish tekshiruvi uchun o'qiydi
    /// (<c>GROUP_DIVISION_OVERLAP</c>).
    /// </summary>
    Task<IReadOnlyList<PlacedCardView>> LoadPlacedCardsAsync(
        int scheduleId, CancellationToken ct = default);

    // ---------------------------------------------------------------------
    // Jadval to'ri (UI) uchun o'qish/yozish — 00 §10.8, 1-band
    // ---------------------------------------------------------------------

    /// <summary>
    /// Jadvaldagi barcha kartochkalarni UI uchun to'liq ko'rinishda o'qiydi
    /// (uzunlik, hafta maskasi, qulf, guruh nomi, fan/o'qituvchi/sinf nomlari).
    /// </summary>
    Task<IReadOnlyList<CardView>> LoadCardViewsAsync(
        int scheduleId, CancellationToken ct = default);

    /// <summary>
    /// To'liq joylashtirilmagan darslar ro'yxati: reja (<c>Lesson.PeriodsPerWeek</c>) va
    /// fakt (<c>SUM(Card.Length)</c>) taqqoslanadi.
    /// </summary>
    Task<IReadOnlyList<UnplacedLessonView>> LoadUnplacedLessonsAsync(
        int scheduleId, CancellationToken ct = default);

    /// <summary>Jadvalning bandlik qatorlari (<c>CardOccurrence</c>) — xotiradagi tekshiruv uchun.</summary>
    Task<IReadOnlyList<CardOccupancy>> LoadOccupancyAsync(
        int scheduleId, CancellationToken ct = default);

    /// <summary>
    /// Kartochkani yangi kun/soatga ko'chiradi. Topilmasa <c>false</c> qaytadi.
    /// Bandlik qatorlarini qayta qurish — chaqiruvchining ishi.
    /// </summary>
    Task<bool> MoveCardAsync(
        int cardId, int dayNo, int periodId, int? weeksMask, CancellationToken ct = default);

    /// <summary>
    /// Bir nechta kartochkani ko'chiradi. Amallar tartibi shunday tanlanadiki,
    /// <c>UX_Cards_Schedule_Lesson_Day_Period_Weeks</c> unikal indeksi ORALIQ holatda
    /// ham buzilmaydi (ikki kartochkaning o'rin almashtirishi qo'llab-quvvatlanadi).
    /// </summary>
    /// <returns>Ko'chirilgan kartochkalar soni.</returns>
    Task<int> MoveCardsAsync(
        IReadOnlyList<CardPlacement> placements, CancellationToken ct = default);

    /// <summary>
    /// Kartochka qulfini bazaga saqlaydi. Topilmasa <c>false</c> qaytadi.
    /// </summary>
    Task<bool> SetCardLockAsync(int cardId, bool isLocked, CancellationToken ct = default);

    /// <summary>
    /// BITTA kartochkani (va uning bandlik qatorlarini) o'chiradi. Topilmasa <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Ilgari bitta kartochkani o'chirish uchun <see cref="DeleteCardsAsync"/> +
    /// <see cref="InsertCardsAsync"/> bilan BUTUN jadval qayta yozilardi: <c>Card.Id</c>
    /// lar o'zgarib ketardi, taxta to'liq qayta yuklanardi va undo tarixi tozalanardi.
    /// </remarks>
    Task<bool> DeleteCardAsync(int cardId, CancellationToken ct = default);

    /// <summary>
    /// Sinfning smenasini o'zgartiradi (<c>SchoolClass.ShiftId</c>). Sinf yoki smena
    /// topilmasa <c>false</c> qaytadi.
    /// </summary>
    /// <param name="schoolClassId">Sinf Id.</param>
    /// <param name="shiftId">Yangi smena Id; <c>null</c> — smenadan chiqarish.</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    Task<bool> SetClassShiftAsync(int schoolClassId, int? shiftId, CancellationToken ct = default);
}
