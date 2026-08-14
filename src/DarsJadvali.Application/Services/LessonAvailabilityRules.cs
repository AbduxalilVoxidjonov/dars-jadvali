using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>
/// CONTRACT 2.2 §8 — o'qituvchi bandligi qoidasining YAGONA manbasi.
/// Ham <c>ScheduleSnapshot</c> (validatsiya), ham <c>AvailabilityService</c>
/// (dars soati bo'yicha o'girish) shu metodlarga tayanadi — qoida ikki joyda
/// ikki xil bo'lib qolmasligi uchun.
/// </summary>
internal static class LessonAvailabilityRules
{
    /// <summary>Ikki vaqt oralig'i kesishadimi (chegara tegishi kesishish emas).</summary>
    internal static bool Overlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd) =>
        aStart < bEnd && bStart < aEnd;

    /// <summary>Oraliq berilgan dars vaqtini to'liq qamrab oladimi.</summary>
    internal static bool Covers(TeacherAvailability item, TimeSpan start, TimeSpan end)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.StartTime <= start && end <= item.EndTime;
    }

    /// <summary>Qora ro'yxat: dars vaqti bilan kesishuvchi birinchi "band" oraliq (yo'q bo'lsa null).</summary>
    internal static TeacherAvailability? FindBlocking(
        IEnumerable<TeacherAvailability> dayItems, TimeSpan start, TimeSpan end)
    {
        ArgumentNullException.ThrowIfNull(dayItems);
        return dayItems.FirstOrDefault(a => !a.IsAvailable && Overlaps(start, end, a.StartTime, a.EndTime));
    }

    /// <summary>Oq ro'yxat: shu kun uchun "ishlayman" deb belgilangan oraliqlar.</summary>
    internal static List<TeacherAvailability> WhiteList(IEnumerable<TeacherAvailability> dayItems)
    {
        ArgumentNullException.ThrowIfNull(dayItems);
        return dayItems.Where(a => a.IsAvailable).ToList();
    }

    /// <summary>
    /// Berilgan dars vaqti shu kun uchun ruxsat etilganmi.
    /// Qora ro'yxat har doim ustun; oq ro'yxat faqat kamida bitta "ishlayman"
    /// oralig'i bo'lsa qo'llanadi (aks holda kun ochiq deb hisoblanadi).
    /// </summary>
    internal static bool IsAllowed(IEnumerable<TeacherAvailability> dayItems, TimeSpan start, TimeSpan end)
    {
        ArgumentNullException.ThrowIfNull(dayItems);

        var items = dayItems as IReadOnlyCollection<TeacherAvailability> ?? dayItems.ToList();
        if (items.Count == 0)
        {
            return true;
        }

        if (FindBlocking(items, start, end) is not null)
        {
            return false;
        }

        var free = WhiteList(items);
        if (free.Count == 0)
        {
            return true;
        }

        return free.Any(a => Covers(a, start, end));
    }
}
