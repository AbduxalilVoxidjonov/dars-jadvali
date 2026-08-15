using System.Globalization;
using DarsJadvali.Application.Board;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>
/// To'rning o'qlari (ustunlar — kunlar, qatorlar — dars soatlari) va sikldagi haftalar soni.
/// </summary>
/// <param name="Days">Kun ustunlari; <see cref="PrintableDay.Index"/> — <c>CardView.DayNo</c>.</param>
/// <param name="Periods">Soat qatorlari — UZLUKSIZ raqamlangan (1-smena 1..6, 2-smena 7..12).</param>
/// <param name="WeeksInCycle">
/// Sikldagi haftalar soni. 1 bo'lsa A/B hafta yo'q — hamma kartochka "har hafta" deb ko'rsatiladi.
/// </param>
public sealed record CardPrintAxes(
    IReadOnlyList<PrintableDay> Days,
    IReadOnlyList<PrintablePeriod> Periods,
    int WeeksInCycle = 1);

/// <summary>
/// <b>YANGI</b> (va endi yagona ishlatiladigan) joy, qayerda chop etish modeli
/// <c>Card</c>/<c>Lesson</c> ma'lumoti bilan uchrashadi.
/// </summary>
/// <remarks>
/// <para>
/// Eski <see cref="ScheduleEntryPrintableAdapter"/> <c>ScheduleEntry</c> dan karta yasardi
/// va uchta maydonni STANDART qiymat bilan to'ldirishga majbur edi: <c>Length=1</c>
/// (juft dars yo'q), <c>WeeksMask="har hafta"</c> (A/B yo'q), <c>GroupName=null</c>
/// (bo'linma yo'q). Bu adapter uchalasini ham <see cref="CardView"/> dan —
/// HAQIQIY manbadan — oladi.
/// </para>
/// <para>
/// Renderer, joylashuv (<see cref="TimetableGridLayout"/>), dizayn va HTML eksport
/// bu o'tishni umuman sezmaydi: ular faqat <see cref="PrintableCard"/> bilan ishlaydi.
/// </para>
/// <para>
/// Eski adapter hozircha o'chirilmadi — Desktop va eski <c>ScheduleEntry</c> asosidagi
/// eksport yo'li hali ko'chirilmoqda. Ko'chish tugagach o'sha fayl o'chiriladi.
/// </para>
/// </remarks>
public static class CardPrintableAdapter
{
    // ==================================================================
    // O'qlar (axes)
    // ==================================================================

    /// <summary>
    /// Faol ish kunlarini ustunlarga aylantiradi.
    /// <see cref="PrintableDay.Index"/> — <c>CardView.DayNo</c> (0 = dushanba),
    /// ustun tartibi esa ro'yxatdagi joylashuv.
    /// </summary>
    /// <param name="workDays">Ish kunlari (faol bo'lmaganlari tashlanadi).</param>
    public static IReadOnlyList<PrintableDay> ToDays(IReadOnlyList<WorkDay> workDays)
    {
        ArgumentNullException.ThrowIfNull(workDays);

        // DayNo v2 maydoni; eski qatorlarda hammasi 0 bo'lishi mumkin — bunda
        // kun raqami WeekDay dan tiklanadi.
        var hasDayNo = workDays.Any(w => w.DayNo > 0);

        var result = new List<PrintableDay>();
        var seen = new HashSet<int>();

        foreach (var day in workDays.Where(w => w.IsActive))
        {
            var dayNo = hasDayNo ? day.DayNo : DayNumbering.ToDayNo(day.DayOfWeek);
            if (!seen.Add(dayNo))
                continue;

            var name = string.IsNullOrWhiteSpace(day.Name) ? day.DayOfWeek.ToUzbek() : day.Name!.Trim();
            var shortName = string.IsNullOrWhiteSpace(day.ShortName)
                ? (name.Length >= 2 ? name[..2] : name)
                : day.ShortName!.Trim();

            result.Add(new PrintableDay(dayNo, name, shortName));
        }

        return result.OrderBy(d => d.Index).ToList();
    }

