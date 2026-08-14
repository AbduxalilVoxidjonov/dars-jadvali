using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DarsJadvali.UI.Converters;

/// <summary>"#RRGGBB" ko'rinishidagi rang kodini Brush ga aylantiradi.</summary>
public sealed class ColorCodeToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SolidColorBrush Fallback = CreateFrozen(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value as string;
        if (string.IsNullOrWhiteSpace(code))
        {
            return Fallback;
        }

        lock (Cache)
        {
            if (Cache.TryGetValue(code, out var cached))
            {
                return cached;
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(code);
                if (converted is Color color)
                {
                    // Katak foni uchun ochiqroq variant kerak bo'lsa "Light" parametri beriladi.
                    if (parameter is string p && p.Equals("Light", StringComparison.OrdinalIgnoreCase))
                    {
                        color = Lighten(color, 0.72);
                    }

                    var brush = CreateFrozen(color);
                    Cache[code] = brush;
                    return brush;
                }
            }
            catch (FormatException)
            {
                // Noto'g'ri kod — zaxira rang.
            }
            catch (NotSupportedException)
            {
                // Noto'g'ri kod — zaxira rang.
            }
        }

        return Fallback;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static Color Lighten(Color color, double amount)
    {
        byte Mix(byte channel) => (byte)Math.Clamp(channel + ((255 - channel) * amount), 0, 255);
        return Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B));
    }

    private static SolidColorBrush CreateFrozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
