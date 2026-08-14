using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DarsJadvali.UI.Converters;

/// <summary>
/// Bo'sh matn -&gt; Collapsed, aks holda Visible.
/// ConverterParameter="Invert" berilsa teskarisi.
/// </summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasText = !string.IsNullOrWhiteSpace(value as string);

        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            hasText = !hasText;
        }

        return hasText ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