    /// <summary>
    /// Dars soatlarini qatorlarga aylantiradi. Raqamlar UZLUKSIZ: 2-smena 1 dan
    /// qayta boshlanmaydi (<c>Period.PeriodNo</c> o'quv yili ichida global).
    /// </summary>
    /// <param name="periods">Qo'ng'iroq jadvali qatorlari (tanaffuslar tashlanadi).</param>
    /// <param name="shifts">Smenalar — nomi polosaga chiqadi. Bitta smena bo'lsa polosa yo'q.</param>
    public static IReadOnlyList<PrintablePeriod> ToPeriods(
        IReadOnlyList<Period> periods,
        IReadOnlyList<Shift>? shifts = null)
    {
        ArgumentNullException.ThrowIfNull(periods);

        var shiftNames = new Dictionary<int, string>();
        if (shifts is not null)
        {
            foreach (var shift in shifts.OrderBy(s => s.ShiftNo))
            {
                shiftNames[shift.Id] = string.IsNullOrWhiteSpace(shift.Name)
                    ? $"{shift.ShiftNo}-smena"
                    : shift.Name.Trim();
            }
        }

        // Smena polosasi faqat HAQIQATAN ikki (yoki undan ko'p) smena bo'lganda chiqadi.
        var usedShifts = periods
            .Where(p => !p.IsBreak && p.ShiftId is not null)
            .Select(p => p.ShiftId!.Value)
            .Distinct()
            .Count();

        var result = new List<PrintablePeriod>();

        foreach (var period in periods.Where(p => !p.IsBreak).OrderBy(p => p.PeriodNo))
        {
            var label = string.IsNullOrWhiteSpace(period.ShortName)
                ? period.PeriodNo.ToString(CultureInfo.InvariantCulture)
                : period.ShortName!.Trim();

            string? shiftName = null;
            if (usedShifts > 1 && period.ShiftId is int id && shiftNames.TryGetValue(id, out var name))
                shiftName = name;

            result.Add(new PrintablePeriod(
                period.PeriodNo,
                label,
                $"{FormatTime(period.StartTime)}-{FormatTime(period.EndTime)}",
                shiftName));
        }

        return result;
    }

    /// <summary>O'qlarni bir chaqiruvda quradi.</summary>
    /// <param name="workDays">Ish kunlari.</param>
    /// <param name="periods">Dars soatlari.</param>
    /// <param name="shifts">Smenalar.</param>
    /// <param name="weeksInCycle">Sikldagi haftalar soni (A/B uchun).</param>
    public static CardPrintAxes ToAxes(
        IReadOnlyList<WorkDay> workDays,
        IReadOnlyList<Period> periods,
        IReadOnlyList<Shift>? shifts = null,
        int weeksInCycle = 1) =>
        new(ToDays(workDays), ToPeriods(periods, shifts), Math.Max(1, weeksInCycle));

    // ==================================================================
    // Kartochkalar
    // ==================================================================

    /// <summary>
    /// <see cref="CardView"/> larni chop etish kartalariga aylantiradi.
    /// </summary>
    /// <param name="cards">Kartochkalar.</param>
    /// <param name="axes">O'qlar (kun ustunlari va hafta sikli).</param>
    /// <param name="subjectShortNames">Fan Id → qisqartma (tor katak uchun).</param>
    /// <param name="subjectColors">Fan Id → rang.</param>
    /// <param name="teacherColors">O'qituvchi Id → rang.</param>
    /// <param name="showClassName">
    /// Katakda sinf nomi ko'rinsinmi (o'qituvchi jadvalida — ha, sinf jadvalida — yo'q).
    /// </param>
    public static IReadOnlyList<PrintableCard> ToCards(
        IEnumerable<CardView> cards,
        CardPrintAxes axes,
        IReadOnlyDictionary<int, string?>? subjectShortNames = null,
        IReadOnlyDictionary<int, string?>? subjectColors = null,
        IReadOnlyDictionary<int, string?>? teacherColors = null,
        bool showClassName = false)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(axes);

        var dayIndexes = axes.Days.Select(d => d.Index).ToHashSet();
        var result = new List<PrintableCard>();

