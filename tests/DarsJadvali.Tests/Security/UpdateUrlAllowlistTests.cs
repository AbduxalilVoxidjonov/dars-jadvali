using System.Net;
using System.Text;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Infrastructure.Update;
using Xunit;

namespace DarsJadvali.Tests.Security;

/// <summary>
/// U-01: yangilanish tekshiruvida TARMOQDAN kelgan manzil tekshirilmasdan
/// tashqariga (foydalanuvchi brauzeriga) uzatilmasligi kerak.
/// </summary>
public class UpdateUrlAllowlistTests
{
    private const string RepoUrl = "https://github.com/AbduxalilVoxidjonov/dars-jadvali";

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private static bool IsApi(HttpRequestMessage request)
        => request.RequestUri!.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase);

    // -----------------------------------------------------------------
    // 1. Ro'yxatning o'zi
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("https://github.com/AbduxalilVoxidjonov/dars-jadvali/releases/tag/v1.2.0")]
    [InlineData("https://www.github.com/AbduxalilVoxidjonov/dars-jadvali/releases")]
    [InlineData("https://api.github.com/repos/AbduxalilVoxidjonov/dars-jadvali/releases/latest")]
    [InlineData("https://objects.githubusercontent.com/fayl.zip")]
    [InlineData("HTTPS://GITHUB.COM/AbduxalilVoxidjonov/dars-jadvali/releases/tag/v1.2.0")]
    public void Ruxsat_etilgan_manzillar_qabul_qilinadi(string url)
        => Assert.True(GitHubUpdateChecker.IsAllowedReleaseUrl(url), url);

    [Theory]
    [InlineData(null)]                                              // manzil yo'q
    [InlineData("")]                                                // bo'sh
    [InlineData("/releases/tag/v1.2.0")]                            // nisbiy
    [InlineData("http://github.com/a/b/releases/tag/v1.2.0")]       // https emas
    [InlineData("ftp://github.com/a/b")]                            // begona sxema
    [InlineData("javascript:alert(1)")]                             // skript
    [InlineData("file:///etc/passwd")]                              // lokal fayl
    [InlineData("https://example.com/releases/tag/v1.2.0")]         // begona host
    [InlineData("https://github.com.evil.uz/a/b")]                  // o'xshatilgan host
    [InlineData("https://evil.github.com/a/b")]                     // subdomen
    [InlineData("https://githubbcom/a/b")]                          // xato yozilgan host
    [InlineData("https://github.com@evil.uz/a/b")]                  // haqiqiy host yashirilgan
    public void Ruxsat_etilmagan_manzillar_rad_etiladi(string? url)
        => Assert.False(GitHubUpdateChecker.IsAllowedReleaseUrl(url), url ?? "(null)");

    // -----------------------------------------------------------------
    // 2. Tekshiruvchining o'zida qo'llanishi
    // -----------------------------------------------------------------

    /// <summary>302 begona saytga olib borsa — u reliz manzili sifatida ISHLATILMAYDI.</summary>
    [Fact]
    public async Task Begona_saytga_yonaltirish_reliz_manzili_deb_qabul_qilinmaydi()
    {
        var handler = new FakeHandler(request =>
        {
            if (IsApi(request))
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{\"message\":\"rate limit\"}", Encoding.UTF8, "application/json"),
                };

            var response = new HttpResponseMessage(HttpStatusCode.Found);
            // Diqqat: manzilda "/releases/tag/" bo'lagi BOR — ya'ni faqat teg qidiruvi
            // bilan cheklansak, begona sayt o'tib ketardi.
            response.Headers.TryAddWithoutValidation(
                "Location", "https://evil.uz/releases/tag/v9.9.9");
            return response;
        });

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.DoesNotContain("evil.uz", result.ReleaseUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>API javobidagi begona <c>html_url</c> o'rniga o'z relizlar sahifamiz ishlatiladi.</summary>
    [Fact]
    public async Task API_javobidagi_begona_html_url_ishlatilmaydi()
    {
        var handler = new FakeHandler(request => IsApi(request)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"tag_name\":\"v2.0.0\",\"html_url\":\"https://evil.uz/zararli\",\"body\":\"Izoh\"}",
                    Encoding.UTF8,
                    "application/json"),
            }
            : new HttpResponseMessage(HttpStatusCode.BadGateway));

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.ReleaseUrl);
        Assert.DoesNotContain("evil.uz", result.ReleaseUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.True(GitHubUpdateChecker.IsAllowedReleaseUrl(result.ReleaseUrl));
    }

    /// <summary>To'g'ri (github.com) manzil esa o'zgarishsiz qoladi.</summary>
    [Fact]
    public async Task Togri_github_manzili_ozgarishsiz_qaytadi()
    {
        var handler = new FakeHandler(request =>
        {
            if (IsApi(request))
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };

            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.TryAddWithoutValidation("Location", RepoUrl + "/releases/tag/v3.0.0");
            return response;
        });

        var checker = new GitHubUpdateChecker(new HttpClient(handler), "1.0.0");

        var result = await checker.CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal(RepoUrl + "/releases/tag/v3.0.0", result.ReleaseUrl);
    }
}
