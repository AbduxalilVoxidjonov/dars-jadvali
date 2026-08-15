using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DarsJadvali.Desktop.Models;

namespace DarsJadvali.Desktop.Converters;

/// <summary>
/// Pozitsiya bahosini rangga aylantiradi — aSc'dagi <b>kulrang / ko'k / yashil</b>
/// (<c>03-asc-features-ux.md</c> §4.1).
/// </summary>
/// <remarks>
/// ViewModel <c>IBrush</c> qaytarmaydi (M-06): u faqat <see cref="PlacementRating"/> beradi,
/// rang esa shu yerda hal qilinadi.
/// <para>Parametrlar: <c>Background</c> — ochiq fon, <c>Border</c> — chegara rangi.</para>
/// </remarks>
public sealed class PlacementRatingToBrushConverter : IValueConverter
{
    /// <summary>XAML'siz ishlatish uchun tayyor nusxa.</summary>
    public static readonly PlacementRatingToBrushConverter Instance = new();

    // Kulrang — taqiqlangan.
    private static readonly IBrush ForbiddenBorder = Frozen(0x9E, 0x9E, 0x9E);
    private static readonly IBrush ForbiddenBackground = Frozen(0xE0, 0xE0, 0xE0);

    // Ko'k — ruxsat etilgan, lekin yaxshi emas.
    private static readonly IBrush AllowedBorder = Frozen(0x15, 0x65, 0xC0);
    private static readonly IBrush AllowedBackground = Frozen(0xE3, 0xF2, 0xFD);

    // Yashil — yaxshi pozitsiya.
    private static readonly IBrush PreferredBorder = Frozen(0x2E, 0x7D, 0x32);
    private static readonly IBrush PreferredBackground = Frozen(0xE8, 0xF5, 0xE9);

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PlacementRating rating)
        {
            return Brushes.Transparent;
        }

        var background = parameter is string p && p.Equals("Background", StringComparison.OrdinalIgnoreCase);

        return rating switch
        {
            PlacementRating.Forbidden => background ? ForbiddenBackground : ForbiddenBorder,
            PlacementRating.Allowed => background ? AllowedBackground : AllowedBorder,
            _ => background ? PreferredBackground : PreferredBorder,
        };
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;

    private static IBrush Frozen(byte r, byte g, byte b)
        => new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(r, g, b));
}
