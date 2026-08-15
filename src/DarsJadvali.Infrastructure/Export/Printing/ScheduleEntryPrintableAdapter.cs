using System.Globalization;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>Hujjat sarlavhasiga tushadigan umumiy ma'lumot.</summary>
/// <param name="SchoolName">Maktab nomi.</param>
/// <param name="AcademicYear">O'quv yili, masalan "2025/2026".</param>
/// <param name="Term">Chorak/semestr.</param>
/// <param name="FirstShiftPeriodCount">
/// 1-smenadagi darslar soni. 0 — bitta smena (smena polosasi chiqmaydi).
/// Masalan 6 bo'lsa: 1..6 — "1-smena", 7.. — "2-smena", soat raqamlari UZLUKSIZ qoladi.
/// </param>
/// <param name="GeneratedAt">Hujjat sanasi.</param>
public sealed record PrintableContext(
    string? SchoolName = null,
    string? AcademicYear = null,
    string? Term = null,
    int FirstShiftPeriodCount = 0,
    DateTime? GeneratedAt = null);

/// <summary>
/// <b>ESKIRGAN</b> — eski <c>ScheduleEntry</c> modelidan chop etish kartalari.
/// Yangi kod uchun <see cref="CardPrintableAdapter"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ko'chish holati.</b> Chop etish yo'li <c>Card</c>/<c>Lesson</c> modeliga o'tdi:
/// veb qobiq (<c>/api/board/print</c>) va <see cref="DesignBasedTimetablePdfExporter"/>
/// ning yangi konstruktori <see cref="CardPrintableAdapter"/> ni ishlatadi hamda juft
/// darsni, A/B haftani, guruh bo'linmasini va xonani HAQIQIY manbadan oladi. Bu fayl
/// faqat hali ko'chmagan chaqiruvchilar (Desktop va eski eksport yo'li) uchun qoldirildi
/// va ular ko'chgach o'chiriladi.
/// </para>
/// <para>
/// Renderer, joylashuv (layout), dizayn va HTML eksport entity'ni <b>umuman ko'rmaydi</b> —
/// faqat <see cref="PrintableCard"/> bilan ishlaydi. Shuning uchun modelni almashtirish
/// aynan adapter fayli bilan cheklandi.
/// (Desktop qatlamida xuddi shu naqsh: <c>Services/Timetable/ScheduleEntryCardAdapter.cs</c>.)
/// </para>
/// <para>
/// <b>Eski modelning cheklovlari</b> — quyidagi maydonlar <c>ScheduleEntry</c> da YO'Q,
/// shuning uchun standart qiymat beriladi va chop etishda ular ko'rinmaydi
/// (aynan shu sabab yangi adapter yozildi):
/// <list type="bullet">
///   <item><c>Length</c> → doim 1 (juft dars yo'q). Renderer <c>Length=2</c> ni
///     to'liq qo'llab-quvvatlaydi — <c>Card.Length</c> paydo bo'lishi bilan ishlaydi.</item>
///   <item><c>WeeksMask</c> → doim "har hafta" (A/B hafta yo'q).</item>
///   <item><c>GroupName</c> → doim bo'sh (bo'linma yo'q).</item>
/// </list>
/// </para>
/// </remarks>
public static class ScheduleEntryPrintableAdapter
{
    /// <summary>Ish kunlarini ustunlarga aylantiradi (indeks — ro'yxatdagi tartib).</summary>
    /// <param name="days">Faol kunlar, tartiblangan.</param>
    public static IReadOnlyList<PrintableDay> ToDays(IReadOnlyList<WeekDay> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        var result = new List<PrintableDay>(days.Count);
        for (var i = 0; i < days.Count; i++)
        {
            var name = days[i].ToUzbek();
            result.Add(new PrintableDay(i, name, name.Length >= 2 ? name[..2] : name));
        }

        return result;
    }

