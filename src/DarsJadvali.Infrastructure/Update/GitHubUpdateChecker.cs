using System.Net;
using System.Text.Json;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Common;

namespace DarsJadvali.Infrastructure.Update;

/// <summary>
/// GitHub'ning ochiq API'si orqali so'nggi relizni tekshiradi:
/// <c>GET /repos/{owner}/{repo}/releases/latest</c>.
/// Tarmoq xatosi, cheklov yoki noto'g'ri javob bo'lsa ham istisno tashlamaydi —
/// har doim tushunarli o'zbekcha xabarli natija qaytaradi.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    /// <summary>So'rov uchun eng ko'p kutish vaqti — dastur qotib qolmasligi uchun.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Reliz izohidan ko'rsatiladigan eng ko'p belgilar soni.</summary>
    private const int MaxReleaseNotesLength = 400;

    private readonly HttpClient _http;
    private readonly string _currentVersion;
    private readonly string _apiUrl;

    /// <summary>
    /// Yangi tekshiruvchi yaratadi.
    /// </summary>
    /// <param name="httpClient">HTTP mijoz (sinovda soxta handler bilan almashtiriladi).</param>
    /// <param name="currentVersion">Joriy versiya; berilmasa <see cref="AppInfo.Version"/>.</param>
    /// <param name="apiUrl">API manzili; berilmasa <see cref="AppInfo.ReleasesApiUrl"/>.</param>
    public GitHubUpdateChecker(HttpClient httpClient, string? currentVersion = null, string? apiUrl = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? AppInfo.Version : currentVersion.Trim();
        _apiUrl = string.IsNullOrWhiteSpace(apiUrl) ? AppInfo.ReleasesApiUrl : apiUrl.Trim();
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response;
        string body;

        // HttpClient.Timeout emas, balki bog'langan token: sinovda berilgan mijozga tegmaydi
        // va tashqi bekor qilishni ham to'g'ri uzatadi.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _apiUrl);

            // GitHub User-Agent'siz so'rovni 403 bilan rad etadi — bu sarlavha majburiy.
            request.Headers.TryAddWithoutValidation("User-Agent", AppInfo.HttpUserAgent);
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            response = await _http.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Foydalanuvchi (yoki sahifa) bekor qilgan — bu xato emas, yuqoriga uzatiladi.
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed("So'rov vaqti tugadi (10 soniya). Keyinroq qayta urinib ko'ring.");
        }
        catch (HttpRequestException)
        {
            return Failed("Internetga ulanib bo'lmadi. Keyinroq qayta urinib ko'ring.");
        }
        catch (Exception)
        {
            // Kutilmagan holat ham dasturni yiqitmasligi kerak.
            return Failed("Yangilanishni tekshirib bo'lmadi. Keyinroq qayta urinib ko'ring.");
        }

        using (response)
        {
            // Repoda hali birorta reliz yo'q — bu xato emas, xotirjam holat.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(
                    UpdateStatus.NoRelease,
                    LatestVersion: null,
                    ReleaseUrl: AppInfo.ReleasesUrl,
                    ReleaseNotes: null,
                    Message: "Hozircha reliz e'lon qilinmagan.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden ||
                (int)response.StatusCode == 429)
            {
                return Failed(IsRateLimited(response, body)
                    ? "GitHub so'rovlar chegarasiga yetildi (soatiga 60 ta so'rov). " +
                      "Bir ozdan keyin qayta urinib ko'ring."
                    : "GitHub so'rovni rad etdi. Keyinroq qayta urinib ko'ring.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failed(
                    "GitHub javob bermadi (kod " + (int)response.StatusCode + "). " +
                    "Keyinroq qayta urinib ko'ring.");
            }
        }

        return Evaluate(body);
    }

    /// <summary>Muvaffaqiyatli javob matnini o'qib, natijaga aylantiradi.</summary>
    private UpdateCheckResult Evaluate(string body)
    {
        string? tag;
        string? releaseUrl;
        string? notes;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return Failed("GitHub javobini o'qib bo'lmadi.");

            tag = ReadString(root, "tag_name");
            releaseUrl = ReadString(root, "html_url");
            notes = Shorten(ReadString(root, "body"));
        }
        catch (JsonException)
        {
            return Failed("GitHub javobini o'qib bo'lmadi.");
        }

        if (string.IsNullOrWhiteSpace(tag))
            return Failed("Reliz ma'lumotida versiya tegi topilmadi.");

        if (string.IsNullOrWhiteSpace(releaseUrl))
            releaseUrl = AppInfo.ReleasesUrl;

        var latestText = Normalize(tag);

        if (!TryParseVersion(tag, out var latest))
        {
            return new UpdateCheckResult(
                UpdateStatus.Failed,
                latestText,
                releaseUrl,
                notes,
                "Reliz tegi (\"" + tag.Trim() + "\") versiya formatiga mos emas, " +
                "shuning uchun solishtirib bo'lmadi.");
        }

        if (!TryParseVersion(_currentVersion, out var current))
            return Failed("Joriy versiya (\"" + _currentVersion + "\") noto'g'ri formatda.");

        if (latest > current)
        {
            return new UpdateCheckResult(
                UpdateStatus.UpdateAvailable,
                latestText,
                releaseUrl,
                notes,
                "Yangi versiya mavjud: " + latestText + " (sizda " + _currentVersion + ").");
        }

        return new UpdateCheckResult(
            UpdateStatus.UpToDate,
            latestText,
            releaseUrl,
            notes,
            "Sizda eng so'nggi versiya o'rnatilgan (" + _currentVersion + ").");
    }

    /// <summary>
    /// Versiya tegini raqamli <see cref="Version"/> ga o'giradi.
    /// Boshidagi <c>v</c> olib tashlanadi, <c>1.2.0-beta</c> kabi qo'shimchalar kesiladi,
    /// va <c>1.2</c> bilan <c>1.2.0</c> teng bo'lishi uchun bo'sh bo'laklar nolga to'ldiriladi.
    /// Shu tufayli solishtirish SATR emas, RAQAM bo'yicha bo'ladi: 1.10.0 &gt; 1.9.0.
    /// </summary>
    private static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0, 0, 0);

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();

        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            text = text[1..];

        // Semver qo'shimchalari ("-beta.1", "+build") solishtirishga kirmaydi.
        var cut = text.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0)
            text = text[..cut];

        text = text.Trim();
        if (text.Length == 0)
            return false;

        // "2" kabi bitta bo'lakli teg ham qabul qilinsin.
        if (!text.Contains('.'))
            text += ".0";

        if (!Version.TryParse(text, out var parsed))
            return false;

        version = new Version(
            parsed.Major,
            parsed.Minor,
            Math.Max(parsed.Build, 0),
            Math.Max(parsed.Revision, 0));

        return true;
    }

    /// <summary>Tegdan boshidagi <c>v</c> harfini olib tashlaydi ("v1.2.0" → "1.2.0").</summary>
    private static string Normalize(string tag)
    {
        var text = tag.Trim();
        return text.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? text[1..] : text;
    }

    /// <summary>403 javobi so'rov cheklovi sababli chiqqanmi.</summary>
    private static bool IsRateLimited(HttpResponseMessage response, string body)
    {
        if (response.Headers.TryGetValues("x-ratelimit-remaining", out var values) &&
            values.FirstOrDefault() == "0")
        {
            return true;
        }

        return body.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>JSON obyektidan matnli maydonni xavfsiz o'qiydi.</summary>
    private static string? ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Reliz izohini ko'rsatishga qulay uzunlikka qisqartiradi.</summary>
    private static string? Shorten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        return trimmed.Length <= MaxReleaseNotesLength
            ? trimmed
            : trimmed[..MaxReleaseNotesLength].TrimEnd() + "…";
    }

    /// <summary>Xato holati uchun qisqa yordamchi.</summary>
    private static UpdateCheckResult Failed(string message)
        => new(UpdateStatus.Failed, null, AppInfo.ReleasesUrl, null, message);
}
