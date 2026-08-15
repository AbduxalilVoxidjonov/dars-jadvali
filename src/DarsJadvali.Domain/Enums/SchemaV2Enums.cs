namespace DarsJadvali.Domain.Enums;

/// <summary>
/// <c>CardOccurrence</c> da band qilinadigan resurs turi.
/// Qiymatlar DB'ga <c>int</c> bo'lib tushadi — o'zgartirilmaydi.
/// </summary>
public enum ResourceKind
{
    /// <summary>O'qituvchi.</summary>
    Teacher = 1,

    /// <summary>O'quvchilar guruhi (shu jumladan "Butun sinf" guruhi).</summary>
    StudentGroup = 2,

    /// <summary>Xona.</summary>
    Classroom = 3
}

/// <summary>
/// <c>TimeOff</c> (va kelajakda <c>ScheduleConstraint</c>) egasi bo'la oladigan obyekt turi.
/// </summary>
public enum ResourceOwnerKind
{
    /// <summary>O'qituvchi.</summary>
    Teacher = 1,

    /// <summary>O'quvchilar guruhi.</summary>
    StudentGroup = 2,

    /// <summary>Xona.</summary>
    Classroom = 3,

    /// <summary>Sinf.</summary>
    SchoolClass = 4,

    /// <summary>Fan.</summary>
    Subject = 5,

    /// <summary>Parallel (sinf darajasi).</summary>
    Grade = 6,

    /// <summary>Butun maktab.</summary>
    Global = 7
}

/// <summary>
/// aSc'dagi uch darajali time-off matritsasi: yashil / "?" / qizil.
/// <see cref="NotRecommended"/> = aSc'dagi "?" — qat'iy taqiq emas, lekin jarimali.
/// </summary>
public enum AvailabilityLevel
{
    /// <summary>Ruxsat etilgan (yashil).</summary>
    Allowed = 0,

    /// <summary>Tavsiya etilmaydi ("?") — dars qo'yish mumkin, lekin jarima beriladi.</summary>
    NotRecommended = 1,

    /// <summary>Taqiqlangan (qizil).</summary>
    Forbidden = 2
}

/// <summary>Fanning hafta bo'ylab taqsimlanish talabi (aSc "distribution").</summary>
public enum SubjectDistribution
{
    /// <summary>Talab yo'q.</summary>
    None = 0,

    /// <summary>Past.</summary>
    Low = 1,

    /// <summary>O'rtacha.</summary>
    Medium = 2,

    /// <summary>Ideal (kunlar bo'ylab tekis).</summary>
    Ideal = 3,

    /// <summary>Ideal va ketma-ket kunlarsiz.</summary>
    IdealNoConsecutive = 4
}

/// <summary>Xona turi.</summary>
public enum ClassroomKind
{
    /// <summary>Oddiy sinfxona.</summary>
    Regular = 0,

    /// <summary>Laboratoriya.</summary>
    Laboratory = 1,

    /// <summary>Sport zali.</summary>
    Gym = 2,

    /// <summary>Ustaxona (mehnat).</summary>
    Workshop = 3,

    /// <summary>Kompyuter xonasi.</summary>
    Computer = 4
}

/// <summary>Jins.</summary>
public enum Gender
{
    /// <summary>Erkak.</summary>
    Male = 1,

    /// <summary>Ayol.</summary>
    Female = 2
}

/// <summary>
/// <see cref="WeekDay"/> (1-based, UI uchun) va <c>DayNo</c> (0-based, DB uchun) o'rtasidagi
/// yagona konvertatsiya nuqtasi. Boshqa joyda <c>(int)day - 1</c> yozilmaydi.
/// </summary>
public static class DayNumbering
{
    /// <summary>Dushanba → 0, Seshanba → 1, ...</summary>
    public static int ToDayNo(WeekDay day) => (int)day - 1;

    /// <summary>0 → Dushanba, 1 → Seshanba, ...</summary>
    public static WeekDay ToWeekDay(int dayNo) => (WeekDay)(dayNo + 1);
}

/// <summary>
/// <c>int</c> bitmask ustunlari (<c>WeeksMask</c>, <c>AllowedDaysMask</c>, ...) bilan ishlash.
/// Bit 0 = birinchi kun/hafta. Mask <c>0</c> = "cheklov yo'q".
/// </summary>
public static class BitMask
{
    /// <summary>Maskdagi yoqilgan bit indekslarini qaytaradi (0-based, o'sish tartibida).</summary>
    public static IEnumerable<int> Bits(int mask)
    {
        for (var i = 0; i < 31; i++)
        {
            if ((mask & (1 << i)) != 0) yield return i;
        }
    }

    /// <summary>Bit indeksi maskda yoqilganmi.</summary>
    public static bool Has(int mask, int bitIndex) => (mask & (1 << bitIndex)) != 0;

    /// <summary>Birinchi <paramref name="count"/> ta bit yoqilgan mask: <c>(1 &lt;&lt; count) - 1</c>.</summary>
    public static int All(int count) => count <= 0 ? 0 : (1 << count) - 1;
}
