using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Common;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// U-01: tarmoqdan kelgan havola <c>Process.Start(UseShellExecute: true)</c> ga
/// tekshiruvsiz uzatilmasligi kerak.
/// </summary>
public sealed class ExternalUrlPolicyTests
{
    [Theory]
    [InlineData("https://github.com/AbduxalilVoxidjonov/dars-jadvali/releases/tag/v1.2.0")]
    [InlineData("https://github.com/AbduxalilVoxidjonov/dars-jadvali/releases")]
    [InlineData("https://www.github.com/AbduxalilVoxidjonov")]
    [InlineData("https://GitHub.com/AbduxalilVoxidjonov")]
    [InlineData("https://t.me/abduxalilvoxidjonov")]
    public void Ishonchli_havolalar_ochiladi(string url)
        => Assert.True(ExternalUrlPolicy.IsAllowed(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://github.com/x")]                        // https emas
    [InlineData("file:///Users/me/.ssh/id_rsa")]               // fayl tizimi
    [InlineData("ms-msdt:/id PCWDiagnostic")]                  // OS dastur sxemasi
    [InlineData("javascript:alert(1)")]
    [InlineData("https://zararli.example/releases")]           // begona host
    [InlineData("https://github.com.zararli.example/relizlar")] // o'xshash host
    [InlineData("https://github.com@zararli.example/x")]        // foydalanuvchi ma'lumoti bilan aldash
    [InlineData("not a url")]
    public void Ishonchsiz_havolalar_rad_etiladi(string? url)
        => Assert.False(ExternalUrlPolicy.IsAllowed(url));

    [Fact]
    public void Dasturning_oz_havolalari_oq_royxatda()
    {
        Assert.True(ExternalUrlPolicy.IsAllowed(AppInfo.TelegramUrl));
        Assert.True(ExternalUrlPolicy.IsAllowed(AppInfo.ReleasesUrl));
        Assert.True(ExternalUrlPolicy.IsAllowed(AppInfo.RepositoryUrl));
        Assert.True(ExternalUrlPolicy.IsAllowed(AppInfo.LatestReleaseUrl));
    }

    [Fact]
    public void Rad_etish_xabari_ozbekcha_va_manzilni_korsatadi()
    {
        var message = ExternalUrlPolicy.RejectionMessage("https://zararli.example");

        Assert.Contains("xavfsiz emas", message, StringComparison.Ordinal);
        Assert.Contains("https://zararli.example", message, StringComparison.Ordinal);
    }
}
