using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DarsJadvali.UI.Converters;

/// <summary>
/// true -&gt; Visible, false -&gt; Collapsed.
/// ConverterParameter="Invert" berilsa teskarisi.
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;

        if (IsInverted(parameter))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is Visibility v && v == Visibility.Visible;
        return IsInverted(parameter) ? !flag : flag;
    }

    private static bool IsInverted(object? parameter)
        => parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
}
