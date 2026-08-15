using System.Globalization;
using PdfSharp.Drawing;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>"#RRGGBB" ranglari bilan ishlash.</summary>
public static class PrintColor
{
    /// <summary>Rang matni to'g'rimi ("#RRGGBB").</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        if (text.Length != 7 || text[0] != '#')
            return false;

        for (var i = 1; i < 7; i++)
        {
            if (!Uri.IsHexDigit(text[i]))
                return false;
        }

        return true;
    }

    /// <summary>Matnni PDFsharp rangiga aylantiradi; noto'g'ri bo'lsa <paramref name="fallback"/>.</summary>
    /// <param name="value">"#RRGGBB".</param>
    /// <param name="fallback">Zaxira rang.</param>
    public static XColor Parse(string? value, XColor fallback)
    {
        if (!IsValid(value))
            return fallback;

        var text = value!.Trim();
        var r = byte.Parse(text.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = byte.Parse(text.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = byte.Parse(text.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return XColor.FromArgb(r, g, b);
    }

    /// <summary>Rang och (yorug') mi — matn rangini tanlashda kerak.</summary>
    /// <param name="color">Tekshiriladigan rang.</param>
    public static bool IsLight(XColor color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) > 150;

    /// <summary>
    /// Nomdan BARQAROR pastel rang hosil qiladi: bir xil fan har doim bir xil rangda chiqadi
    /// (hatto boshqa kompyuterda, boshqa tartibda ham).
    /// </summary>
    /// <param name="name">Fan/o'qituvchi nomi.</param>
    public static string FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "#EEEEEE";

        // FNV-1a — platformadan mustaqil (string.GetHashCode randomizatsiya qilinadi, yaramaydi).
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in name.Trim())
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            // Pastel: har bir kanal 190..250 oralig'ida — matn ustida o'qiladi.
            var r = 190 + (int)(hash % 61);
            var g = 190 + (int)((hash >> 8) % 61);
            var b = 190 + (int)((hash >> 16) % 61);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
