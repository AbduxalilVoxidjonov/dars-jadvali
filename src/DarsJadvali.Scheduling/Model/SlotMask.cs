using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// Vaqt slotlari ustidagi qat'iy o'lchamli bitset (8 x ulong = 512 slot).
/// Spetsifikatsiya: 02-asc-constraints-algorithm.md, 4.1-bo'lim ("Nega bitmask": to'qnashuv tekshiruvi O(1)).
/// Immutable <c>readonly struct</c> — boxing yo'q, stack'da yashaydi, 64 bayt.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SlotMask : IEquatable<SlotMask>
{
    /// <summary>ulong so'zlar soni.</summary>
    public const int WordCount = 8;

    /// <summary>Maksimal slot soni (Weeks * Days * Periods shundan oshmasligi kerak).</summary>
    public const int Capacity = WordCount * 64;

    private readonly ulong _w0, _w1, _w2, _w3, _w4, _w5, _w6, _w7;

    private SlotMask(ulong w0, ulong w1, ulong w2, ulong w3, ulong w4, ulong w5, ulong w6, ulong w7)
    {
        _w0 = w0; _w1 = w1; _w2 = w2; _w3 = w3; _w4 = w4; _w5 = w5; _w6 = w6; _w7 = w7;
    }

    /// <summary>Bo'sh maska.</summary>
    public static SlotMask Empty => default;

    private ReadOnlySpan<ulong> W
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _w0), WordCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SlotMask From(ReadOnlySpan<ulong> w)
        => new(w[0], w[1], w[2], w[3], w[4], w[5], w[6], w[7]);

    /// <summary><paramref name="index"/> so'zini qaytaradi (0..7).</summary>
    public ulong Word(int index) => W[index];

    /// <summary>Slot band/ruxsat etilganmi.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Test(int slot) => (W[slot >> 6] & (1UL << (slot & 63))) != 0UL;

    /// <summary>Bitni o'rnatgan yangi maska (immutable — <c>Set</c> nusxa qaytaradi).</summary>
    public SlotMask Set(int slot)
    {
        Span<ulong> t = stackalloc ulong[WordCount];
        W.CopyTo(t);
        t[slot >> 6] |= 1UL << (slot & 63);
        return From(t);
    }

    /// <summary>(day, period) bo'yicha bit o'rnatish — <see cref="TimeGrid"/> indeksatsiyasi bilan.</summary>
    public SlotMask Set(TimeGrid grid, int dayIndex, int period) => Set(grid.SlotOf(dayIndex, period));

    /// <summary>Bitni tozalagan yangi maska.</summary>
    public SlotMask Clear(int slot)
    {
        Span<ulong> t = stackalloc ulong[WordCount];
        W.CopyTo(t);
        t[slot >> 6] &= ~(1UL << (slot & 63));
        return From(t);
    }

    /// <summary><paramref name="start"/> dan boshlab <paramref name="length"/> ta ketma-ket bit o'rnatilgan maska.</summary>
    public static SlotMask Range(int start, int length)
    {
        Span<ulong> t = stackalloc ulong[WordCount];
        for (int i = 0; i < length; i++)
        {
            int s = start + i;
            t[s >> 6] |= 1UL << (s & 63);
        }
        return From(t);
    }

    /// <summary>0..count-1 bitlari yoqilgan to'liq maska.</summary>
    public static SlotMask FullTo(int count) => Range(0, count);

    public static SlotMask operator &(SlotMask a, SlotMask b) => new(
        a._w0 & b._w0, a._w1 & b._w1, a._w2 & b._w2, a._w3 & b._w3,
        a._w4 & b._w4, a._w5 & b._w5, a._w6 & b._w6, a._w7 & b._w7);

    public static SlotMask operator |(SlotMask a, SlotMask b) => new(
        a._w0 | b._w0, a._w1 | b._w1, a._w2 | b._w2, a._w3 | b._w3,
        a._w4 | b._w4, a._w5 | b._w5, a._w6 | b._w6, a._w7 | b._w7);

    public static SlotMask operator ^(SlotMask a, SlotMask b) => new(
        a._w0 ^ b._w0, a._w1 ^ b._w1, a._w2 ^ b._w2, a._w3 ^ b._w3,
        a._w4 ^ b._w4, a._w5 ^ b._w5, a._w6 ^ b._w6, a._w7 ^ b._w7);

    public static SlotMask operator ~(SlotMask a) => new(
        ~a._w0, ~a._w1, ~a._w2, ~a._w3, ~a._w4, ~a._w5, ~a._w6, ~a._w7);

    /// <summary><c>a &amp; ~b</c>.</summary>
    public SlotMask AndNot(SlotMask other) => this & ~other;

    /// <summary>Kesishadimi — O(1) to'qnashuv tekshiruvi.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(SlotMask o)
        => ((_w0 & o._w0) | (_w1 & o._w1) | (_w2 & o._w2) | (_w3 & o._w3)
          | (_w4 & o._w4) | (_w5 & o._w5) | (_w6 & o._w6) | (_w7 & o._w7)) != 0UL;

    /// <summary>Bo'shmi.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_w0 | _w1 | _w2 | _w3 | _w4 | _w5 | _w6 | _w7) == 0UL;
    }

    /// <summary>Yoqilgan bitlar soni — MRV evristikasi uchun domain hajmi.</summary>
    public int PopCount()
        => BitOperations.PopCount(_w0) + BitOperations.PopCount(_w1)
         + BitOperations.PopCount(_w2) + BitOperations.PopCount(_w3)
         + BitOperations.PopCount(_w4) + BitOperations.PopCount(_w5)
         + BitOperations.PopCount(_w6) + BitOperations.PopCount(_w7);

    /// <summary><paramref name="from"/> dan boshlab birinchi yoqilgan bit indeksi, yo'q bo'lsa -1.</summary>
    public int FirstSet(int from = 0)
    {
        if (from >= Capacity) return -1;
        var w = W;
        int wi = from >> 6;
        ulong cur = w[wi] & (ulong.MaxValue << (from & 63));
        while (true)
        {
            if (cur != 0UL) return (wi << 6) + BitOperations.TrailingZeroCount(cur);
            wi++;
            if (wi >= WordCount) return -1;
            cur = w[wi];
        }
    }

    /// <summary>Yoqilgan bit indekslarini ketma-ket qaytaradi.</summary>
    public void ForEachSet(Span<int> buffer, out int count)
    {
        count = 0;
        for (int s = FirstSet(); s >= 0 && count < buffer.Length; s = FirstSet(s + 1))
            buffer[count++] = s;
    }

    /// <summary>
    /// [start, start+count) oralig'idagi bitlarni bitta ulong sifatida ajratib oladi (count &lt;= 64).
    /// Kunlik agregatlar (oyna, ketma-ketlik, yuk) shu ustida hisoblanadi.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Extract(int start, int count)
    {
        var w = W;
        int wi = start >> 6;
        int off = start & 63;
        ulong v = w[wi] >> off;
        if (off != 0 && wi + 1 < WordCount)
            v |= w[wi + 1] << (64 - off);
        if (count < 64) v &= (1UL << count) - 1UL;
        return v;
    }

    public bool Equals(SlotMask other)
        => _w0 == other._w0 && _w1 == other._w1 && _w2 == other._w2 && _w3 == other._w3
        && _w4 == other._w4 && _w5 == other._w5 && _w6 == other._w6 && _w7 == other._w7;

    public override bool Equals(object? obj) => obj is SlotMask m && Equals(m);

    public override int GetHashCode()
    {
        ulong h = _w0;
        h = h * 1000003UL ^ _w1; h = h * 1000003UL ^ _w2;
        h = h * 1000003UL ^ _w3; h = h * 1000003UL ^ _w4;
        h = h * 1000003UL ^ _w5; h = h * 1000003UL ^ _w6;
        h = h * 1000003UL ^ _w7;
        return (int)(h ^ (h >> 32));
    }

    public static bool operator ==(SlotMask a, SlotMask b) => a.Equals(b);
    public static bool operator !=(SlotMask a, SlotMask b) => !a.Equals(b);

    public override string ToString()
    {
        Span<char> buf = stackalloc char[Capacity];
        for (int i = 0; i < Capacity; i++) buf[i] = Test(i) ? '1' : '0';
        return new string(buf).TrimEnd('0');
    }
}
