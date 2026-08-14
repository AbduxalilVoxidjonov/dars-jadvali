using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DarsJadvali.Desktop.Converters;

/// <summary>
/// Matn bo'sh bo'lmasa true. Avalonia'da <c>Visibility</c> yo'q —
/// <c>IsVisible="{Binding Xato, Converter={StaticResource StringNotEmpty}}"</c> ko'rinishida ishlating.
/// </summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
    public static readonly StringNotEmptyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}
