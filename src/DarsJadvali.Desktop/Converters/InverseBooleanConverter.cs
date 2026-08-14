using System.Globalization;
using Avalonia.Data.Converters;

namespace DarsJadvali.Desktop.Converters;

/// <summary>bool qiymatini teskarisiga aylantiradi.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
}
