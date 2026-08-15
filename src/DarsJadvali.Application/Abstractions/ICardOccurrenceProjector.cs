namespace DarsJadvali.Application.Abstractions;

/// <summary>
/// <c>CardOccurrence</c> hosila jadvalining <b>yagona egasi</b>. Bandlik qatorlarini
/// qo'lda yozish/tahrirlash taqiqlanadi — faqat shu servis orqali.
/// </summary>
/// <remarks>
/// 00 §10.8 TODO-2 bo'yicha interfeys <c>Infrastructure/Persistence/Projection</c> dan
/// shu yerga ko'chirildi; implementatsiya Infrastructure'da qoldi.
/// <para>
/// <b>Tranzaksiya qoidasi (00 §6.4/5):</b> projector hech qachon o'z tranzaksiyasini
/// ochmaydi — u chaqiruvchining tranzaksiyasi ichida ishlaydi.
/// </para>
/// </remarks>
public interface ICardOccurrenceProjector
{
    /// <summary>Bitta kartochkaning bandlik qatorlarini qayta quradi.</summary>
    /// <returns>Yozilgan qatorlar soni.</returns>
    Task<int> RebuildForCardAsync(int cardId, CancellationToken ct = default);

    /// <summary>
    /// Bir necha kartochkaning bandlik qatorlarini qayta quradi: avval BARCHASINING
    /// eski qatorlari o'chiriladi, keyin yangilari yoziladi.
    /// </summary>
    /// <remarks>
    /// <b>Tartib muhim.</b> Kartochkalarni bittalab qayta qurish "o'rin almashtirish"
    /// stsenariysini yiqitardi: A karta B ning eski o'rniga ko'chsa, B hali qayta
    /// qurilmagan bo'lgani uchun uning ESKI qatorlari bazada turadi va unikal indeks
    /// noto'g'ri ravishda to'sardi. Ikki fazali qayta qurish shuni yopadi.
    /// </remarks>
    /// <returns>Yozilgan qatorlar soni.</returns>
    Task<int> RebuildForCardsAsync(IReadOnlyList<int> cardIds, CancellationToken ct = default);

    /// <summary>Butun jadval variantining bandlik qatorlarini qayta quradi.</summary>
    /// <returns>Yozilgan qatorlar soni.</returns>
    Task<int> RebuildForScheduleAsync(int scheduleId, CancellationToken ct = default);
}
