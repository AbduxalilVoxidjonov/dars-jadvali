using System.Globalization;
using System.Windows.Data;

namespace DarsJadvali.UI.Converters;

/// <summary>Qiymat null bo'lmasa true (tugmalarni yoqish uchun).</summary>
public sealed class NotNullToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
