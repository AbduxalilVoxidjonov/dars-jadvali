using System.Reflection;

namespace DarsJadvali.Domain.Common;

/// <summary>Dastur haqidagi ma'lumotlarning yagona manbasi.</summary>
public static class AppInfo
{
    /// <summary>Dastur nomi.</summary>
    public const string AppName = "Dars Jadvali Tuzuvchi";

    /// <summary>
    /// Dastur versiyasi. YAGONA MANBA — <c>Directory.Build.props</c> dagi <c>&lt;Version&gt;</c>:
    /// u qurilish paytida assembly atributiga yoziladi va shu yerda o'qiladi.
    /// Shu sababli <c>const</c> EMAS — aks holda versiya kodda ikkinchi marta yozilib,
    /// props bilan bir-biriga mos kelmay qolardi.
    /// </summary>
    public static readonly string Version = ReadAssemblyVersion();

    /// <summary>Muallif.</summary>
    public const string Author = "Abduxalil Voxidjonov";

    /// <summary>Telegram havolasi.</summary>
    public const string TelegramUrl = "https://t.me/abduxalilvoxidjonov";

    /// <summary>Telegram foydalanuvchi nomi.</summary>
    public const string TelegramHandle = "@abduxalilvoxidjonov";

    /// <summary>Xayriya uchun karta raqami.</summary>
    public const string DonateCardNumber = "9860 3501 4679 1495";

    /// <summary>Karta turi.</summary>
    public const string DonateCardType = "Humo";

    /// <summary>Karta egasi.</summary>
    public const string DonateCardHolder = "Abduxalil Voxidjonov";

    /// <summary>Dastur haqida qisqacha.</summary>
    public const string Description = "Maktab va o'quv markazlari uchun dars jadvalini tuzish dasturi.";

    /// <summary>Loyihaning GitHub repozitoriysi.</summary>
    public const string RepositoryUrl = "https://github.com/AbduxalilVoxidjonov/dars-jadvali";

    /// <summary>Relizlar ro'yxati sahifasi (brauzerda ochish uchun zaxira havola).</summary>
    public const string ReleasesUrl = RepositoryUrl + "/releases";

    /// <summary>
    /// So'nggi relizga yo'naltiruvchi sahifa. Bu manzil API EMAS: u <c>302</c> bilan
    /// <c>.../releases/tag/vX.Y.Z</c> ga yo'naltiradi va so'rovlar cheklovi (rate limit)
    /// qo'llanmaydi. Yangilanishni tekshirishning ASOSIY manbasi shu.
    /// </summary>
    public const string LatestReleaseUrl = ReleasesUrl + "/latest";

    /// <summary>
    /// So'nggi relizni qaytaradigan GitHub API manzili. Autentifikatsiyasiz bu API
    /// IP manzil bo'yicha soatiga 60 ta so'rov bilan cheklangan, shuning uchun u faqat
    /// reliz izohini olish uchun (best-effort) va zaxira usul sifatida ishlatiladi.
    /// </summary>
    public const string ReleasesApiUrl =
        "https://api.github.com/repos/AbduxalilVoxidjonov/dars-jadvali/releases/latest";

    /// <summary>
    /// GitHub API uchun <c>User-Agent</c> sarlavhasi. GitHub bu sarlavhasiz
    /// so'rovlarni 403 bilan rad etadi, shuning uchun u majburiy.
    /// </summary>
    public static readonly string HttpUserAgent = "DarsJadvali/" + Version + " (+" + RepositoryUrl + ")";

    /// <summary>
    /// Assembly atributidan versiyani o'qiydi:
    /// avval <c>InformationalVersion</c> (props dagi <c>&lt;Version&gt;</c> aynan shunga tushadi),
    /// bo'lmasa <c>AssemblyVersion</c>. <c>"1.0.0+abc123"</c> ko'rinishidagi qurilish
    /// qo'shimchasi kesiladi — foydalanuvchiga faqat toza raqam ko'rinadi.
    /// </summary>
    private static string ReadAssemblyVersion()
    {
        var assembly = typeof(AppInfo).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var text = informational.Trim();
            var cut = text.IndexOf('+');
            if (cut > 0)
                text = text[..cut];

            if (text.Length > 0)
                return text;
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : version.ToString(3);
    }
}
