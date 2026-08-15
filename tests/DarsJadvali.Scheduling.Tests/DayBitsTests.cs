using DarsJadvali.Scheduling.Model;
using Xunit;

namespace DarsJadvali.Scheduling.Tests;

public class DayBitsTests
{
    [Theory]
    [InlineData(0b0000_0000UL, 0)]
    [InlineData(0b0000_0001UL, 0)]
    [InlineData(0b0000_0111UL, 0)]
    [InlineData(0b0010_0001UL, 4)]   // 1-dars va 6-dars → 4 ta oyna (T-S-01)
    [InlineData(0b0000_1001UL, 2)]
    [InlineData(0b0100_1011UL, 3)]
    public void Gaps_Counts_Windows_Between_First_And_Last(ulong bits, int expected)
        => Assert.Equal(expected, DayBits.Gaps(bits));

    [Fact]
    public void Count_First_Last()
    {
        ulong bits = 0b0010_1100UL;
        Assert.Equal(3, DayBits.Count(bits));
        Assert.Equal(2, DayBits.First(bits));
        Assert.Equal(5, DayBits.Last(bits));
        Assert.Equal(-1, DayBits.First(0UL));
    }

    [Fact]
    public void MaxRun_And_RunCount()
    {
        ulong bits = 0b1011_0111UL;   // 111 0 11 0 1
        Assert.Equal(3, DayBits.MaxRun(bits));
        Assert.Equal(3, DayBits.RunCount(bits));
    }

    /// <summary>T-S-02: 8 ta ketma-ket dars, maxConsec = 4 → (8-4)^2 = 16.</summary>
    [Fact]
    public void ConsecutivePenalty_Is_Quadratic()
    {
        ulong eight = 0b1111_1111UL;
        Assert.Equal(16, DayBits.ConsecutivePenalty(eight, 4));
        Assert.Equal(0, DayBits.ConsecutivePenalty(0b1111UL, 4));
        Assert.Equal(1, DayBits.ConsecutivePenalty(0b1_1111UL, 4));
    }

    /// <summary>
    /// T-S-09 (adolatlilik): bitta o'qituvchida 4 ortiqcha (16) &gt; 4 o'qituvchida bittadan (4x1 = 4).
    /// </summary>
    [Fact]
    public void QuadraticPenalty_Prefers_Spreading_Overload()
    {
        long concentrated = DayBits.ConsecutivePenalty(0b1111_1111UL, 4);      // 1 x (8-4)^2
        long spread = 4 * DayBits.ConsecutivePenalty(0b1_1111UL, 4);           // 4 x (5-4)^2
        Assert.True(spread < concentrated, $"{spread} < {concentrated}");
    }

    [Fact]
    public void ConsecutivePenalty_Sums_All_Runs()
    {
        // 6 ta ketma-ket, bo'shliq, 6 ta ketma-ket → 2 x (6-3)^2 = 18
        ulong bits = 0b0011_1111_0011_1111UL;
        Assert.Equal(18, DayBits.ConsecutivePenalty(bits, 3));
    }
}