    /// <summary>
    /// Dars soatlarini qatorlarga aylantiradi. Raqamlar UZLUKSIZ (1..N),
    /// ikkinchi smena 1 dan qayta boshlanmaydi.
    /// </summary>
    /// <param name="slots">Vaqt oralig'i sozlamalari (bo'lmasa vaqt ko'rsatilmaydi).</param>
    /// <param name="maxLessonNumber">Eng katta soat raqami.</param>
    /// <param name="firstShiftPeriodCount">1-smenadagi darslar soni; 0 — bitta smena.</param>
    public static IReadOnlyList<PrintablePeriod> ToPeriods(
        IReadOnlyList<LessonSlot>? slots,
        int maxLessonNumber,
        int firstShiftPeriodCount = 0)
    {
        if (maxLessonNumber <= 0)
            return Array.Empty<PrintablePeriod>();

        var times = new Dictionary<int, string>();
        if (slots is not null)
        {
            foreach (var slot in slots)
                times[slot.LessonNumber] = $"{FormatTime(slot.StartTime)}-{FormatTime(slot.EndTime)}";
        }

        var twoShifts = firstShiftPeriodCount > 0 && maxLessonNumber > firstShiftPeriodCount;

        var result = new List<PrintablePeriod>(maxLessonNumber);
        for (var number = 1; number <= maxLessonNumber; number++)
        {
            times.TryGetValue(number, out var time);

            var shift = twoShifts
                ? (number <= firstShiftPeriodCount ? "1-smena" : "2-smena")
                : null;

            result.Add(new PrintablePeriod(
                number,
                number.ToString(CultureInfo.InvariantCulture),
                time,
                shift));
        }

        return result;
    }

    /// <summary>Yozuvlarni chop etish kartalariga aylantiradi.</summary>
    /// <param name="entries">Jadval yozuvlari.</param>
    /// <param name="dayOrder">Kunlar tartibi (indeks shu ro'yxatdan olinadi).</param>
    /// <param name="classNames">Sinf Id → nom (o'qituvchi jadvalida katakda kerak).</param>
    /// <param name="classRooms">Sinf Id → xona (yozuvda xona ko'rsatilmagan bo'lsa zaxira).</param>
    public static IReadOnlyList<PrintableCard> ToCards(
        IEnumerable<ScheduleEntry> entries,
        IReadOnlyList<WeekDay> dayOrder,
        IReadOnlyDictionary<int, string>? classNames = null,
        IReadOnlyDictionary<int, string?>? classRooms = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(dayOrder);

        var dayIndex = new Dictionary<WeekDay, int>(dayOrder.Count);
        for (var i = 0; i < dayOrder.Count; i++)
            dayIndex.TryAdd(dayOrder[i], i);

        var cards = new List<PrintableCard>();

        foreach (var entry in entries)
        {
            if (!dayIndex.TryGetValue(entry.DayOfWeek, out var index))
                continue;

            var subject = entry.Subject?.Name;
            if (string.IsNullOrWhiteSpace(subject))
                subject = entry.Subject?.Code;
            if (string.IsNullOrWhiteSpace(subject))
                subject = "(fan ko'rsatilmagan)";

            string? className = null;
            classNames?.TryGetValue(entry.ClassGroupId, out className);
            if (string.IsNullOrWhiteSpace(className))
                className = entry.ClassGroup?.Name;

            var room = entry.RoomNumber;
            if (string.IsNullOrWhiteSpace(room))
            {
                if (classRooms is not null && classRooms.TryGetValue(entry.ClassGroupId, out var fallback))
                    room = fallback;
                else
                    room = entry.ClassGroup?.RoomNumber;
            }

            var teacher = entry.Teacher?.FullName;

            cards.Add(new PrintableCard
            {
                SubjectName = subject!,
                SubjectShortName = string.IsNullOrWhiteSpace(entry.Subject?.Code) ? null : entry.Subject!.Code,
                TeacherNames = string.IsNullOrWhiteSpace(teacher) ? Array.Empty<string>() : new[] { teacher! },
                ClassName = string.IsNullOrWhiteSpace(className) ? null : className,
                RoomName = string.IsNullOrWhiteSpace(room) ? null : room!.Trim(),
                DayIndex = index,
                Period = entry.LessonNumber,

                // --- Eski modelda yo'q maydonlar (Card ga o'tishda shu yer o'zgaradi) ---
                GroupName = null,
                Length = 1,
                WeeksMask = PrintableCard.AllWeeks,
                // -----------------------------------------------------------------------

                ColorCode = FirstColor(entry.Teacher?.ColorCode, entry.Subject?.ColorCode),
            });
        }

        return cards;
    }

    /// <summary>Bitta sinf jadvali.</summary>
    /// <param name="classGroup">Sinf.</param>
    /// <param name="entries">Shu sinfning yozuvlari.</param>
    /// <param name="dayOrder">Kunlar tartibi.</param>
    /// <param name="periods">Soat qatorlari.</param>
    /// <param name="context">Sarlavha ma'lumoti.</param>
    public static PrintableTimetable BuildClass(
        ClassGroup classGroup,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<WeekDay> dayOrder,
        IReadOnlyList<PrintablePeriod> periods,
        PrintableContext context)
    {
        ArgumentNullException.ThrowIfNull(classGroup);
        ArgumentNullException.ThrowIfNull(context);

        var cards = ToCards(entries, dayOrder);
        var subCaption = string.IsNullOrWhiteSpace(classGroup.RoomNumber)
            ? null
            : $"Xona: {classGroup.RoomNumber}";

        return Compose(
            context,
            PrintScope.Class,
            classGroup.Name,
            dayOrder,
            periods,
            new[] { new PrintableSection(classGroup.Name, subCaption, cards) });
    }

