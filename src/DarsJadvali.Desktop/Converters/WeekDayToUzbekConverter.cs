using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.Converters;

/// <summary>WeekDay qiymatini o'zbekcha nomga aylantiradi.</summary>
public sealed class WeekDayToUzbekConverter : IValueConverter
{
    public static readonly WeekDayToUzbekConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is WeekDay day ? day.ToUzbek() : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text)
        {
            foreach (var day in WeekDayExtensions.All)
            {
                if (string.Equals(day.ToUzbek(), text, StringComparison.OrdinalIgnoreCase))
                {
                    return day;
                }
            }
        }

        return BindingOperations.DoNothing;
    }
}
