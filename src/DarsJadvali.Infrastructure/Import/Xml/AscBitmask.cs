namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>
/// aSc bit-satrlarini (<c>days</c>, <c>weeks</c>, <c>terms</c>) <c>int</c> bitmask'ga o'giradi.
/// </summary>
/// <remarks>
/// <para><b>aSc semantikasi</b> (01-asc-data-model.md §3.4): satr <c>'0'</c>/<c>'1'</c>
/// belgilaridan iborat, <b>chapdan o'ngga</b>, indeks 0 = birinchi kun/hafta/chorak.</para>
/// <list type="table">
/// <item><term><c>"10000"</c></term><description>faqat dushanba → bit 0 → <c>1</c></description></item>
/// <item><term><c>"00100"</c></term><description>faqat chorshanba → bit 2 → <c>4</c></description></item>
/// <item><term><c>"11111"</c></term><description>har kuni → <c>31</c></description></item>
/// <item><term><c>"00000"</c></term><description>cheklov yo'q → <c>0</c></description></item>
/// </list>
/// <para>Bizning modelda ham <c>0</c> = "cheklov yo'q"
/// (<see cref="DarsJadvali.Domain.Enums.BitMask"/>), shuning uchun bu to'g'ridan-to'g'ri
/// mos tushadi.</para>
/// </remarks>
public static class AscBitmask
{
    /// <summary>Bir bitmask'da ruxsat etilgan maksimal bit soni (31 — <c>int</c> chegarasi).</summary>
    public const int MaxBits = 31;

    /// <summary>
    /// Bit-satrni <c>int</c> bitmask'ga o'giradi. Bo'sh yoki <c>null</c> satr → <c>0</c>.
    /// <c>'0'</c>/<c>'1'</c> dan boshqa belgilar e'tiborsiz qoldiriladi (pozitsiya saqlanadi).
    /// </summary>
    public static int ToMask(string? bits)
    {
        if (string.IsNullOrWhiteSpace(bits)) return 0;

        var mask = 0;
        var index = 0;

        foreach (var ch in bits)
        {
            if (ch is not ('0' or '1')) continue;
            if (index >= MaxBits) break;
            if (ch == '1') mask |= 1 << index;
            index++;
        }

        return mask;
    }

    /// <summary>
    /// Bit-satrning mazmunli uzunligi — nechta kun/hafta/chorak ta'riflanganini bildiradi.
    /// </summary>
    public static int Length(string? bits)
    {
        if (string.IsNullOrWhiteSpace(bits)) return 0;

        var count = 0;
        foreach (var ch in bits)
        {
            if (ch is '0' or '1') count++;
        }

        return Math.Min(count, MaxBits);
    }

    /// <summary>
    /// Maskdagi yoqilgan bit indekslari (0-based, o'sish tartibida).
    /// </summary>
    public static IEnumerable<int> Bits(int mask) => DarsJadvali.Domain.Enums.BitMask.Bits(mask);

    /// <summary>
    /// Maskni "tanlangan indekslar" ro'yxatiga aylantiradi. Mask <c>0</c> bo'lsa
    /// ("cheklov yo'q") <paramref name="fallbackCount"/> ta indeks
    /// (<c>0..fallbackCount-1</c>) qaytariladi.
    /// </summary>
    /// <remarks>
    /// Aynan shu qoida kartochkani choraklarga tarqatishda ishlatiladi:
    /// <c>terms="000"</c> = "istalgan chorak" → kartochka HAR bir chorak jadvaliga
    /// nusxalanadi.
    /// </remarks>
    public static IReadOnlyList<int> Selected(int mask, int fallbackCount)
    {
        if (mask != 0) return Bits(mask).ToList();
        if (fallbackCount <= 0) return Array.Empty<int>();
        return Enumerable.Range(0, Math.Min(fallbackCount, MaxBits)).ToList();
    }
}
