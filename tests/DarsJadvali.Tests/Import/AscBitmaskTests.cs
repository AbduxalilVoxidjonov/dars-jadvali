using DarsJadvali.Infrastructure.Import.Xml;
using Xunit;

namespace DarsJadvali.Tests.Import;

/// <summary>
/// aSc bit-satrlarining <c>int</c> bitmask'ga o'girilishi (01-asc-data-model.md §3.4).
/// </summary>
public class AscBitmaskTests
{
    [Theory]
    [InlineData("10000", 1)]      // faqat dushanba
    [InlineData("01000", 2)]      // faqat seshanba
    [InlineData("00100", 4)]      // faqat chorshanba
    [InlineData("11111", 31)]     // har kuni
    [InlineData("00000", 0)]      // cheklov yo'q
    [InlineData("11000", 3)]      // dushanba yoki seshanba
    [InlineData("10", 1)]         // A hafta
    [InlineData("01", 2)]         // B hafta
    [InlineData("11", 3)]         // ikkala hafta
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void ToMask_bit_satrni_toʻgʻri_oʻgiradi(string? bits, int expected)
    {
        Assert.Equal(expected, AscBitmask.ToMask(bits));
    }

    [Theory]
    [InlineData("10000", 5)]
    [InlineData("11", 2)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void Length_taʼriflangan_pozitsiyalar_sonini_beradi(string? bits, int expected)
    {
        Assert.Equal(expected, AscBitmask.Length(bits));
    }

    [Fact]
    public void ToMask_notoʻgʻri_belgilarni_eʼtiborsiz_qoldiradi()
    {
        // '0'/'1' dan boshqasi hisobga olinmaydi, lekin pozitsiya buzilmaydi.
        Assert.Equal(4, AscBitmask.ToMask("0 0 1 0 0"));
    }

    [Fact]
    public void Selected_nol_maskni_barcha_pozitsiyaga_yoyadi()
    {
        // "000" = cheklov yo'q → kartochka HAR chorakka nusxalanadi.
        Assert.Equal(new[] { 0, 1, 2 }, AscBitmask.Selected(0, 3));
    }

    [Fact]
    public void Selected_toʻldirilgan_maskni_oʻzicha_qaytaradi()
    {
        Assert.Equal(new[] { 0, 2 }, AscBitmask.Selected(AscBitmask.ToMask("101"), 3));
    }

    [Fact]
    public void Selected_boʻsh_fallback_bilan_boʻsh_roʻyxat_qaytaradi()
    {
        Assert.Empty(AscBitmask.Selected(0, 0));
    }
}
