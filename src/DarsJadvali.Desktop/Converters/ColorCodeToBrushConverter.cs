using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DarsJadvali.Desktop.Converters;

/// <summary>"#RRGGBB" ko'rinishidagi rang kodini <see cref="IBrush"/> ga aylantiradi.</summary>
/// <remarks>
/// Parametr sifatida "Light" berilsa, rangning ochiq varianti qaytadi (katak foni uchun).
/// </remarks>
public sealed class ColorCodeToBrushConverter : IValueConverter
{
    /// <summary>XAML'siz ishlatish uchun tayyor nusxa.</summary>
    public static readonly ColorCodeToBrushConverter Instance = new();

    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SolidColorBrush Fallback = new(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value as string;
        if (string.IsNullOrWhiteSpace(code))
        {
            return Fallback;
        }

        var light = parameter is string p && p.Equals("Light", StringComparison.OrdinalIgnoreCase);
        var key = light ? code + "|L" : code;

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (!Color.TryParse(code, out var color))
            {
                return Fallback;
            }

            if (light)
            {
                color = Lighten(color, 0.72);
            }

            var brush = new SolidColorBrush(color);
            Cache[key] = brush;
            return brush;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;

    private static Color Lighten(Color color, double amount)
    {
        byte Mix(byte channel) => (byte)Math.Clamp(channel + ((255 - channel) * amount), 0, 255);
        return Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B));
    }
}
