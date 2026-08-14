using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DarsJadvali.Desktop.Converters;

/// <summary>Qiymat null bo'lmasa true (tugmalarni yoqish uchun).</summary>
public sealed class NotNullToBooleanConverter : IValueConverter
{
    public static readonly NotNullToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
