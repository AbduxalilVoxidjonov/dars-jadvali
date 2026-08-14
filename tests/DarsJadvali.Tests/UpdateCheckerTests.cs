using System.Net;
using System.Text;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Infrastructure.Update;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// <see cref="GitHubUpdateChecker"/> testlari. Hech biri tarmoqqa chiqmaydi —
/// HTTP qatlami soxta <see cref="HttpMessageHandler"/> bilan almashtirilgan.
/// <para>
/// Tekshiruvchi ikki manbadan foydalanadi:
/// ASOSIY — <c>github.com/.../releases/latest</c> ga <c>HEAD</c> so'rov (302 + Location),
/// QO'SHIMCHA — <c>api.github.com</c> (faqat reliz izohi uchun, best-effort).
/// Shu sababli soxta handler so'rov manziliga qarab javob beradi.
/// </para>
/// </summary>
public class UpdateCheckerTests
{
    // -----------------------------------------------------------------
    // Yordamchilar
    // -----------------------------------------------------------------

    private const string RepoUrl = "https://github.com/AbduxalilVoxidjonov/dars-jadvali";
    private const string LatestUrl = RepoUrl + "/releases/latest";
    private const string ApiUrl =
        "https://api.github.com/repos/AbduxalilVoxidjonov/dars-jadvali/releases/latest";

    /// <summary>Oldindan tayyorlangan javobni qaytaradigan soxta HTTP handler.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        /// <summary>So'rovda kelgan sarlavhalarni tekshirish uchun saqlanadi.</summary>
        public HttpRequestMessage? LastRequest { get; private set; }

