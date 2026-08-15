using DarsJadvali.Infrastructure.DependencyInjection;
using Xunit;

namespace DarsJadvali.Tests.Security;

/// <summary>
/// W-01: veb qobiqdagi barcha endpointlar anonim edi — DELETE ham. Endi yozuv
/// so'rovlari kalitsiz o'tmaydi (veb qatlam bu qarorni 401 ga aylantiradi).
/// </summary>
public class ApiKeyAuthorizationTests
{
    private const string Key = "juda-uzun-tasodifiy-kalit-0123456789";

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("delete")]
    public void Yozuv_usullari_yozuv_deb_taniladi(string method)
        => Assert.True(LocalApiKey.IsWriteMethod(method));

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData(null)]
    public void Oqish_usullari_yozuv_emas(string? method)
        => Assert.False(LocalApiKey.IsWriteMethod(method));

    /// <summary>Kalitsiz DELETE — 401 (aynan shu holat audit topgan xavf edi).</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void Kalitsiz_yozuv_sorovi_rad_etiladi(string method)
    {
        Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate(method, null, Key));
        Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate(method, string.Empty, Key));
    }

    [Fact]
    public void Notogri_kalit_bilan_yozuv_sorovi_rad_etiladi()
        => Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate("DELETE", "boshqa-kalit", Key));

    /// <summary>Kalitning bir qismi ham yetarli emas (prefiks moslik bo'lmaydi).</summary>
    [Fact]
    public void Kalitning_qismi_yetarli_emas()
        => Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate("POST", Key[..10], Key));

    [Fact]
    public void Togri_kalit_bilan_yozuv_sorovi_otadi()
        => Assert.Equal(ApiKeyDecision.Allow, LocalApiKey.Evaluate("POST", Key, Key));

    /// <summary>Serverda kalit sozlanmagan bo'lsa yozuvga umuman ruxsat yo'q.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Server_kaliti_sozlanmagan_bolsa_yozuv_rad_etiladi(string? expected)
        => Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate("POST", "istalgan", expected));

    /// <summary>O'qish standart holatda erkin — sahifaning o'zi shu server orqali ochiladi.</summary>
    [Fact]
    public void Oqish_standart_holatda_kalitsiz_otadi()
        => Assert.Equal(ApiKeyDecision.Allow, LocalApiKey.Evaluate("GET", null, Key));

    /// <summary>Sozlama yoqilsa o'qish ham kalit talab qiladi.</summary>
    [Fact]
    public void Oqish_uchun_kalit_talab_qilinsa_kalitsiz_sorov_rad_etiladi()
    {
        Assert.Equal(
            ApiKeyDecision.Unauthorized,
            LocalApiKey.Evaluate("GET", null, Key, requireKeyForReads: true));

        Assert.Equal(
            ApiKeyDecision.Allow,
            LocalApiKey.Evaluate("GET", Key, Key, requireKeyForReads: true));
    }

    // -----------------------------------------------------------------
    // Kalitning o'zi
    // -----------------------------------------------------------------

    [Fact]
    public void Yaratilgan_kalit_har_safar_boshqacha_va_yetarlicha_uzun()
    {
        var first = LocalApiKey.Generate();
        var second = LocalApiKey.Generate();

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 32, "Kalit qisqa bo'lmasligi kerak: " + first.Length);
        Assert.DoesNotContain("=", first, StringComparison.Ordinal);
    }

    /// <summary>Birinchi ishga tushishda kalit yaratiladi, keyingilarida AYNI kalit o'qiladi.</summary>
    [Fact]
    public void Kalit_faylda_saqlanadi_va_qayta_ishlatiladi()
    {
        var folder = Path.Combine(Path.GetTempPath(), "darsjadvali-tests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, LocalApiKey.KeyFileName);

        try
        {
            var (created, wasCreated) = LocalApiKey.LoadOrCreate(file);

            Assert.True(wasCreated);
            Assert.True(File.Exists(file));

            var (loaded, wasCreatedAgain) = LocalApiKey.LoadOrCreate(file);

            Assert.False(wasCreatedAgain);
            Assert.Equal(created, loaded);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