    /// <summary>Bitta o'qituvchi jadvali — katakda sinf ko'rinadi.</summary>
    /// <param name="teacher">O'qituvchi.</param>
    /// <param name="entries">Shu o'qituvchining yozuvlari.</param>
    /// <param name="dayOrder">Kunlar tartibi.</param>
    /// <param name="periods">Soat qatorlari.</param>
    /// <param name="classGroups">Barcha sinflar (nom va xona uchun).</param>
    /// <param name="context">Sarlavha ma'lumoti.</param>
    public static PrintableTimetable BuildTeacher(
        Teacher teacher,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<WeekDay> dayOrder,
        IReadOnlyList<PrintablePeriod> periods,
        IReadOnlyList<ClassGroup> classGroups,
        PrintableContext context)
    {
        ArgumentNullException.ThrowIfNull(teacher);
        ArgumentNullException.ThrowIfNull(classGroups);
        ArgumentNullException.ThrowIfNull(context);

        var names = classGroups.ToDictionary(c => c.Id, c => c.Name);
        var rooms = classGroups.ToDictionary(c => c.Id, c => (string?)c.RoomNumber);

        var cards = ToCards(entries, dayOrder, names, rooms);
        var hours = cards.Sum(c => Math.Max(1, c.Length));

        return Compose(
            context,
            PrintScope.Teacher,
            teacher.FullName,
            dayOrder,
            periods,
            new[]
            {
                new PrintableSection(
                    teacher.FullName,
                    hours > 0 ? $"Haftalik yuklama: {hours.ToString(CultureInfo.InvariantCulture)} soat" : null,
                    cards),
            });
    }

    /// <summary>Butun maktab: har sinf uchun alohida to'r.</summary>
    /// <param name="classGroups">Sinflar (tartiblangan holda chiqadi).</param>
    /// <param name="entries">Barcha yozuvlar.</param>
    /// <param name="dayOrder">Kunlar tartibi.</param>
    /// <param name="periods">Soat qatorlari.</param>
    /// <param name="context">Sarlavha ma'lumoti.</param>
    public static PrintableTimetable BuildSchool(
        IReadOnlyList<ClassGroup> classGroups,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<WeekDay> dayOrder,
        IReadOnlyList<PrintablePeriod> periods,
        PrintableContext context)
    {
        ArgumentNullException.ThrowIfNull(classGroups);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(context);

        var byClass = entries.GroupBy(e => e.ClassGroupId).ToDictionary(g => g.Key, g => (IReadOnlyList<ScheduleEntry>)g.ToList());
        var sections = new List<PrintableSection>(classGroups.Count);

        foreach (var classGroup in classGroups)
        {
            var classEntries = byClass.TryGetValue(classGroup.Id, out var list) ? list : Array.Empty<ScheduleEntry>();
            var cards = ToCards(classEntries, dayOrder);

            sections.Add(new PrintableSection(
                classGroup.Name,
                string.IsNullOrWhiteSpace(classGroup.RoomNumber) ? null : $"Xona: {classGroup.RoomNumber}",
                cards));
        }

        return Compose(
            context,
            PrintScope.School,
            context.SchoolName,
            dayOrder,
            periods,
            sections);
    }

    // ------------------------------------------------------------------

    private static PrintableTimetable Compose(
        PrintableContext context,
        PrintScope scope,
        string? scopeName,
        IReadOnlyList<WeekDay> dayOrder,
        IReadOnlyList<PrintablePeriod> periods,
        IReadOnlyList<PrintableSection> sections) => new()
        {
            SchoolName = context.SchoolName,
            AcademicYear = context.AcademicYear,
            Term = context.Term,
            Scope = scope,
            ScopeName = scopeName,
            Days = ToDays(dayOrder),
            Periods = periods,
            Sections = sections,
            GeneratedAt = context.GeneratedAt ?? DateTime.Now,
        };

    private static string? FirstColor(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (PrintColor.IsValid(candidate))
                return candidate!.Trim().ToUpperInvariant();
        }

        return null;
    }

    private static string FormatTime(TimeSpan time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hours:00}:{time.Minutes:00}");
}