        /// <summary>Yuborilgan barcha so'rovlar (tartib bo'yicha).</summary>
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responder(request));
        }
    }

    /// <summary>So'rov API manziliga yuborilganmi.</summary>
    private static bool IsApi(HttpRequestMessage request)
        => request.RequestUri!.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>GitHub'ning 302 javobini taqlid qiladi: <c>Location</c> — reliz tegi sahifasi.</summary>
    private static HttpResponseMessage Redirect(string tag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.TryAddWithoutValidation("Location", RepoUrl + "/releases/tag/" + tag);
        return response;
    }

    /// <summary>Berilgan holat kodi va tana bilan javob.</summary>
    private static HttpResponseMessage Text(HttpStatusCode status, string body = "")
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    /// <summary>Berilgan teg bilan GitHub reliz JSON'ini yasaydi.</summary>
    private static string ReleaseJson(string tag, string? notes = "Yangi imkoniyatlar.")
        => "{\"tag_name\":\"" + tag + "\"," +
           "\"name\":\"Reliz " + tag + "\"," +
           "\"html_url\":\"" + RepoUrl + "/releases/tag/" + tag + "\"," +
           "\"body\":\"" + notes + "\"}";

    /// <summary>
    /// Odatiy holat: redirect 302 bilan tegni beradi, API esa reliz JSON'ini
    /// (ya'ni izohni) qaytaradi.
    /// </summary>
    private static GitHubUpdateChecker CheckerFor(
        string tag, string currentVersion, string? notes = "Yangi imkoniyatlar.")
        => CheckerFor(tag, currentVersion, notes, out _);

    private static GitHubUpdateChecker CheckerFor(
        string tag, string currentVersion, string? notes, out FakeHandler handler)
    {
        var captured = new FakeHandler(request => IsApi(request)
            ? Text(HttpStatusCode.OK, ReleaseJson(tag, notes))
            : Redirect(tag));

        handler = captured;
        return new GitHubUpdateChecker(new HttpClient(captured), currentVersion);
    }

    /// <summary>Har qanday so'rovga bir xil javob beradigan tekshiruvchi.</summary>
    private static GitHubUpdateChecker CheckerForAll(
        HttpStatusCode status, string body, string currentVersion, out FakeHandler handler)
    {
        var captured = new FakeHandler(_ => Text(status, body));
        handler = captured;
        return new GitHubUpdateChecker(new HttpClient(captured), currentVersion);
    }

    // -----------------------------------------------------------------
    // 1. Versiyalarni solishtirish (asosiy usul — redirect)
    // -----------------------------------------------------------------

    [Fact]
    public async Task Yangi_versiya_chiqqan_bolsa_yangilanish_bor_deydi()
    {
        var checker = CheckerFor("v1.2.0", currentVersion: "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion);
        Assert.Contains("1.2.0", result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.ReleaseUrl));
    }

    /// <summary>302 dagi <c>Location</c> — reliz sahifasining aynan o'zi.</summary>
    [Fact]
    public async Task Redirect_manzili_reliz_sahifasi_sifatida_olinadi()
    {
        var checker = CheckerFor("v1.2.0", currentVersion: "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(RepoUrl + "/releases/tag/v1.2.0", result.ReleaseUrl);
    }

    [Fact]
    public async Task Bir_xil_versiya_eng_songgi_deb_qaraladi()
    {
        var checker = CheckerFor("v1.2.0", currentVersion: "1.2.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion);
    }

    /// <summary>
    /// Eng nozik holat: SATR sifatida "1.9.0" &gt; "1.10.0" bo'ladi, ammo RAQAM
    /// sifatida 1.10.0 kattaroq. Bu test satr solishtirish xatosini ushlaydi.
    /// </summary>
    [Fact]
    public async Task Raqamli_solishtirish_1_10_0_ni_1_9_0_dan_katta_deb_biladi()
    {
        var checker = CheckerFor("v1.9.0", currentVersion: "1.10.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task Raqamli_solishtirish_teskari_yonalishda_ham_togri()
    {
        var checker = CheckerFor("v1.10.0", currentVersion: "1.9.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.10.0", result.LatestVersion);
    }

    [Theory]
    [InlineData("v1.2.0")]
    [InlineData("V1.2.0")]
    [InlineData("1.2.0")]
    public async Task V_prefiksi_bilan_va_prefikssiz_teglar_bir_xil_oqiladi(string tag)
    {
        var checker = CheckerFor(tag, currentVersion: "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion);
    }

    [Theory]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1.2.0", "1.2")]
    [InlineData("v1.2", "1.2.0.0")]
    public async Task Bir_xil_versiyaning_turli_yozilishi_teng_deb_qaraladi(
        string tag, string currentVersion)
    {
        var checker = CheckerFor(tag, currentVersion);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task Semver_qoshimchali_teg_raqamli_ozagi_boyicha_solishtiriladi()
    {
        var checker = CheckerFor("v1.3.0-beta.1", currentVersion: "1.2.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.3.0-beta.1", result.LatestVersion);
    }

    /// <summary>Redirect manzilidagi kodlangan belgilar ochiladi ("%2B" → "+").</summary>
    [Fact]
    public async Task Kodlangan_teg_togri_ochiladi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? Text(HttpStatusCode.Forbidden, "{\"message\":\"rate limit\"}")
            : Redirect("v1.2.0%2Bbuild7"));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0+build7", result.LatestVersion);
    }

    // -----------------------------------------------------------------
    // 2. API cheklovi — ENG MUHIM HOLAT
    // -----------------------------------------------------------------

    /// <summary>
    /// Foydalanuvchining haqiqiy holati: NAT ortidagi IP uchun API cheklovi tugagan
    /// (403), ammo redirect ishlaydi. Yangilanish BARIBIR aniqlanishi, izoh bo'sh
    /// bo'lishi va holat <see cref="UpdateStatus.Failed"/> BO'LMASLIGI kerak.
    /// </summary>
    [Fact]
    public async Task Redirect_ishlasa_API_403_bolsa_ham_yangilanish_aniqlanadi()
    {
        var handler = new FakeHandler(request =>
        {
            if (!IsApi(request))
                return Redirect("v1.2.0");

            var response = Text(
                HttpStatusCode.Forbidden,
                "{\"message\":\"API rate limit exceeded for 213.230.78.152.\"}");
            response.Headers.TryAddWithoutValidation("x-ratelimit-remaining", "0");
            return response;
        });

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotEqual(UpdateStatus.Failed, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion);
        Assert.Equal(RepoUrl + "/releases/tag/v1.2.0", result.ReleaseUrl);
        Assert.Null(result.ReleaseNotes);
        Assert.DoesNotContain("chegara", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>API cheklangan bo'lsa ham "yangilanish yo'q" holati to'g'ri aniqlanadi.</summary>
    [Fact]
    public async Task Redirect_ishlasa_API_403_bolsa_ham_UpToDate_aniqlanadi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? Text(HttpStatusCode.Forbidden, "{\"message\":\"API rate limit exceeded\"}")
            : Redirect("v1.0.0"));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.Equal("1.0.0", result.LatestVersion);
    }

    /// <summary>Versiya bir xil bo'lsa API'ga umuman murojaat qilinmaydi — izoh kerak emas.</summary>
    [Fact]
    public async Task UpToDate_holatda_API_ga_sorov_yuborilmaydi()
    {
        var checker = CheckerFor("v1.0.0", "1.0.0", "Izoh", out var handler);

        await checker.CheckAsync();

        Assert.DoesNotContain(handler.Requests, IsApi);
    }

    /// <summary>Yangilanish bo'lsa va API ishlasa — izoh qo'shiladi.</summary>
    [Fact]
    public async Task Yangilanish_bolsa_API_dan_reliz_izohi_olinadi()
    {
        var checker = CheckerFor("v1.2.0", "1.0.0", "Yangi imkoniyatlar.", out var handler);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("Yangi imkoniyatlar.", result.ReleaseNotes);
        Assert.Contains(handler.Requests, IsApi);
    }

    /// <summary>API buzuq JSON qaytarsa ham natija buzilmaydi — izoh shunchaki bo'lmaydi.</summary>
    [Fact]
    public async Task API_buzuq_JSON_qaytarsa_izoh_olinmaydi_lekin_natija_saqlanadi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? Text(HttpStatusCode.OK, "{ bu JSON emas ")
            : Redirect("v1.2.0"));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Null(result.ReleaseNotes);
    }

    /// <summary>Izoh so'rovidagi tarmoq xatosi ham natijani buzmaydi.</summary>
    [Fact]
    public async Task Izoh_sorovidagi_tarmoq_xatosi_natijani_buzmaydi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? throw new HttpRequestException("No such host")
            : Redirect("v1.2.0"));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Null(result.ReleaseNotes);
    }

    /// <summary>Reliz izohi juda uzun bo'lsa qisqartiriladi.</summary>
    [Fact]
    public async Task Uzun_reliz_izohi_qisqartiriladi()
    {
        var checker = CheckerFor("v2.0.0", "1.0.0", new string('a', 900));

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.ReleaseNotes);
        Assert.True(result.ReleaseNotes!.Length < 500, "Izoh qisqartirilishi kerak edi.");
    }

    // -----------------------------------------------------------------
    // 3. Xatolarni chiroyli ishlash — istisno TASHLANMAYDI
    // -----------------------------------------------------------------

    /// <summary>
    /// Repoda hali reliz yo'q: <c>releases/latest</c> 302 emas, 404 qaytaradi.
    /// Bu xato emas, xotirjam holat.
    /// </summary>
    [Fact]
    public async Task Reliz_yoq_bolsa_404_NoRelease_qaytaradi_va_istisno_tashlamaydi()
    {
        var checker = CheckerForAll(
            HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}", "1.0.0", out _);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.NoRelease, result.Status);
        Assert.Null(result.LatestVersion);
        Assert.Contains("reliz", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.ReleaseUrl));
    }

    /// <summary>404 holatida API'ga ortiqcha so'rov yuborilmaydi.</summary>
    [Fact]
    public async Task Reliz_yoq_holatda_API_ga_sorov_yuborilmaydi()
    {
        var checker = CheckerForAll(
            HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}", "1.0.0", out var handler);

        await checker.CheckAsync();

        Assert.DoesNotContain(handler.Requests, IsApi);
    }

    /// <summary>
    /// Redirect javobida <c>Location</c> yo'q — yiqilmaydi, API zaxira sifatida sinaladi.
    /// </summary>
    [Fact]
    public async Task Redirectda_Location_yoq_bolsa_yiqilmaydi_va_API_zaxira_boladi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? Text(HttpStatusCode.OK, ReleaseJson("v1.2.0"))
            : new HttpResponseMessage(HttpStatusCode.Found));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion);
        Assert.Contains(handler.Requests, IsApi);
    }

    /// <summary>Redirect manzili tushunarsiz bo'lsa ham yiqilmaydi.</summary>
    [Fact]
    public async Task Redirect_manzili_tushunarsiz_bolsa_yiqilmaydi()
    {
        var handler = new FakeHandler(request =>
        {
            if (IsApi(request))
                return Text(HttpStatusCode.Forbidden, "{\"message\":\"rate limit\"}");

            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.TryAddWithoutValidation("Location", "https://example.com/boshqa-sahifa");
            return response;
        });

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>Ikkala usul ham cheklovga uchrasa — tushunarli xabar.</summary>
    [Fact]
    public async Task Sorov_chegarasiga_yetilganda_tushunarli_xabar_beriladi()
    {
        var handler = new FakeHandler(_ =>
        {
            var response = Text(
                HttpStatusCode.Forbidden, "{\"message\":\"API rate limit exceeded for 1.2.3.4.\"}");
            response.Headers.TryAddWithoutValidation("x-ratelimit-remaining", "0");
            return response;
        });

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Contains("60", result.Message);
    }

    /// <summary>Redirect ham, API ham ishlamasa — Failed, istisno tashlanmaydi.</summary>
    [Fact]
    public async Task Tarmoq_xatosi_Failed_qaytaradi_va_istisno_tashlamaydi()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("No such host"));
        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Contains("Internet", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Kutish_vaqti_tugasa_Failed_qaytaradi_va_istisno_tashlamaydi()
    {
        // Handler tashqi bekor qilishsiz TaskCanceledException tashlaydi — bu timeout holati.
        var handler = new FakeHandler(_ => throw new TaskCanceledException("Timeout"));
        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Contains("vaqti", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Redirect ishlamadi, API esa buzuq JSON qaytardi.</summary>
    [Fact]
    public async Task Buzuq_JSON_da_yiqilmaydi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? Text(HttpStatusCode.OK, "{ bu JSON emas ")
            : Text(HttpStatusCode.BadGateway));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>Zaxira API javobida teg maydoni yo'q.</summary>
    [Fact]
    public async Task Teg_maydoni_yoq_bolsa_yiqilmaydi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? Text(HttpStatusCode.OK, "{\"name\":\"Reliz\"}")
            : Text(HttpStatusCode.BadGateway));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Contains("teg", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("release-2026")]
    [InlineData("latest")]
    [InlineData("v")]
    public async Task Semver_bolmagan_teg_da_yiqilmaydi(string tag)
    {
        var checker = CheckerFor(tag, currentVersion: "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>Redirect ham, zaxira API ham kutilmagan kod qaytarsa — Failed.</summary>
    [Fact]
    public async Task Kutilmagan_server_xatosi_Failed_qaytaradi()
    {
        var checker = CheckerForAll(
            HttpStatusCode.InternalServerError, "Internal Server Error", "1.0.0", out _);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Contains("500", result.Message);
    }

    // -----------------------------------------------------------------
    // 4. So'rovlarning o'zi to'g'ri yuborilishi
    // -----------------------------------------------------------------

    /// <summary>Birinchi so'rov — aynan redirect beruvchi sahifaga, HEAD usulida.</summary>
    [Fact]
    public async Task Birinchi_sorov_releases_latest_sahifasiga_HEAD_bilan_yuboriladi()
    {
        var checker = CheckerFor("v1.0.0", "1.0.0", null, out var handler);

        await checker.CheckAsync();

        var first = handler.Requests[0];
        Assert.Equal(HttpMethod.Head, first.Method);
        Assert.Equal(LatestUrl, first.RequestUri!.ToString());
    }

    /// <summary>GitHub User-Agent'siz so'rovni rad etadi — sarlavha HAR IKKALA so'rovda bo'lishi shart.</summary>
    [Fact]
    public async Task Har_ikkala_sorovda_ham_UserAgent_yuboriladi()
    {
        var checker = CheckerFor("v2.0.0", "1.0.0", "Izoh", out var handler);

        await checker.CheckAsync();

        Assert.Equal(2, handler.Requests.Count);

        foreach (var request in handler.Requests)
        {
            Assert.True(request.Headers.TryGetValues("User-Agent", out var userAgent));
            Assert.Contains("DarsJadvali", string.Join(' ', userAgent!));
        }
    }

    /// <summary>API so'rovida GitHub talab qiladigan sarlavhalar bo'ladi.</summary>
    [Fact]
    public async Task API_sorovida_Accept_sarlavhasi_yuboriladi()
    {
        var checker = CheckerFor("v2.0.0", "1.0.0", "Izoh", out var handler);

        await checker.CheckAsync();

        var apiRequest = Assert.Single(handler.Requests, IsApi);

        Assert.True(apiRequest.Headers.TryGetValues("Accept", out var accept));
        Assert.Contains("application/vnd.github+json", string.Join(' ', accept!));
    }

    /// <summary>Standart manzillar — loyihaning o'z repozitoriysi.</summary>
    [Fact]
    public async Task Standart_manzillar_loyiha_repozitoriysiga_ishora_qiladi()
    {
        var checker = CheckerFor("v2.0.0", "1.0.0", "Izoh", out var handler);

        await checker.CheckAsync();

        Assert.Equal(LatestUrl, handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(ApiUrl, handler.Requests[1].RequestUri!.ToString());
    }

    /// <summary>Foydalanuvchi bekor qilsa — bu xato emas, istisno yuqoriga uzatiladi.</summary>
    [Fact]
    public async Task Tashqi_bekor_qilish_yuqoriga_uzatiladi()
    {
        var handler = new FakeHandler(_ => Redirect("v1.0.0"));
        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => checker.CheckAsync(cts.Token));
    }

    /// <summary>Ishlab chiqarish mijozi redirect'ni AVTOMATIK KUZATMASLIGI shart.</summary>
    [Fact]
    public void Ishlab_chiqarish_mijozi_redirectni_kuzatmaydi()
    {
        using var handler = GitHubUpdateChecker.CreateHandler();
        using var client = GitHubUpdateChecker.CreateHttpClient();

        Assert.False(handler.AllowAutoRedirect, "Location sarlavhasi o'qilishi uchun redirect kuzatilmasligi kerak.");
        Assert.Equal(GitHubUpdateChecker.RequestTimeout, client.Timeout);
    }
}
