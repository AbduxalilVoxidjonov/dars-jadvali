using System.Globalization;
using System.Windows.Data;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.UI.Converters;

/// <summary>WeekDay qiymatini o'zbekcha nomga aylantiradi.</summary>
public sealed class WeekDayToUzbekConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is WeekDay day ? day.ToUzbek() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
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

        return Binding.DoNothing;
    }
}
