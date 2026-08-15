using DarsJadvali.Scheduling.Model;
using Xunit;

namespace DarsJadvali.Scheduling.Tests;

public class SlotMaskTests
{
    [Fact]
    public void Set_And_Test_Work_Across_Word_Boundaries()
    {
        var m = SlotMask.Empty;
        int[] slots = { 0, 1, 63, 64, 127, 128, 255, 256, 511 };
        foreach (var s in slots) m = m.Set(s);

        foreach (var s in slots) Assert.True(m.Test(s), $"slot {s} yoqilgan bo'lishi kerak");
        Assert.False(m.Test(2));
        Assert.False(m.Test(510));
        Assert.Equal(slots.Length, m.PopCount());
    }

    [Fact]
    public void Clear_Removes_Only_Target_Bit()
    {
        var m = SlotMask.Empty.Set(10).Set(11).Set(12);
        m = m.Clear(11);
        Assert.True(m.Test(10));
        Assert.False(m.Test(11));
        Assert.True(m.Test(12));
        Assert.Equal(2, m.PopCount());
    }

    [Fact]
    public void Logical_Operators()
    {
        var a = SlotMask.Empty.Set(1).Set(2).Set(3);
        var b = SlotMask.Empty.Set(3).Set(4);

        Assert.Equal(1, (a & b).PopCount());
        Assert.Equal(4, (a | b).PopCount());
        Assert.Equal(2, a.AndNot(b).PopCount());
        Assert.True(a.Intersects(b));
        Assert.False(a.AndNot(b).Intersects(b));
        Assert.True(SlotMask.Empty.IsEmpty);
        Assert.False(a.IsEmpty);
    }

    [Fact]
    public void FirstSet_Iterates_All_Bits_In_Order()
    {
        var m = SlotMask.Empty.Set(5).Set(70).Set(300);
        var found = new List<int>();
        for (int s = m.FirstSet(); s >= 0; s = m.FirstSet(s + 1)) found.Add(s);
        Assert.Equal(new[] { 5, 70, 300 }, found);
    }

    [Fact]
    public void Range_Creates_Consecutive_Bits()
    {
        var m = SlotMask.Range(62, 5);
        Assert.Equal(5, m.PopCount());
        for (int i = 62; i < 67; i++) Assert.True(m.Test(i));
        Assert.False(m.Test(61));
        Assert.False(m.Test(67));
    }

    [Fact]
    public void Extract_Returns_Requested_Window()
    {
        var m = SlotMask.Empty.Set(70).Set(72).Set(75);
        ulong bits = m.Extract(70, 8);
        Assert.Equal(0b0010_0101UL, bits);
    }

    [Fact]
    public void Extract_Handles_Word_Boundary()
    {
        var m = SlotMask.Empty.Set(60).Set(63).Set(64).Set(65);
        ulong bits = m.Extract(60, 8);
        Assert.Equal(0b0011_1001UL, bits);
    }

    [Fact]
    public void Equality_And_Fingerprint()
    {
        var a = SlotMask.Empty.Set(3).Set(9);
        var b = SlotMask.Empty.Set(9).Set(3);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void FullTo_Sets_Exact_Count()
    {
        var m = SlotMask.FullTo(60);
        Assert.Equal(60, m.PopCount());
        Assert.True(m.Test(59));
        Assert.False(m.Test(60));
    }
}
