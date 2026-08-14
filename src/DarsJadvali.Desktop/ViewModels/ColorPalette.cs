using System;
using System.Collections.Generic;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Rang tanlash ro'yxatidagi bitta variant.</summary>
public sealed record ColorOption(string Name, string Code)
{
    public override string ToString() => Name;
}

/// <summary>Tayyor ranglar to'plami (WPF versiyasidagi ro'yxat bilan bir xil).</summary>
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

    /// <summary>
    /// Yangi yozuv uchun paletkadan bo'sh rang tanlaydi: avvalo umuman ishlatilmagan
    /// birinchi rangni qaytaradi, hammasi band bo'lsa — eng kam uchraydiganini
    /// (teng bo'lsa paletkadagi birinchisini). Natija barqaror: bir xil kirishga bir xil rang.
    /// </summary>
    /// <param name="usedCodes">Allaqachon ishlatilgan rang kodlari (null/bo'sh qiymatlar e'tiborsiz).</param>
    public static ColorOption NextFree(IEnumerable<string?>? usedCodes)
    {
        var counts = new int[All.Count];

        if (usedCodes is not null)
        {
            foreach (var code in usedCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                for (var i = 0; i < All.Count; i++)
                {
                    if (string.Equals(All[i].Code, code, StringComparison.OrdinalIgnoreCase))
                    {
                        counts[i]++;
                        break;
                    }
                }
            }
        }

        // Paletka tartibi bo'yicha eng kam ishlatilgani — 0 bo'lsa darhol shu qaytariladi.
        var bestIndex = 0;
        for (var i = 1; i < All.Count; i++)
        {
            if (counts[i] < counts[bestIndex])
            {
                bestIndex = i;
            }
        }

        return All[bestIndex];
    }
}
