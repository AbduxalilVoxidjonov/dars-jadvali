using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DarsJadvali.Application.Import;

namespace DarsJadvali.Desktop.Converters;

/// <summary>
/// Import xabarining darajasiga qarab rang beradi: xato — qizil, ogohlantirish —
/// to'q sariq, ma'lumot — ko'k.
/// </summary>
/// <remarks>
/// ViewModel'lar <c>IBrush</c> qaytarmaydi: ular faqat semantik
/// <see cref="ImportSeverity"/> ni beradi, rang shu yerda tanlanadi.
/// Parametr <c>"Background"</c> bo'lsa ochiq fon rangi qaytadi.
/// </remarks>
public sealed class ImportSeverityToBrushConverter : IValueConverter
{
    /// <summary>XAML'da <c>x:Static</c> orqali ishlatish uchun tayyor nusxa.</summary>
    public static readonly ImportSeverityToBrushConverter Instance = new();

    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xEF, 0x6C, 0x00));
    private static readonly SolidColorBrush InfoBrush = new(Color.FromRgb(0x15, 0x65, 0xC0));

    private static readonly SolidColorBrush ErrorBackground = new(Color.FromRgb(0xFF, 0xEB, 0xEE));
    private static readonly SolidColorBrush WarningBackground = new(Color.FromRgb(0xFF, 0xF8, 0xE1));
    private static readonly SolidColorBrush InfoBackground = new(Color.FromRgb(0xE3, 0xF2, 0xFD));

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var severity = value as ImportSeverity? ?? ImportSeverity.Info;
        var wantBackground = parameter is string p
                             && p.Equals("Background", StringComparison.OrdinalIgnoreCase);

        if (wantBackground)
        {
            return severity switch
            {
                ImportSeverity.Error => ErrorBackground,
                ImportSeverity.Warning => WarningBackground,
                _ => InfoBackground,
            };
        }

        return severity switch
        {
            ImportSeverity.Error => ErrorBrush,
            ImportSeverity.Warning => WarningBrush,
            _ => InfoBrush,
        };
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
