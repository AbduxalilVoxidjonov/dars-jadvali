using System.Net;
using System.Text.Json;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Common;

namespace DarsJadvali.Infrastructure.Update;

/// <summary>
/// GitHub'dagi so'nggi relizni tekshiradi.
/// <para>
/// ASOSIY usul — <c>https://github.com/{owner}/{repo}/releases/latest</c> manziliga
/// redirect'ni KUZATMAYDIGAN so'rov. GitHub <c>302</c> qaytaradi va <c>Location</c>
/// sarlavhasida aynan so'nggi reliz tegi bo'ladi (<c>.../releases/tag/v1.2.0</c>).
/// Bu API emas — so'rovlar cheklovi (soatiga 60 ta) QO'LLANMAYDI. NAT ortidagi
/// maktab tarmoqlarida ham ishonchli ishlaydi.
/// </para>
/// <para>
/// QO'SHIMCHA usul — reliz izohini (<c>body</c>) faqat API beradi. U <b>best-effort</b>
/// olinadi: API 403/404/xato bersa, izohsiz davom etiladi va holat <b>Failed'ga
/// o'tmaydi</b>. Redirect usuli umuman ishlamasagina API zaxira sifatida sinaladi.
/// </para>
/// Tarmoq xatosi, cheklov yoki noto'g'ri javob bo'lsa ham istisno tashlamaydi —
/// har doim tushunarli o'zbekcha xabarli natija qaytaradi.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    /// <summary>Butun tekshiruv uchun eng ko'p kutish vaqti — dastur qotib qolmasligi uchun.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Reliz izohidan ko'rsatiladigan eng ko'p belgilar soni.</summary>
    private const int MaxReleaseNotesLength = 400;

    /// <summary>Reliz sahifasi manzilida tegdan oldin keladigan bo'lak.</summary>
    private const string TagPathMarker = "/releases/tag/";

    private readonly HttpClient _http;
    private readonly string _currentVersion;
    private readonly string _latestReleaseUrl;
    private readonly string _apiUrl;

    /// <summary>
    /// Yangi tekshiruvchi yaratadi.
    /// </summary>
    /// <param name="httpClient">
    /// HTTP mijoz (sinovda soxta handler bilan almashtiriladi). Ishlab chiqarishda
    /// <see cref="CreateHttpClient"/> orqali yaratilgan, redirect'ni AVTOMATIK
    /// KUZATMAYDIGAN mijoz berilishi kerak.
    /// </param>
    /// <param name="currentVersion">Joriy versiya; berilmasa <see cref="AppInfo.Version"/>.</param>
    /// <param name="latestReleaseUrl">
    /// So'nggi relizga yo'naltiruvchi sahifa; berilmasa <see cref="AppInfo.LatestReleaseUrl"/>.
    /// </param>
    /// <param name="apiUrl">API manzili; berilmasa <see cref="AppInfo.ReleasesApiUrl"/>.</param>
    public GitHubUpdateChecker(
        HttpClient httpClient,
        string? currentVersion = null,
        string? latestReleaseUrl = null,
        string? apiUrl = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? AppInfo.Version : currentVersion.Trim();
        _latestReleaseUrl = string.IsNullOrWhiteSpace(latestReleaseUrl)
            ? AppInfo.LatestReleaseUrl
            : latestReleaseUrl.Trim();
        _apiUrl = string.IsNullOrWhiteSpace(apiUrl) ? AppInfo.ReleasesApiUrl : apiUrl.Trim();
    }

    /// <summary>
    /// Ishlab chiqarish uchun mos HTTP handler yaratadi. <c>AllowAutoRedirect = false</c>
    /// MAJBURIY: aks holda mijoz 302 ni o'zi kuzatib ketadi va <c>Location</c>
    /// sarlavhasidagi reliz tegini o'qib bo'lmaydi.
    /// </summary>
    public static HttpClientHandler CreateHandler()
        => new() { AllowAutoRedirect = false };

    /// <summary>Ishlab chiqarish uchun mos HTTP mijoz yaratadi.</summary>
    public static HttpClient CreateHttpClient()
        => new(CreateHandler())
        {
            Timeout = RequestTimeout,
        };

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        // HttpClient.Timeout emas, balki bog'langan token: sinovda berilgan mijozga tegmaydi
        // va tashqi bekor qilishni ham to'g'ri uzatadi. Bitta byudjet butun tekshiruvga —
        // shuning uchun ikkita so'rov ham umumiy 10 soniyadan oshmaydi.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);
        var budget = timeoutCts.Token;

        var probe = await ProbeLatestReleaseAsync(budget, ct).ConfigureAwait(false);

        // Repoda hali birorta reliz yo'q: "releases/latest" 302 emas, 404 qaytaradi.
        if (probe.Kind == ProbeKind.NoRelease)
            return NoRelease();

        if (probe.Kind == ProbeKind.Found)
        {
            var result = Evaluate(probe.Tag!, probe.ReleaseUrl, notes: null);

            // Izoh faqat yangilanish bo'lganda va faqat best-effort tarzda olinadi.
            if (result.Status == UpdateStatus.UpdateAvailable)
            {
                var notes = await TryReadReleaseNotesAsync(budget, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(notes))
                    result = result with { ReleaseNotes = notes };
            }

            return result;
        }

        // Redirect usuli ishlamadi (tarmoq yo'q, GitHub yopiq, javob tushunarsiz) —
        // faqat shunda API zaxira sifatida sinaladi.
        return await CheckViaApiAsync(budget, ct, probe.FailureMessage!).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------
    // 1. Asosiy usul — redirect (cheklovsiz)
    // -----------------------------------------------------------------

    /// <summary>Redirect javobining natijasi.</summary>
    private enum ProbeKind
    {
        /// <summary>Teg topildi.</summary>
        Found,

        /// <summary>404 — repoda reliz yo'q.</summary>
        NoRelease,

        /// <summary>Usul ishlamadi; zaxira sifatida API sinalishi mumkin.</summary>
        Unavailable,
    }

    /// <summary>Redirect tekshiruvining natijasi.</summary>
    private readonly record struct Probe(
        ProbeKind Kind,
        string? Tag,
        string? ReleaseUrl,
        string? FailureMessage)
    {
        public static Probe Found(string tag, string releaseUrl)
            => new(ProbeKind.Found, tag, releaseUrl, null);

        public static Probe NoRelease() => new(ProbeKind.NoRelease, null, null, null);

        public static Probe Unavailable(string message)
            => new(ProbeKind.Unavailable, null, null, message);
    }

    /// <summary>
    /// <c>releases/latest</c> ga <c>HEAD</c> so'rov yuboradi va <c>Location</c>
    /// sarlavhasidan so'nggi reliz tegini ajratib oladi.
    /// </summary>
    private async Task<Probe> ProbeLatestReleaseAsync(CancellationToken budget, CancellationToken userToken)
    {
        try
        {
            // HEAD: tana kerak emas, faqat sarlavhalar. Redirect KUZATILMAYDI —
            // aks holda Location sarlavhasi yo'qoladi (qarang: CreateHttpClient).
            using var request = new HttpRequestMessage(HttpMethod.Head, _latestReleaseUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", AppInfo.HttpUserAgent);
            request.Headers.TryAddWithoutValidation("Accept", "text/html");

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Probe.NoRelease();

            var location = ResolveLocation(response, request.RequestUri);
            var tag = ExtractTag(location);

            if (tag is null)
            {
                // 302 keldi-yu Location yo'q/tushunarsiz bo'lsa ham yiqilmaymiz.
                return Probe.Unavailable(
                    "GitHub javobidan so'nggi reliz aniqlanmadi. Keyinroq qayta urinib ko'ring.");
            }

            return Probe.Found(tag, location!.ToString());
        }
        catch (OperationCanceledException) when (userToken.IsCancellationRequested)
        {
            // Foydalanuvchi (yoki sahifa) bekor qilgan — bu xato emas, yuqoriga uzatiladi.
            throw;
        }
        catch (OperationCanceledException)
        {
            return Probe.Unavailable("So'rov vaqti tugadi (10 soniya). Keyinroq qayta urinib ko'ring.");
        }
        catch (HttpRequestException)
        {
            return Probe.Unavailable("Internetga ulanib bo'lmadi. Keyinroq qayta urinib ko'ring.");
        }
        catch (Exception)
        {
            // Kutilmagan holat ham dasturni yiqitmasligi kerak.
            return Probe.Unavailable("Yangilanishni tekshirib bo'lmadi. Keyinroq qayta urinib ko'ring.");
        }
    }

    /// <summary>
    /// Yo'naltirish manzilini aniqlaydi. Nisbiy <c>Location</c> so'rov manziliga nisbatan
    /// to'ldiriladi. Agar mijoz (sozlamaga qaramay) redirect'ni o'zi kuzatib ketgan bo'lsa,
    /// oxirgi so'rov manzilining o'zida teg bo'ladi — u ham qabul qilinadi.
    /// </summary>
    private static Uri? ResolveLocation(HttpResponseMessage response, Uri? requestUri)
    {
        var location = response.Headers.Location;

        if (location is not null)
        {
            if (location.IsAbsoluteUri)
                return location;

            return requestUri is null ? null : new Uri(requestUri, location);
        }

        return response.RequestMessage?.RequestUri;
    }

    /// <summary>
    /// <c>.../releases/tag/v1.2.0</c> ko'rinishidagi manzildan tegni ajratadi.
    /// Mos kelmasa <c>null</c> qaytaradi (so'rov manzilining o'zi tegga o'xshamaydi).
    /// </summary>
    private static string? ExtractTag(Uri? url)
    {
        if (url is null)
            return null;

        var text = url.IsAbsoluteUri ? url.AbsoluteUri : url.OriginalString;

        var index = text.IndexOf(TagPathMarker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var tag = text[(index + TagPathMarker.Length)..];

        var cut = tag.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0)
            tag = tag[..cut];

        tag = tag.Trim('/');

        try
        {
            tag = Uri.UnescapeDataString(tag);
        }
        catch (UriFormatException)
        {
            // Kodlanishi buzuq bo'lsa — asl matn bilan davom etamiz.
        }

        tag = tag.Trim();
        return tag.Length == 0 ? null : tag;
    }

    // -----------------------------------------------------------------
    // 2. Qo'shimcha usul — API (best-effort izoh) va zaxira yo'l
    // -----------------------------------------------------------------

    /// <summary>API so'rovini sarlavhalari bilan tayyorlaydi.</summary>
    private HttpRequestMessage CreateApiRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _apiUrl);

        // GitHub User-Agent'siz so'rovni 403 bilan rad etadi — bu sarlavha majburiy.
        request.Headers.TryAddWithoutValidation("User-Agent", AppInfo.HttpUserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        return request;
    }

    /// <summary>
    /// Reliz izohini BEST-EFFORT oladi: 403 (cheklov), 404, timeout yoki har qanday
    /// xatoda shunchaki <c>null</c> qaytaradi va tekshiruv natijasiga TA'SIR QILMAYDI.
    /// </summary>
    private async Task<string?> TryReadReleaseNotesAsync(CancellationToken budget, CancellationToken userToken)
    {
        try
        {
            using var request = CreateApiRequest();
            using var response = await _http.SendAsync(request, budget).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(budget).ConfigureAwait(false);

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            return Shorten(ReadString(document.RootElement, "body"));
        }
        catch (OperationCanceledException) when (userToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Izoh — qo'shimcha qulaylik. Bo'lmasa ham funksiya ishlayveradi.
            return null;
        }
    }

    /// <summary>
    /// Zaxira yo'l: redirect usuli umuman ishlamaganda API orqali tekshirish.
    /// </summary>
    /// <param name="fallbackMessage">API ham ishlamasa ko'rsatiladigan xabar.</param>
    private async Task<UpdateCheckResult> CheckViaApiAsync(
        CancellationToken budget, CancellationToken userToken, string fallbackMessage)
    {
        HttpResponseMessage response;
        string body;

        try
        {
            using var request = CreateApiRequest();

            response = await _http.SendAsync(request, budget).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(budget).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (userToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Ikkala usul ham ishlamadi — redirect bosqichidagi xabar aniqroq.
            return Failed(fallbackMessage);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return NoRelease();

            if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
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

        return EvaluateApiBody(body);
    }

    /// <summary>API javob matnini o'qib, natijaga aylantiradi.</summary>
    private UpdateCheckResult EvaluateApiBody(string body)
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

        return Evaluate(tag, releaseUrl, notes);
    }

    // -----------------------------------------------------------------
    // 3. Versiyani solishtirish (usuldan qat'i nazar bir xil)
    // -----------------------------------------------------------------

    /// <summary>Topilgan tegni joriy versiya bilan solishtiradi.</summary>
    private UpdateCheckResult Evaluate(string tag, string? releaseUrl, string? notes)
    {
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

    /// <summary>Reliz e'lon qilinmagan holat — bu xato emas.</summary>
    private static UpdateCheckResult NoRelease()
        => new(
            UpdateStatus.NoRelease,
            LatestVersion: null,
            ReleaseUrl: AppInfo.ReleasesUrl,
            ReleaseNotes: null,
            Message: "Hozircha reliz e'lon qilinmagan.");

    /// <summary>Xato holati uchun qisqa yordamchi.</summary>
    private static UpdateCheckResult Failed(string message)
        => new(UpdateStatus.Failed, null, AppInfo.ReleasesUrl, null, message);
}
