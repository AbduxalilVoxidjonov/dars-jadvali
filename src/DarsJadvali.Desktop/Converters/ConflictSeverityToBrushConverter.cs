using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DarsJadvali.Application.Validation;

namespace DarsJadvali.Desktop.Converters;

/// <summary>Konflikt darajasiga qarab rang beradi: Error — qizil, Warning — sariq.</summary>
/// <remarks>Parametr "Background" bo'lsa ochiq fon rangi qaytadi.</remarks>
public sealed class ConflictSeverityToBrushConverter : IValueConverter
{
    public static readonly ConflictSeverityToBrushConverter Instance = new();

    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xEF, 0x6C, 0x00));
    private static readonly SolidColorBrush ErrorBackground = new(Color.FromRgb(0xFF, 0xEB, 0xEE));
    private static readonly SolidColorBrush WarningBackground = new(Color.FromRgb(0xFF, 0xF8, 0xE1));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isError = value is ConflictSeverity severity && severity == ConflictSeverity.Error;
        var wantBackground = parameter is string p && p.Equals("Background", StringComparison.OrdinalIgnoreCase);

        if (wantBackground)
        {
            return isError ? ErrorBackground : WarningBackground;
        }

        return isError ? ErrorBrush : WarningBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
