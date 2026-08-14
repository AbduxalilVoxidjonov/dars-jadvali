using System.Net;
using System.Text;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Infrastructure.Update;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// <see cref="GitHubUpdateChecker"/> testlari. Hech biri tarmoqqa chiqmaydi —
/// HTTP qatlami soxta <see cref="HttpMessageHandler"/> bilan almashtirilgan.
/// </summary>
public class UpdateCheckerTests
{
    // -----------------------------------------------------------------
    // Yordamchilar
    // -----------------------------------------------------------------

    /// <summary>Oldindan tayyorlangan javobni qaytaradigan soxta HTTP handler.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        /// <summary>So'rovda kelgan sarlavhalarni tekshirish uchun saqlanadi.</summary>
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responder(request));
        }
    }

    /// <summary>Berilgan teg bilan GitHub reliz JSON'ini yasaydi.</summary>
    private static string ReleaseJson(string tag, string? notes = "Yangi imkoniyatlar.")
        => "{\"tag_name\":\"" + tag + "\"," +
           "\"name\":\"Reliz " + tag + "\"," +
           "\"html_url\":\"https://github.com/AbduxalilVoxidjonov/dars-jadvali/releases/tag/" + tag + "\"," +
           "\"body\":\"" + notes + "\"}";

    /// <summary>Berilgan JSON'ni 200 bilan qaytaradigan tekshiruvchi.</summary>
    private static GitHubUpdateChecker CheckerFor(string json, string currentVersion)
        => CheckerFor(json, HttpStatusCode.OK, currentVersion, out _);

    private static GitHubUpdateChecker CheckerFor(
        string body, HttpStatusCode status, string currentVersion, out FakeHandler handler)
    {
        var captured = new FakeHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        handler = captured;
        return new GitHubUpdateChecker(new HttpClient(captured), currentVersion);
    }

    // -----------------------------------------------------------------
    // 1. Versiyalarni solishtirish
    // -----------------------------------------------------------------

    [Fact]
    public async Task Yangi_versiya_chiqqan_bolsa_yangilanish_bor_deydi()
    {
        var checker = CheckerFor(ReleaseJson("v1.2.0"), currentVersion: "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion);
        Assert.Contains("1.2.0", result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.ReleaseUrl));
    }

    [Fact]
    public async Task Bir_xil_versiya_eng_songgi_deb_qaraladi()
    {
        var checker = CheckerFor(ReleaseJson("v1.2.0"), currentVersion: "1.2.0");

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
        var checker = CheckerFor(ReleaseJson("v1.9.0"), currentVersion: "1.10.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task Raqamli_solishtirish_teskari_yonalishda_ham_togri()
    {
        var checker = CheckerFor(ReleaseJson("v1.10.0"), currentVersion: "1.9.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.10.0", result.LatestVersion);
    }

    [Theory]
    [InlineData("v1.2.0")]
    [InlineData("V1.2.0")]
    [InlineData("1.2.0")]
    [InlineData(" v1.2.0 ")]
    public async Task V_prefiksi_bilan_va_prefikssiz_teglar_bir_xil_oqiladi(string tag)
    {
        var checker = CheckerFor(ReleaseJson(tag), currentVersion: "1.0.0");

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
        var checker = CheckerFor(ReleaseJson(tag), currentVersion);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task Semver_qoshimchali_teg_raqamli_ozagi_boyicha_solishtiriladi()
    {
        var checker = CheckerFor(ReleaseJson("v1.3.0-beta.1"), currentVersion: "1.2.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.3.0-beta.1", result.LatestVersion);
    }

    // -----------------------------------------------------------------
    // 2. Xatolarni chiroyli ishlash — istisno TASHLANMAYDI
    // -----------------------------------------------------------------

    /// <summary>Repoda hali reliz yo'q: bu xato emas, xotirjam holat.</summary>
    [Fact]
    public async Task Reliz_yoq_bolsa_404_NoRelease_qaytaradi_va_istisno_tashlamaydi()
    {
        var checker = CheckerFor(
            "{\"message\":\"Not Found\"}", HttpStatusCode.NotFound, "1.0.0", out _);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.NoRelease, result.Status);
        Assert.Null(result.LatestVersion);
        Assert.Contains("reliz", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.ReleaseUrl));
    }

    [Fact]
    public async Task Sorov_chegarasiga_yetilganda_tushunarli_xabar_beriladi()
    {
        var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    "{\"message\":\"API rate limit exceeded for 1.2.3.4.\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
            response.Headers.TryAddWithoutValidation("x-ratelimit-remaining", "0");
            return response;
        });

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Contains("60", result.Message);
    }

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

    [Fact]
    public async Task Buzuq_JSON_da_yiqilmaydi()
    {
        var checker = CheckerFor("{ bu JSON emas ", HttpStatusCode.OK, "1.0.0", out _);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task Teg_maydoni_yoq_bolsa_yiqilmaydi()
    {
        var checker = CheckerFor("{\"name\":\"Reliz\"}", HttpStatusCode.OK, "1.0.0", out _);

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
        var checker = CheckerFor(ReleaseJson(tag), currentVersion: "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task Kutilmagan_server_xatosi_Failed_qaytaradi()
    {
        var checker = CheckerFor(
            "Internal Server Error", HttpStatusCode.InternalServerError, "1.0.0", out _);

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Contains("500", result.Message);
    }

    // -----------------------------------------------------------------
    // 3. So'rovning o'zi to'g'ri yuborilishi
    // -----------------------------------------------------------------

    /// <summary>GitHub User-Agent'siz so'rovni rad etadi — sarlavha bo'lishi shart.</summary>
    [Fact]
    public async Task Sorovda_UserAgent_va_Accept_sarlavhalari_yuboriladi()
    {
        var checker = CheckerFor(ReleaseJson("v1.0.0"), HttpStatusCode.OK, "1.0.0", out var handler);

        await checker.CheckAsync();

        var request = Assert.IsType<HttpRequestMessage>(handler.LastRequest);

        Assert.True(request.Headers.TryGetValues("User-Agent", out var userAgent));
        Assert.Contains("DarsJadvali", string.Join(' ', userAgent!));

        Assert.True(request.Headers.TryGetValues("Accept", out var accept));
        Assert.Contains("application/vnd.github+json", string.Join(' ', accept!));
    }

    /// <summary>Standart manzil — loyihaning o'z repozitoriysi.</summary>
    [Fact]
    public async Task Standart_manzil_loyiha_repozitoriysiga_ishora_qiladi()
    {
        var checker = CheckerFor(ReleaseJson("v1.0.0"), HttpStatusCode.OK, "1.0.0", out var handler);

        await checker.CheckAsync();

        Assert.Equal(
            "https://api.github.com/repos/AbduxalilVoxidjonov/dars-jadvali/releases/latest",
            handler.LastRequest!.RequestUri!.ToString());
    }

    /// <summary>Foydalanuvchi bekor qilsa — bu xato emas, istisno yuqoriga uzatiladi.</summary>
    [Fact]
    public async Task Tashqi_bekor_qilish_yuqoriga_uzatiladi()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ReleaseJson("v1.0.0")),
        });

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => checker.CheckAsync(cts.Token));
    }

    /// <summary>Reliz izohi juda uzun bo'lsa qisqartiriladi.</summary>
    [Fact]
    public async Task Uzun_reliz_izohi_qisqartiriladi()
    {
        var longNotes = new string('a', 900);
        var checker = CheckerFor(ReleaseJson("v2.0.0", longNotes), currentVersion: "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.ReleaseNotes);
        Assert.True(result.ReleaseNotes!.Length < 500, "Izoh qisqartirilishi kerak edi.");
    }
}
