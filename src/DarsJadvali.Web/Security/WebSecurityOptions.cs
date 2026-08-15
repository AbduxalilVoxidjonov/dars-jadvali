using DarsJadvali.Infrastructure.DependencyInjection;

namespace DarsJadvali.Web.Security;

/// <summary>
/// Veb qobiqning xavfsizlik sozlamalari. Barchasi <c>appsettings.json</c> dagi
/// <c>"Security"</c> bo'limidan yoki muhit o'zgaruvchisidan o'qiladi — KODDA
/// qattiq kodlangan kalit yo'q.
/// </summary>
public sealed class WebSecurityOptions
{
    /// <summary>Sozlamalar bo'limi nomi.</summary>
    public const string SectionName = "Security";

    /// <summary>Kalitni beradigan muhit o'zgaruvchisi.</summary>
    public const string ApiKeyEnvironmentVariable = "DARSJADVALI_API_KEY";

    /// <summary>Standart manzil — FAQAT shu kompyuter (tarmoqqa ochilmaydi).</summary>
    public const string DefaultUrl = "http://127.0.0.1:5080";

    /// <summary>Yozuv so'rovlari uchun kalit.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Kalit endigina yaratildimi (birinchi ishga tushish).</summary>
    public bool ApiKeyCreated { get; init; }

    /// <summary>Kalit saqlangan fayl (sozlamadan berilgan bo'lsa <c>null</c>).</summary>
    public string? ApiKeyFilePath { get; init; }

    /// <summary>O'qish (GET/HEAD) uchun ham kalit talab qilinsinmi.</summary>
    public bool RequireKeyForReads { get; init; }

    /// <summary>
    /// HTTPS ga majburiy yo'naltirish. Standart holatda O'CHIQ: dastur faqat
    /// <c>127.0.0.1</c> ni tinglaydi, u yerda TLS hech narsa qo'shmaydi (o'ziga o'zi
    /// imzolagan sertifikat esa brauzerda ogohlantirish beradi). Tarmoqqa ATAYLAB
    /// ochilganda (HTTPS endpoint sozlangan holda) yoqiladi.
    /// </summary>
    public bool RequireHttps { get; init; }

    /// <summary>Bir daqiqada bitta manzildan ruxsat etilgan so'rovlar soni.</summary>
    public int RequestsPerMinute { get; init; } = 240;

    /// <summary>CORS uchun ruxsat etilgan manbalar (bo'sh — begona manba umuman yo'q).</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = Array.Empty<string>();

    /// <summary>Dastur tinglayotgan manzillar.</summary>
    public string Urls { get; init; } = DefaultUrl;

    /// <summary>Manzillar orasida loopback bo'lmagani bormi (ya'ni tarmoqqa ochiqmi).</summary>
    public bool IsNetworkExposed { get; init; }

    /// <summary>
    /// Sozlamalarni yig'adi: kalit uchun tartib — <c>Security:ApiKey</c> &gt;
    /// <c>DARSJADVALI_API_KEY</c> &gt; baza yonidagi fayl (bo'lmasa yaratiladi).
    /// </summary>
    /// <param name="configuration">Dastur konfiguratsiyasi.</param>
    /// <param name="dbPath">Baza fayli yo'li — kalit fayli shuning yonida turadi.</param>
    /// <param name="urls">Dastur tinglaydigan manzillar.</param>
    public static WebSecurityOptions Load(IConfiguration configuration, string dbPath, string urls)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);

        var key = section["ApiKey"];
        string? keyFile = null;
        var created = false;

        if (string.IsNullOrWhiteSpace(key))
            key = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(key))
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            keyFile = Path.Combine(
                string.IsNullOrEmpty(folder) ? AppContext.BaseDirectory : folder,
                LocalApiKey.KeyFileName);

            (key, created) = LocalApiKey.LoadOrCreate(keyFile);
        }

        var requestsPerMinute = 240;
        if (int.TryParse(section["RequestsPerMinute"], out var parsed) && parsed > 0)
            requestsPerMinute = parsed;

        var origins = section.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        return new WebSecurityOptions
        {
            ApiKey = key.Trim(),
            ApiKeyCreated = created,
            ApiKeyFilePath = keyFile,
            RequireKeyForReads = bool.TryParse(section["RequireKeyForReads"], out var reads) && reads,
            RequireHttps = bool.TryParse(section["RequireHttps"], out var https) && https,
            RequestsPerMinute = requestsPerMinute,
            AllowedOrigins = origins,
            Urls = urls,
            IsNetworkExposed = HasNonLoopbackUrl(urls),
        };
    }

    /// <summary>Manzillar orasida loopback bo'lmagani bormi.</summary>
    private static bool HasNonLoopbackUrl(string urls)
    {
        foreach (var raw in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var url))
                continue;

            var host = url.Host;

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host is "127.0.0.1" or "::1" or "[::1]")
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
