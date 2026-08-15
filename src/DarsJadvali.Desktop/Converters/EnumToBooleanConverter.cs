using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DarsJadvali.Desktop.Converters;

/// <summary>
/// Enum qiymati parametrga tengmi. Semantik holatni XAML sinfiga (Classes) bog'lash uchun:
/// <c>Classes.header="{Binding State, Converter={StaticResource EnumToBoolean}, ConverterParameter=Header}"</c>.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    /// <summary>XAML'siz ishlatish uchun tayyor nusxa.</summary>
    public static readonly EnumToBooleanConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string expected)
        {
            return false;
        }

        return string.Equals(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
