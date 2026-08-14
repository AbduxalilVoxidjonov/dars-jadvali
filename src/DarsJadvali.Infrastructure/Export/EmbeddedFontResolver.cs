using System.Reflection;
using PdfSharp.Fonts;

namespace DarsJadvali.Infrastructure.Export;

/// <summary>
/// PDFsharp uchun shrift yechuvchi: shriftni tizimdan emas, assembly ichiga
/// o'rnatilgan (embedded) DejaVu Sans Condensed faylidan oladi.
/// Shu tufayli Windows/macOS/Linux da natija bir xil bo'ladi va o'zbek lotin
/// belgilari (oʻ — U+02BB, gʻ, ʼ — U+02BC) to'g'ri chiziladi.
/// </summary>
public sealed class EmbeddedFontResolver : IFontResolver
{
    /// <summary>Chizishda ishlatiladigan yagona shrift oilasi nomi.</summary>
    public const string FamilyName = "DejaVu Sans Condensed";

    private const string RegularFace = "DejaVuSansCondensed#Regular";
    private const string BoldFace = "DejaVuSansCondensed#Bold";

    private const string RegularResource = "DarsJadvali.Infrastructure.Export.Fonts.DejaVuSansCondensed.ttf";
    private const string BoldResource = "DarsJadvali.Infrastructure.Export.Fonts.DejaVuSansCondensed-Bold.ttf";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, byte[]> Cache = new(StringComparer.Ordinal);
    private static bool _installed;

    /// <summary>Yagona nusxa — PDFsharp global sozlamasi ham shu nusxani ishlatadi.</summary>
    public static EmbeddedFontResolver Instance { get; } = new();

    /// <summary>
    /// Resolver'ni PDFsharp global sozlamasiga bir marta o'rnatadi.
    /// Takroriy chaqiruvlar hech narsa qilmaydi (PDFsharp shriftdan foydalangandan
    /// keyin resolver'ni almashtirishga ruxsat bermaydi).
    /// </summary>
    public static void EnsureInstalled()
    {
        if (_installed)
            return;

        lock (Gate)
        {
            if (_installed)
                return;

            if (!ReferenceEquals(GlobalFontSettings.FontResolver, Instance))
                GlobalFontSettings.FontResolver = Instance;

            _installed = true;
        }
    }

    /// <inheritdoc />
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Qaysi oila so'ralmasin — bizda bitta to'liq qamrovli oila bor.
        _ = familyName;
        _ = isItalic;
        return new FontResolverInfo(isBold ? BoldFace : RegularFace);
    }

    /// <inheritdoc />
    public byte[]? GetFont(string faceName)
    {
        var resource = string.Equals(faceName, BoldFace, StringComparison.Ordinal)
            ? BoldResource
            : RegularResource;

        lock (Gate)
        {
            if (Cache.TryGetValue(resource, out var cached))
                return cached;

            var assembly = typeof(EmbeddedFontResolver).GetTypeInfo().Assembly;
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException(
                    $"Shrift resursi topilmadi: {resource}. " +
                    "csproj dagi EmbeddedResource yozuvini tekshiring.");

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            Cache[resource] = bytes;
            return bytes;
        }
    }
}
