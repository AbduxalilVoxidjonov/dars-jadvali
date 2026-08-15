namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>
/// To'rdagi bitta chiziladigan blok — bitta karta egallagan to'rtburchak (katak koordinatalarida).
/// </summary>
/// <param name="Card">Karta.</param>
/// <param name="DayIndex">Kun ustuni.</param>
/// <param name="RowIndex">Boshlanish qatori (soatlar ro'yxatidagi indeks, 0 dan).</param>
/// <param name="RowSpan">Nechta qatorni egallaydi. 2 — juft dars, YAXLIT blok.</param>
/// <param name="Lane">Katak ichidagi yo'lakcha (guruh) indeksi, 0 dan.</param>
/// <param name="LaneCount">Katak nechta yo'lakchaga bo'lingan (guruhlar soni).</param>
public sealed record TimetableBlock(
    PrintableCard Card,
    int DayIndex,
    int RowIndex,
    int RowSpan,
    int Lane,
    int LaneCount)
{
    /// <summary>Blok egallagan oxirgi qator indeksi (shu qator ham kiradi).</summary>
    public int LastRowIndex => RowIndex + RowSpan - 1;

    /// <summary>Juft (yoki undan uzun) darsmi.</summary>
    public bool IsDouble => RowSpan > 1;

    /// <summary>Katak guruhlarga bo'linganmi.</summary>
    public bool IsShared => LaneCount > 1;
}

/// <summary>
/// Bir to'rning tayyor joylashuvi.
/// </summary>
/// <param name="Section">Manba to'r.</param>
/// <param name="Blocks">Chiziladigan bloklar.</param>
/// <param name="Dropped">Joylashtirib bo'lmagan kartalar (soat jadvalda yo'q va h.k.).</param>
public sealed record TimetableLayout(
    PrintableSection Section,
    IReadOnlyList<TimetableBlock> Blocks,
    IReadOnlyList<PrintableCard> Dropped);

