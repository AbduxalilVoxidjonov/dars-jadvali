using System.Globalization;

namespace DarsJadvali.UI.Models;

/// <summary>"HH:mm" ko'rinishidagi vaqt matnini o'qish va yozish uchun yordamchi.</summary>
public static class TimeTextHelper
{
    private static readonly string[] Formats = { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" };

    /// <summary>TimeSpan ni "HH:mm" matniga aylantiradi.</summary>
    public static string ToText(TimeSpan value)
        => value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>Matnni TimeSpan ga aylantiradi. Format noto'g'ri bo'lsa false qaytadi.</summary>
    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace('.', ':');

        if (!TimeSpan.TryParseExact(normalized, Formats, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return value >= TimeSpan.Zero && value < TimeSpan.FromDays(1);
    }
}
