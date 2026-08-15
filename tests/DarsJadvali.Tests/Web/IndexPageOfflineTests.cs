using System.Text.RegularExpressions;
using Xunit;

namespace DarsJadvali.Tests.Web;

/// <summary>
/// <c>wwwroot/index.html</c> — dastur bilan birga keladigan yagona sahifa.
/// U <b>internetsiz</b> ishlashi shart: maktabda tarmoq bo'lmasligi mumkin, tashqi
/// CDN esa sahifani "yuklanmoqda" holatida qoldiradi.
/// </summary>
public class IndexPageOfflineTests
{
    private static string Page => WebSources.Read("wwwroot", "index.html");

    /// <summary>Sahifada birorta tashqi manzil yo'q.</summary>
    [Fact]
    public void Sahifada_tashqi_manzil_yoq()
    {
        var external = Regex.Matches(Page, @"https?://[^\s""'<>)]+")
            .Select(m => m.Value)
            // XML nomlar fazosi (masalan svg xmlns) hech narsa YUKLAMAYDI.
            .Where(url => !url.StartsWith("http://www.w3.org/", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.True(external.Count == 0,
            "Sahifada tashqi manzil bor: " + string.Join(", ", external));
    }

    /// <summary>Tashqi skript, uslub yoki rasm ulanmagan.</summary>
    [Theory]
    [InlineData(@"<script[^>]*\ssrc\s*=")]
    [InlineData(@"<link[^>]*\shref\s*=\s*[""']\s*(https?:)?//")]
    [InlineData(@"<img[^>]*\ssrc\s*=\s*[""']\s*(https?:)?//")]
    [InlineData(@"@import\s+url\(")]
    public void Tashqi_resurs_ulanmagan(string pattern)
    {
        var match = Regex.Match(Page, pattern, RegexOptions.IgnoreCase);
        Assert.False(match.Success, "Tashqi resurs topildi: " + match.Value);
    }

    /// <summary>Uslub va skript sahifaning O'ZIDA (inline).</summary>
    [Fact]
    public void Uslub_va_skript_sahifa_ichida()
    {
        Assert.Contains("<style>", Page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<script>", Page, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sahifa yangi <c>Card</c> modelining endpointlariga ulangan.</summary>
    [Fact]
    public void Sahifa_yangi_board_endpointlarini_ishlatadi()
    {
        Assert.Contains("/board/axes", Page, StringComparison.Ordinal);
        Assert.Contains("/board/cards", Page, StringComparison.Ordinal);
        Assert.Contains("/board/unplaced", Page, StringComparison.Ordinal);
        Assert.Contains("/board/lock", Page, StringComparison.Ordinal);
        Assert.Contains("/board/place", Page, StringComparison.Ordinal);
        Assert.Contains("/board/generate", Page, StringComparison.Ordinal);
        Assert.Contains("/board/validate", Page, StringComparison.Ordinal);
        Assert.Contains("/board/print", Page, StringComparison.Ordinal);
    }

    /// <summary>
    /// To'r yangi modelning uchta belgisini ko'rsatadi: juft dars (rowspan),
    /// guruh yo'lakchalari va A/B nishoni.
    /// </summary>
    [Fact]
    public void Torda_juft_dars_guruh_va_hafta_nishoni_bor()
    {
        Assert.Contains("rowspan", Page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("weekLabel", Page, StringComparison.Ordinal);
        Assert.Contains("groupName", Page, StringComparison.Ordinal);
        Assert.Contains("shiftName", Page, StringComparison.Ordinal);
    }
}
