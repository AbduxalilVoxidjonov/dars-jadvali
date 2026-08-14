using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DarsJadvali.UI.Converters;

/// <summary>
/// null -&gt; Collapsed, aks holda Visible.
/// ConverterParameter="Invert" berilsa teskarisi.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null;

        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
