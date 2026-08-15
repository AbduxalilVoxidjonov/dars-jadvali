using System.Numerics;

namespace DarsJadvali.Scheduling.Util;

/// <summary>
/// Xoshiro256** — tez, deterministik PRNG. <c>System.Random.Shared</c> ATAYLAB ishlatilmaydi:
/// bir xil seed → bayt-bayt bir xil natija (T-A-01) talabi shuni majbur qiladi.
/// </summary>
public sealed class Xoshiro256SS
{
    private ulong _s0, _s1, _s2, _s3;

    public Xoshiro256SS(int seed) : this(unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL)) { }

    public Xoshiro256SS(ulong seed)
    {
        _s0 = SplitMix64(ref seed);
        _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed);
        _s3 = SplitMix64(ref seed);
        if ((_s0 | _s1 | _s2 | _s3) == 0UL) _s0 = 0x9E3779B97F4A7C15UL;
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public ulong NextUInt64()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5UL, 7) * 9UL;
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>[0, bound) oralig'ida butun son.</summary>
    public int Next(int bound)
    {
        if (bound <= 0) return 0;
        // Lemire's method — moduldan yiroq, lekin deterministik.
        ulong m = (ulong)(uint)bound * (NextUInt64() >> 32);
        return (int)(m >> 32);
    }

    /// <summary>[min, max) oralig'ida butun son.</summary>
    public int Next(int min, int max) => min + Next(max - min);

    /// <summary>[0, 1) oralig'ida haqiqiy son.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Massivni joyida aralashtiradi (Fisher–Yates).</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void Shuffle(int[] arr, int count)
    {
        for (int i = count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