        foreach (var card in cards)
        {
            // Ish kuni bo'lmagan kunga tushib qolgan kartochka chop etishga chiqmaydi.
            if (dayIndexes.Count > 0 && !dayIndexes.Contains(card.DayNo))
                continue;

            var subject = string.IsNullOrWhiteSpace(card.SubjectName)
                ? "(fan ko'rsatilmagan)"
                : card.SubjectName.Trim();

            string? shortName = null;
            subjectShortNames?.TryGetValue(card.SubjectId, out shortName);

            string? subjectColor = null;
            subjectColors?.TryGetValue(card.SubjectId, out subjectColor);

            string? teacherColor = null;
            if (card.TeacherIds.Count > 0)
                teacherColors?.TryGetValue(card.TeacherIds[0], out teacherColor);

            result.Add(new PrintableCard
            {
                SubjectName = subject,
                SubjectShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName!.Trim(),
                TeacherNames = card.TeacherNames
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .ToArray(),
                ClassName = showClassName && !string.IsNullOrWhiteSpace(card.ClassName)
                    ? card.ClassName.Trim()
                    : null,

                // --- Eski modelda YO'Q bo'lgan uchta maydon endi haqiqiy manbadan ---
                GroupName = string.IsNullOrWhiteSpace(card.GroupName) ? null : card.GroupName.Trim(),
                Length = Math.Max(1, card.Length),
                WeeksMask = ToPrintWeeksMask(card.WeeksMask, axes.WeeksInCycle),
                // -------------------------------------------------------------------

                RoomName = string.IsNullOrWhiteSpace(card.RoomNumber) ? null : card.RoomNumber!.Trim(),
                DayIndex = card.DayNo,
                Period = card.PeriodNo,
                ColorCode = FirstColor(teacherColor, subjectColor),
            });
        }

