namespace DarsJadvali.Application.Abstractions;

/// <summary>Yangilanishni tekshirish natijasining holati.</summary>
public enum UpdateStatus
{
    /// <summary>O'rnatilgan versiya eng so'nggisi.</summary>
    UpToDate = 0,

    /// <summary>Yangi versiya mavjud.</summary>
    UpdateAvailable = 1,

    /// <summary>Repozitoriyda hali birorta reliz e'lon qilinmagan. Bu xato emas.</summary>
    NoRelease = 2,

    /// <summary>Tekshirib bo'lmadi (tarmoq, cheklov yoki noto'g'ri javob).</summary>
    Failed = 3,
}

/// <summary>
/// Yangilanishni tekshirish natijasi. <see cref="Message"/> — foydalanuvchiga
/// to'g'ridan-to'g'ri ko'rsatiladigan o'zbekcha matn.
/// </summary>
/// <param name="Status">Natija holati.</param>
/// <param name="LatestVersion">Topilgan so'nggi versiya (masalan "1.2.0"), aniqlanmasa null.</param>
/// <param name="ReleaseUrl">Reliz sahifasi — brauzerda ochish uchun, aniqlanmasa null.</param>
/// <param name="ReleaseNotes">Reliz izohi (qisqartirilgan), bo'lmasa null.</param>
/// <param name="Message">Foydalanuvchiga ko'rsatiladigan o'zbekcha xabar.</param>
public sealed record UpdateCheckResult(
    UpdateStatus Status,
    string? LatestVersion,
    string? ReleaseUrl,
    string? ReleaseNotes,
    string Message);

/// <summary>Yangi versiya chiqqanini tekshiradigan servis.</summary>
public interface IUpdateChecker
{
    /// <summary>
    /// So'nggi relizni tekshiradi. Hech qanday holatda istisno tashlamaydi —
    /// tarmoq yo'q bo'lsa ham <see cref="UpdateStatus.Failed"/> qaytaradi
    /// (foydalanuvchi bekor qilgan holat bundan mustasno).
    /// </summary>
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);
}
