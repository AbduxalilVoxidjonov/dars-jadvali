using System.Text.RegularExpressions;
using DarsJadvali.Infrastructure.DependencyInjection;
using Xunit;

namespace DarsJadvali.Tests.Web;

/// <summary>
/// Loyiha ildizini va veb qobiq fayllarini topadi.
/// </summary>
/// <remarks>
/// Sinov loyihasi <c>DarsJadvali.Web</c> ga havola qilmaydi (veb qobiq — ishga
/// tushiriladigan dastur, kutubxona emas), shuning uchun uning <b>manba matni</b>
/// tekshiriladi: yangi endpointlar qaysi guruhga yozilgani va sahifa tashqi resurs
/// yuklamasligi shu yo'l bilan isbotlanadi. Kalit qarorining o'zi
/// (<see cref="LocalApiKey.Evaluate"/>) haqiqiy kod bilan sinaladi.
/// </remarks>
internal static class WebSources
{
    /// <summary>Loyiha ildizi (<c>DarsJadvali.sln</c> turgan papka).</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Veb qobiq papkasi.</summary>
    public static string WebProject => Path.Combine(Root, "src", "DarsJadvali.Web");

    /// <summary>Fayl matnini o'qiydi.</summary>
    public static string Read(params string[] parts)
    {
        var path = Path.Combine(new[] { WebProject }.Concat(parts).ToArray());
        Assert.True(File.Exists(path), $"Fayl topilmadi: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DarsJadvali.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("DarsJadvali.sln topilmadi — loyiha ildizi aniqlanmadi.");
    }
}

/// <summary>
/// W-01 qoidasi YANGI (<c>Card</c> modeli) endpointlarida ham kuchda:
/// yozuv so'rovi kalitsiz 401 oladi.
/// </summary>
public class BoardEndpointSecurityTests
{
    private const string Key = "juda-uzun-tasodifiy-kalit-0123456789";

    private static readonly Regex WriteMap = new(
        @"group\.Map(?<method>Post|Put|Patch|Delete)\(""(?<route>[^""]*)""",
        RegexOptions.Compiled);

    private static string BoardSource => WebSources.Read("Endpoints", "BoardEndpoints.cs");

    /// <summary>Yangi to'r endpointlari orasida yozuv so'rovlari bor.</summary>
    [Fact]
    public void Yangi_yozuv_endpointlari_mavjud()
    {
        var routes = WriteMap.Matches(BoardSource)
            .Select(m => m.Groups["method"].Value.ToUpperInvariant() + " " + m.Groups["route"].Value)
            .ToList();

        Assert.Contains("POST /place", routes);
        Assert.Contains("POST /lock", routes);
        Assert.Contains("POST /generate", routes);
        Assert.Contains("DELETE /generate/{jobId}", routes);
    }

    /// <summary>
    /// Barcha yangi endpointlar <c>/api/board</c> guruhida — ya'ni kalit tekshiruvi
    /// (<c>UseApiKeyAuthorization</c>, u <c>/api</c> yo'lini qamrab oladi) ulardan
    /// chetlab o'tolmaydi.
    /// </summary>
    [Fact]
    public void Yangi_endpointlar_api_board_guruhida()
    {
        Assert.Matches(@"MapGroup\(\s*""/board""\s*\)", BoardSource);

        // Kalit tekshiruvidan ozod yagona yo'l — /api/security. Yangi kod u yerga yozmaydi.
        Assert.DoesNotContain("MapGroup(\"/security\")", BoardSource, StringComparison.Ordinal);

        var program = WebSources.Read("Program.cs");
        Assert.Contains("var api = app.MapGroup(\"/api\");", program, StringComparison.Ordinal);
        Assert.Contains("api.MapBoardEndpoints();", program, StringComparison.Ordinal);

        // Tartib muhim: kalit tekshiruvi endpointlardan OLDIN ulanadi.
        var guard = program.IndexOf("app.UseApiKeyAuthorization(security);", StringComparison.Ordinal);
        var mapped = program.IndexOf("api.MapBoardEndpoints();", StringComparison.Ordinal);
        Assert.True(guard >= 0 && mapped > guard, "Kalit tekshiruvi endpointlardan oldin bo'lishi kerak.");
    }

    /// <summary>Yangi endpointlarning yozuv usullari kalitsiz rad etiladi.</summary>
    [Theory]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public void Yangi_yozuv_endpointlari_kalitsiz_401_beradi(string method)
    {
        Assert.True(LocalApiKey.IsWriteMethod(method));
        Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate(method, null, Key));
        Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate(method, string.Empty, Key));
        Assert.Equal(ApiKeyDecision.Unauthorized, LocalApiKey.Evaluate(method, "boshqa-kalit", Key));
        Assert.Equal(ApiKeyDecision.Allow, LocalApiKey.Evaluate(method, Key, Key));
    }

    /// <summary>
    /// Chop etish endpointi qamrovsiz ishlamaydi (E-01 qoidasi yangi yo'lda ham).
    /// </summary>
    [Fact]
    public void Chop_etish_qamrovsiz_400_qaytaradi()
    {
        Assert.Contains("Qamrov ko'rsatilmagan", BoardSource, StringComparison.Ordinal);
        Assert.Contains("Status400BadRequest", BoardSource, StringComparison.Ordinal);
        Assert.Contains("Bir vaqtda ham sinf, ham o'qituvchi", BoardSource, StringComparison.Ordinal);
    }

    /// <summary>Dizayn tanlash bor va noma'lum kalit 400 beradi.</summary>
    [Fact]
    public void Chop_etish_dizayni_tanlanadi()
    {
        Assert.Contains("BuiltInPrintDesigns.Keys", BoardSource, StringComparison.Ordinal);
        Assert.Contains("Noma'lum dizayn", BoardSource, StringComparison.Ordinal);
        Assert.Contains("/designs", BoardSource, StringComparison.Ordinal);
    }

    /// <summary>Eski <c>/api/schedule</c> yo'li o'chirilmadi, lekin eskirgan deb belgilandi.</summary>
    [Fact]
    public void Eski_endpointlar_eskirgan_deb_belgilangan()
    {
        var old = WebSources.Read("Endpoints", "ScheduleEndpoints.cs");

        Assert.Contains("[Obsolete(", old, StringComparison.Ordinal);
        Assert.Contains("DeprecationHeader", old, StringComparison.Ordinal);
        Assert.Contains("successor-version", old, StringComparison.Ordinal);

        // Eski yo'l HALI ishlaydi (Desktop ko'chmoqda).
        Assert.Contains("api.MapScheduleEndpoints();", WebSources.Read("Program.cs"), StringComparison.Ordinal);
    }
}