/// <summary>
/// Kartalarni to'r kataklariga joylashtiradi. Bu — chizishdan MUSTAQIL, sof hisob:
/// shuning uchun juft dars / guruh / A-B hafta mantig'ini PDF chizmasdan test qilish mumkin.
/// </summary>
/// <remarks>
/// <para><b>Juft dars</b> (<c>Length &gt;= 2</c>): karta bir nechta qatorni egallaydi va bitta
/// YAXLIT blok bo'lib chiziladi — orasidagi ajratuvchi chiziq chizilmaydi.</para>
/// <para><b>Guruh darslari</b>: bitta (kun, soat) da bir nechta karta bo'lsa, ular
/// yonma-yon YO'LAKCHALARGA (lane) bo'linadi. Yo'lakcha kartaning butun uzunligi bo'ylab
/// band bo'ladi, shuning uchun juft dars guruh darsi bilan kesishmaydi.</para>
/// <para><b>Kenglik</b>: blok kengligi o'zi egallagan qatorlardagi ENG KATTA yo'lakchalar
/// soniga qarab hisoblanadi — shu tufayli bloklar hech qachon bir-birining ustiga chiqmaydi.</para>
/// </remarks>
public static class TimetableGridLayout
{
    /// <summary>To'rni joylashtiradi.</summary>
    /// <param name="section">Kartalar to'plami.</param>
    /// <param name="days">Kun ustunlari.</param>
    /// <param name="periods">Soat qatorlari.</param>
    public static TimetableLayout Build(
        PrintableSection section,
        IReadOnlyList<PrintableDay> days,
        IReadOnlyList<PrintablePeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(periods);

        var dayCount = days.Count;
        var rowCount = periods.Count;

        if (dayCount == 0 || rowCount == 0)
            return new TimetableLayout(section, Array.Empty<TimetableBlock>(), section.Cards);

        var rowOfPeriod = new Dictionary<int, int>(rowCount);
        for (var i = 0; i < rowCount; i++)
            rowOfPeriod[periods[i].Number] = i;

        var dayIndexes = new HashSet<int>(days.Select(d => d.Index));

        // 1-bosqich: kartalarni barqaror tartibda saralaymiz — natija har doim bir xil.
        var ordered = section.Cards
            .OrderBy(c => c.DayIndex)
            .ThenBy(c => c.Period)
            .ThenBy(c => c.GroupName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.SubjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dropped = new List<PrintableCard>();

        // 2-bosqich: yo'lakcha (lane) tayinlash. lanes[kun][qator] -> band yo'lakchalar.
        var occupied = new List<bool>[dayCount, rowCount];
        var placed = new List<(PrintableCard Card, int Day, int Row, int Span, int Lane)>();

        foreach (var card in ordered)
        {
            if (!dayIndexes.Contains(card.DayIndex) || !rowOfPeriod.TryGetValue(card.Period, out var startRow))
            {
                dropped.Add(card);
                continue;
            }

            var dayColumn = IndexOfDay(days, card.DayIndex);
            var span = Math.Max(1, card.Length);

            // Jadval oxiridan oshib ketgan juft dars qisqartiriladi (ma'lumot yo'qolmaydi).
            if (startRow + span > rowCount)
                span = rowCount - startRow;

            var lane = FirstFreeLane(occupied, dayColumn, startRow, span);

            for (var r = startRow; r < startRow + span; r++)
            {
                var list = occupied[dayColumn, r] ??= new List<bool>();
                while (list.Count <= lane)
                    list.Add(false);
                list[lane] = true;
            }

            placed.Add((card, dayColumn, startRow, span, lane));
        }

        // 3-bosqich: har bir blok uchun yo'lakchalar sonini aniqlaymiz
        // (o'zi egallagan qatorlardagi eng katta qiymat — kesishishning oldi olinadi).
        var blocks = new List<TimetableBlock>(placed.Count);
        foreach (var (card, day, row, span, lane) in placed)
        {
            var laneCount = 1;
            for (var r = row; r < row + span; r++)
            {
                var list = occupied[day, r];
                if (list is not null)
                    laneCount = Math.Max(laneCount, list.Count);
            }

            blocks.Add(new TimetableBlock(card, day, row, span, lane, laneCount));
        }

        return new TimetableLayout(section, blocks, dropped);
    }

    /// <summary>Bir necha to'rni birdaniga joylashtiradi (maktab jadvali).</summary>
    /// <param name="timetable">Jadval.</param>
    public static IReadOnlyList<TimetableLayout> BuildAll(PrintableTimetable timetable)
    {
        ArgumentNullException.ThrowIfNull(timetable);
        return timetable.Sections
            .Select(s => Build(s, timetable.Days, timetable.Periods))
            .ToList();
    }

    /// <summary>
    /// To'rlarni sahifalarga bo'ladi: har sahifada <paramref name="sectionsPerPage"/> tadan.
    /// </summary>
    /// <param name="layouts">Joylashuvlar.</param>
    /// <param name="sectionsPerPage">Bitta sahifadagi to'rlar soni (kamida 1).</param>
    /// <returns>Sahifalar — har biri to'rlar ro'yxati. Hech bo'lmasa bitta (bo'sh) sahifa qaytadi.</returns>
    public static IReadOnlyList<IReadOnlyList<TimetableLayout>> Paginate(
        IReadOnlyList<TimetableLayout> layouts,
        int sectionsPerPage)
    {
        ArgumentNullException.ThrowIfNull(layouts);

        var perPage = Math.Max(1, sectionsPerPage);
        var pages = new List<IReadOnlyList<TimetableLayout>>();

        for (var i = 0; i < layouts.Count; i += perPage)
            pages.Add(layouts.Skip(i).Take(perPage).ToList());

        if (pages.Count == 0)
            pages.Add(Array.Empty<TimetableLayout>());

        return pages;
    }

    private static int IndexOfDay(IReadOnlyList<PrintableDay> days, int dayIndex)
    {
        for (var i = 0; i < days.Count; i++)
        {
            if (days[i].Index == dayIndex)
                return i;
        }

        return 0;
    }

    /// <summary>Berilgan qatorlar oralig'ida bo'sh bo'lgan eng chapdagi yo'lakcha.</summary>
    private static int FirstFreeLane(List<bool>[,] occupied, int day, int startRow, int span)
    {
        for (var lane = 0; ; lane++)
        {
            var free = true;
            for (var r = startRow; r < startRow + span; r++)
            {
                var list = occupied[day, r];
                if (list is not null && lane < list.Count && list[lane])
                {
                    free = false;
                    break;
                }
            }

            if (free)
                return lane;
        }
    }
}