        return result;
    }

    /// <summary>
    /// <c>Card.WeeksMask</c> ni chop etish maskasiga aylantiradi.
    /// </summary>
    /// <remarks>
    /// Bir haftalik siklda (<paramref name="weeksInCycle"/> = 1) A/B tushunchasi yo'q —
    /// mask qiymati qanday bo'lishidan qat'i nazar "har hafta" qaytadi, aks holda
    /// har bir kartada keraksiz "A" nishoni chiqib qolardi.
    /// </remarks>
    /// <param name="weeksMask">Kartochkadagi mask (0 — chegaralanmagan).</param>
    /// <param name="weeksInCycle">Sikldagi haftalar soni.</param>
    public static int ToPrintWeeksMask(int weeksMask, int weeksInCycle)
    {
        if (weeksInCycle <= 1 || weeksMask <= 0)
            return PrintableCard.AllWeeks;

        var masked = weeksMask & PrintableCard.AllWeeks;
        return masked == 0 ? PrintableCard.AllWeeks : masked;
    }

    // ==================================================================
    // Tayyor jadvallar
    // ==================================================================

    /// <summary>Bitta sinf jadvali.</summary>
    /// <param name="className">Sinf nomi.</param>
    /// <param name="subCaption">Qo'shimcha qator (xona, sinf rahbari, smena).</param>
    /// <param name="cards">Shu sinfning kartochkalari.</param>
    /// <param name="axes">O'qlar.</param>
    /// <param name="context">Sarlavha ma'lumoti.</param>
    /// <param name="names">Nom/rang lug'atlari.</param>
    public static PrintableTimetable BuildClass(
        string className,
        string? subCaption,
        IEnumerable<CardView> cards,
        CardPrintAxes axes,
        PrintableContext context,
        CardPrintNames? names = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var printable = ToCards(
            cards, axes,
            names?.SubjectShortNames, names?.SubjectColors, names?.TeacherColors,
            showClassName: false);

        return Compose(
            context, PrintScope.Class, className, axes,
            new[] { new PrintableSection(className, subCaption, printable) });
    }

    /// <summary>Bitta o'qituvchi jadvali — katakda sinf ko'rinadi.</summary>
    /// <param name="teacherName">O'qituvchi FIO.</param>
    /// <param name="cards">Shu o'qituvchining kartochkalari.</param>
    /// <param name="axes">O'qlar.</param>
    /// <param name="context">Sarlavha ma'lumoti.</param>
    /// <param name="names">Nom/rang lug'atlari.</param>
    public static PrintableTimetable BuildTeacher(
        string teacherName,
        IEnumerable<CardView> cards,
        CardPrintAxes axes,
        PrintableContext context,
        CardPrintNames? names = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var printable = ToCards(
            cards, axes,
            names?.SubjectShortNames, names?.SubjectColors, names?.TeacherColors,
            showClassName: true);

        // Haftalik yuklama JUFT darsni ham to'g'ri sanaydi (Length yig'indisi).
        var hours = printable.Sum(c => Math.Max(1, c.Length));

        return Compose(
            context, PrintScope.Teacher, teacherName, axes,
            new[]
            {
                new PrintableSection(
                    teacherName,
                    hours > 0
                        ? $"Haftalik yuklama: {hours.ToString(CultureInfo.InvariantCulture)} soat"
                        : null,
                    printable),
            });
    }

    /// <summary>Butun maktab: har sinf uchun alohida to'r.</summary>
    /// <param name="classes">Sinflar (ko'rsatiladigan tartibda).</param>
    /// <param name="cards">Barcha kartochkalar.</param>
    /// <param name="axes">O'qlar.</param>
    /// <param name="context">Sarlavha ma'lumoti.</param>
    /// <param name="names">Nom/rang lug'atlari.</param>
    public static PrintableTimetable BuildSchool(
        IReadOnlyList<CardPrintClass> classes,
        IEnumerable<CardView> cards,
        CardPrintAxes axes,
        PrintableContext context,
        CardPrintNames? names = null)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(context);

        var all = cards.ToList();
        var sections = new List<PrintableSection>(classes.Count);

        foreach (var item in classes)
        {
            var own = all.Where(c => c.SchoolClassIds.Contains(item.Id));

            sections.Add(new PrintableSection(
                item.Name,
                item.SubCaption,
                ToCards(
                    own, axes,
                    names?.SubjectShortNames, names?.SubjectColors, names?.TeacherColors,
                    showClassName: false)));
        }

        return Compose(context, PrintScope.School, context.SchoolName, axes, sections);
    }

    // ------------------------------------------------------------------

    private static PrintableTimetable Compose(
        PrintableContext context,
        PrintScope scope,
        string? scopeName,
        CardPrintAxes axes,
        IReadOnlyList<PrintableSection> sections) => new()
        {
            SchoolName = context.SchoolName,
            AcademicYear = context.AcademicYear,
            Term = context.Term,
            Scope = scope,
            ScopeName = scopeName,
            Days = axes.Days,
            Periods = axes.Periods,
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

    private static string FormatTime(TimeOnly time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hour:00}:{time.Minute:00}");
}

/// <summary>Maktab jadvalidagi bitta sinf to'ri.</summary>
/// <param name="Id"><c>SchoolClass.Id</c>.</param>
/// <param name="Name">Sinf nomi.</param>
/// <param name="SubCaption">Qo'shimcha qator (xona, smena).</param>
public sealed record CardPrintClass(int Id, string Name, string? SubCaption = null);

/// <summary>
/// Kartochkada ko'rinadigan qo'shimcha nom va ranglar — <see cref="CardView"/> da yo'q,
/// chunki u faqat to'r uchun kerak bo'lgan minimal ma'lumotni tashiydi.
/// </summary>
/// <param name="SubjectShortNames">Fan Id → qisqartma.</param>
/// <param name="SubjectColors">Fan Id → rang "#RRGGBB".</param>
/// <param name="TeacherColors">O'qituvchi Id → rang "#RRGGBB".</param>
public sealed record CardPrintNames(
    IReadOnlyDictionary<int, string?>? SubjectShortNames = null,
    IReadOnlyDictionary<int, string?>? SubjectColors = null,
    IReadOnlyDictionary<int, string?>? TeacherColors = null);
