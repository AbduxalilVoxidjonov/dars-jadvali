using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DarsJadvali.Application.Validation;

namespace DarsJadvali.UI.Converters;

/// <summary>Konflikt darajasiga qarab rang beradi: Error — qizil, Warning — sariq.</summary>
public sealed class ConflictSeverityToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ErrorBrush = Frozen(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly SolidColorBrush WarningBrush = Frozen(Color.FromRgb(0xEF, 0x6C, 0x00));
    private static readonly SolidColorBrush ErrorBackground = Frozen(Color.FromRgb(0xFF, 0xEB, 0xEE));
    private static readonly SolidColorBrush WarningBackground = Frozen(Color.FromRgb(0xFF, 0xF8, 0xE1));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isError = value is ConflictSeverity severity && severity == ConflictSeverity.Error;
        var wantBackground = parameter is string p && p.Equals("Background", StringComparison.OrdinalIgnoreCase);

        if (wantBackground)
        {
            return isError ? ErrorBackground : WarningBackground;
        }

        return isError ? ErrorBrush : WarningBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
