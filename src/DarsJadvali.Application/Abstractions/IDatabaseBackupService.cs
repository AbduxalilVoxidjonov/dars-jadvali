namespace DarsJadvali.Application.Abstractions;

/// <summary>
/// Migratsiya oldidan bazaning avtomatik zaxira nusxasini oluvchi servis (00 §4.4, §10.8/8).
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> Sxema o'zgarishi (jadval qayta qurish, ustun o'chirish)
/// foydalanuvchi bazasida qaytarib bo'lmaydigan holat yaratishi mumkin. Dastur
/// yangilangandan keyingi BIRINCHI ishga tushishda migratsiyalar qo'llanadi —
/// aynan shundan oldin faylning butun nusxasi saqlanadi.
/// </remarks>
public interface IDatabaseBackupService
{
    /// <summary>
    /// Zaxira nusxa yaratadi va uning to'liq yo'lini qaytaradi.
    /// Zaxira kerak bo'lmasa yoki mumkin bo'lmasa (xotiradagi baza, fayl yo'q,
    /// kutilayotgan migratsiya yo'q) — <c>null</c>.
    /// </summary>
    /// <param name="onlyIfMigrationsPending">
    /// <c>true</c> (odatiy) — faqat qo'llanmagan migratsiya bo'lsa zaxira olinadi.
    /// </param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    Task<string?> CreateBackupAsync(bool onlyIfMigrationsPending = true, CancellationToken ct = default);
}
