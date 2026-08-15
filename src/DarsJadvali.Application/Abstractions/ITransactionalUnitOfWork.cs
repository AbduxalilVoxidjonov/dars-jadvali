namespace DarsJadvali.Application.Abstractions;

/// <summary>
/// Tranzaksiya chegarasi — bir nechta yozishni bitta atomik amalga birlashtiradi.
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> Har bir repozitoriy metodi o'z <c>SaveChangesAsync</c> ini
/// chaqiradi. Shu sababli jadval generatsiyasi kabi ko'p qadamli amal o'rtasida xato
/// chiqsa <b>yarim yozilgan jadval</b> qolib ketardi (05-audit, K-04). Shu interfeys
/// orqali "eskisini o'chir → yangisini yoz → bandlikni qayta qur" ketma-ketligi
/// bitta tranzaksiyada bajariladi: xato bo'lsa eski jadval joyida qoladi.
/// <para>
/// <b>Qoida (00 §6.4):</b> tranzaksiyani faqat Application servisining ommaviy metodi
/// ochadi. Repozitoriy ham, Infrastructure ham o'zi tranzaksiya ochmaydi.
/// </para>
/// </remarks>
public interface ITransactionalUnitOfWork
{
    /// <summary>O'zgarishlarni saqlaydi.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Berilgan amalni bitta tranzaksiya ichida bajaradi. Xato bo'lsa hammasi qaytariladi.
    /// Allaqachon tranzaksiya ochiq bo'lsa yangisi ochilmaydi (qayta kirishga xavfsiz).
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action, CancellationToken ct = default);

    /// <summary>
    /// <see cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)"/>
    /// ning natija qaytaradigan varianti.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);
}
