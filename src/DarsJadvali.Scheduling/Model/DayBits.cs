using System.Numerics;
using System.Runtime.CompilerServices;

namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// Bir kunning band-bitlari ustidagi agregatlar (02-asc-.., 4.1 "DayStats" va 5.2 "v_k" funksiyalari).
/// Hech qanday kesh saqlanmaydi — hammasi to'g'ridan-to'g'ri occupancy bitmask'idan hisoblanadi.
/// Shu sabab delta va to'liq baholash HAR DOIM bir xil natija beradi (kesh desinxronizatsiyasi imkonsiz).
/// </summary>
public static class DayBits
{
    /// <summary>Kundagi band darslar soni.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(ulong bits) => BitOperations.PopCount(bits);

    /// <summary>Birinchi band dars raqami, bo'sh kunda -1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int First(ulong bits) => bits == 0UL ? -1 : BitOperations.TrailingZeroCount(bits);

    /// <summary>Oxirgi band dars raqami, bo'sh kunda -1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Last(ulong bits) => bits == 0UL ? -1 : 63 - BitOperations.LeadingZeroCount(bits);

    /// <summary>
    /// Oynalar (windows/gaps) soni: birinchi va oxirgi dars orasidagi bo'sh soatlar.
    /// C-CLS-01, C-TCH-01/02.
    /// </summary>
    public static int Gaps(ulong bits)
    {
        if (bits == 0UL) return 0;
        int first = BitOperations.TrailingZeroCount(bits);
        int last = 63 - BitOperations.LeadingZeroCount(bits);
        return (last - first + 1) - BitOperations.PopCount(bits);
    }

    /// <summary>Eng uzun uzluksiz dars ketma-ketligi.</summary>
    public static int MaxRun(ulong bits)
    {
        int best = 0;
        ulong b = bits;
        while (b != 0UL)
        {
            int t = BitOperations.TrailingZeroCount(b);
            ulong shifted = b >> t;
            int run = BitOperations.TrailingZeroCount(~shifted);
            if (run > best) best = run;
            b = ClearRun(b, t, run);
        }
        return best;
    }

    /// <summary>
    /// C-TCH-10 — ketma-ket darslar chegarasi. Kvadratik jarima:
    /// <c>v = Sum_run max(0, len(run) - maxConsec)^2</c> (02-asc-.., 5.2).
    /// Kvadrat — adolatlilik uchun: bitta o'qituvchida 4 ortiqcha, 4 o'qituvchida bittadan yomonroq.
    /// </summary>
    public static long ConsecutivePenalty(ulong bits, int maxConsec)
    {
        if (maxConsec <= 0) return 0;
        long p = 0;
        ulong b = bits;
        while (b != 0UL)
        {
            int t = BitOperations.TrailingZeroCount(b);
            ulong shifted = b >> t;
            int run = BitOperations.TrailingZeroCount(~shifted);
            if (run > maxConsec)
            {
                long e = run - maxConsec;
                p += e * e;
            }
            b = ClearRun(b, t, run);
        }
        return p;
    }

    /// <summary>Uzluksiz ketma-ketliklar (bloklar) soni.</summary>
    public static int RunCount(ulong bits)
    {
        int n = 0;
        ulong b = bits;
        while (b != 0UL)
        {
            int t = BitOperations.TrailingZeroCount(b);
            ulong shifted = b >> t;
            int run = BitOperations.TrailingZeroCount(~shifted);
            n++;
            b = ClearRun(b, t, run);
        }
        return n;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ClearRun(ulong b, int start, int run)
    {
        ulong runMask = run >= 64 ? ulong.MaxValue : ((1UL << run) - 1UL);
        return b & ~(runMask << start);
    }
}
