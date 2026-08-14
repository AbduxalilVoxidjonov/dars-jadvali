namespace DarsJadvali.UI.Models;

/// <summary>Rang tanlash ro'yxatidagi bitta variant.</summary>
public sealed record ColorOption(string Name, string Code)
{
    public override string ToString() => Name;
}

/// <summary>Tayyor ranglar to'plami.</summary>
public static class ColorPalette
{
    public static IReadOnlyList<ColorOption> All { get; } = new List<ColorOption>
    {
        new("Ko'k", "#1976D2"),
        new("To'q ko'k", "#303F9F"),
        new("Moviy", "#0097A7"),
        new("Yashil", "#388E3C"),
        new("Yashil-moviy", "#00796B"),
        new("Zaytun", "#827717"),
        new("Sariq", "#F9A825"),
        new("To'q sariq", "#F57C00"),
        new("Qizil", "#D32F2F"),
        new("Pushti", "#C2185B"),
        new("Binafsha", "#7B1FA2"),
        new("Siyoh binafsha", "#512DA8"),
        new("Jigarrang", "#5D4037"),
        new("Kulrang", "#455A64"),
    };

    /// <summary>Berilgan kodga mos variantni topadi, topilmasa birinchisini qaytaradi.</summary>
    public static ColorOption Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return All[0];
        }

        foreach (var option in All)
        {
            if (string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return All[0];
    }
}
