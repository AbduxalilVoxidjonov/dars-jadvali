namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// Vaqt panjarasi (kunlar x soatlar, ko'p haftalik siklni qo'llab-quvvatlaydi).
/// Global slot indeksi: <c>slot = dayIndex * Periods + period</c>, bunda
/// <c>dayIndex = week * DaysPerWeek + day</c> — butun jadval uchun bitta tekis indeks fazosi.
/// </summary>
public sealed class TimeGrid
{
    public TimeGrid(int daysPerWeek, int periods, int weeks = 1)
    {
        if (daysPerWeek <= 0) throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
        if (periods <= 0) throw new ArgumentOutOfRangeException(nameof(periods));
        if (weeks <= 0) throw new ArgumentOutOfRangeException(nameof(weeks));

        Weeks = weeks;
        DaysPerWeek = daysPerWeek;
        Periods = periods;
        TotalDays = weeks * daysPerWeek;
        SlotCount = TotalDays * periods;

        if (Periods > 64)
            throw new ArgumentOutOfRangeException(nameof(periods), "Bir kundagi darslar soni 64 dan oshmasligi kerak.");
        if (SlotCount > SlotMask.Capacity)
            throw new ArgumentOutOfRangeException(nameof(periods),
                $"Slotlar soni {SlotCount} > SlotMask.Capacity ({SlotMask.Capacity}).");

        FullMask = SlotMask.FullTo(SlotCount);
    }

    /// <summary>Sikldagi haftalar soni (C-CYC-01).</summary>
    public int Weeks { get; }

    /// <summary>Bir haftadagi o'quv kunlari (5..10).</summary>
    public int DaysPerWeek { get; }

    /// <summary>Bir kundagi darslar soni (0-dars ham hisobga olinsa, indeks 0 dan boshlanadi).</summary>
    public int Periods { get; }

    /// <summary>Weeks * DaysPerWeek.</summary>
    public int TotalDays { get; }

    /// <summary>Weeks * DaysPerWeek * Periods.</summary>
    public int SlotCount { get; }

    /// <summary>Barcha slotlar yoqilgan maska.</summary>
    public SlotMask FullMask { get; }

    public int SlotOf(int dayIndex, int period) => dayIndex * Periods + period;

    public int SlotOf(int week, int day, int period) => (week * DaysPerWeek + day) * Periods + period;

    public int DayOfSlot(int slot) => slot / Periods;

    public int PeriodOfSlot(int slot) => slot % Periods;

    public int WeekOfDay(int dayIndex) => dayIndex / DaysPerWeek;

    public int WeekOfSlot(int slot) => DayOfSlot(slot) / DaysPerWeek;

    /// <summary>Berilgan kun boshlanadigan slot indeksi.</summary>
    public int DayStart(int dayIndex) => dayIndex * Periods;

    /// <summary>
    /// <paramref name="length"/> uzunlikdagi karta boshlana oladigan slotlar maskasi:
    /// karta kun chegarasidan oshib ketmasligi kerak (C-DBL-01 — qo'sh dars uzluksizligi).
    /// </summary>
    public SlotMask StartMaskForLength(int length)
    {
        var m = SlotMask.Empty;
        for (int d = 0; d < TotalDays; d++)
            for (int p = 0; p + length <= Periods; p++)
                m = m.Set(SlotOf(d, p));
        return m;
    }

    /// <summary>Berilgan kunlar (dayIndex) to'plamiga mos slot maskasi.</summary>
    public SlotMask MaskForDays(IEnumerable<int> dayIndexes)
    {
        var m = SlotMask.Empty;
        foreach (var d in dayIndexes)
            for (int p = 0; p < Periods; p++)
                m = m.Set(SlotOf(d, p));
        return m;
    }
}
