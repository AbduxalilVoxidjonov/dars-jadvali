using System.Globalization;
using System.Reflection;
using System.Text;
using DarsJadvali.Domain.Common;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>HTML eksport sozlamalari.</summary>
public sealed record HtmlExportOptions
{
    /// <summary>Katakda o'qituvchi ismi.</summary>
    public bool ShowTeacher { get; init; } = true;

    /// <summary>Katakda sinf nomi (o'qituvchi jadvali uchun).</summary>
    public bool ShowClass { get; init; }

    /// <summary>Katakda xona.</summary>
    public bool ShowRoom { get; init; } = true;

    /// <summary>Katakda guruh nomi.</summary>
    public bool ShowGroup { get; init; } = true;

    /// <summary>A/B hafta belgisi.</summary>
    public bool ShowWeeks { get; init; } = true;

    /// <summary>Vaqt oralig'i (soat ustunida).</summary>
    public bool ShowTime { get; init; } = true;

    /// <summary>Fanlar legendasi qo'shilsinmi.</summary>
    public bool IncludeSubjectLegend { get; init; } = true;

    /// <summary>Rang sxemasi.</summary>
    public PrintTheme Theme { get; init; } = new();

    /// <summary>Qamrovga mos standart sozlama.</summary>
    /// <param name="scope">Qamrov.</param>
    public static HtmlExportOptions ForScope(PrintScope scope) => scope switch
    {
        PrintScope.Teacher => new HtmlExportOptions { ShowTeacher = false, ShowClass = true },
        PrintScope.School => new HtmlExportOptions { ShowTeacher = false, IncludeSubjectLegend = false },
        _ => new HtmlExportOptions(),
    };
}

/// <summary>
/// Jadvalni BITTA mustaqil (offline) HTML faylga chiqaradi.
/// </summary>
/// <remarks>
/// <para>
/// aSc <c>template/Web/</c> dagi yondashuv takrorlangan: shablon fayli +
/// oddiy <c>{TOKEN}</c> almashtirish, hech qanday shablon dvigateli yo'q.
/// </para>
/// <para>
/// Farqi: aSc'da butun to'r <c>{INSERTTABLE}</c> ga bir bo'lak bo'lib quyiladi
/// va shablondan boshqarib bo'lmaydi; bu yerda CSS shablonda va uni tahrirlash
/// natijaga darhol ta'sir qiladi. Uslub <c>&lt;style&gt;</c> ichida —
/// TASHQI RESURS YO'Q (CSS, JS, rasm, shrift), fayl internetsiz ochiladi.
/// </para>
/// </remarks>
public sealed class TimetableHtmlExporter
{
    private const string TemplateResource =
        "DarsJadvali.Infrastructure.Export.Printing.Templates.timetable.html";

    private static string? _template;

    /// <summary>Jadvalni HTML matniga aylantiradi.</summary>
    /// <param name="timetable">Jadval.</param>
    /// <param name="options">Sozlamalar (<c>null</c> — qamrovga mos standart).</param>
    public string Export(PrintableTimetable timetable, HtmlExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(timetable);

        var opts = options ?? HtmlExportOptions.ForScope(timetable.Scope);
        var theme = opts.Theme;

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{TITLE}"] = Escape(timetable.ScopeTitle),
            ["{SCHOOL}"] = Escape(timetable.SchoolName ?? string.Empty),
            ["{SUBTITLE}"] = Escape(timetable.ScopeTitle),
            ["{META}"] = Escape(BuildMeta(timetable)),
            ["{NAV}"] = BuildNav(timetable),
            ["{CONTENT}"] = BuildContent(timetable, opts),
            ["{FOOTER}"] = Escape($"{AppInfo.AppName} · {timetable.FormattedDate}"),
            ["{ACCENT}"] = theme.Accent,
            ["{HEADER_BG}"] = theme.HeaderBackground,
            ["{HEADER_FG}"] = theme.HeaderForeground,
            ["{CARD_BG}"] = theme.CardBackground,
            ["{GRID_LINE}"] = theme.GridLine,
            ["{MUTED}"] = theme.Muted,
        };

        var html = LoadTemplate();
        foreach (var (token, value) in tokens)
            html = html.Replace(token, value, StringComparison.Ordinal);

        return html;
    }

    /// <summary>HTML ni UTF-8 baytlarga aylantiradi (BOM'siz).</summary>
    /// <param name="timetable">Jadval.</param>
    /// <param name="options">Sozlamalar.</param>
    public byte[] ExportBytes(PrintableTimetable timetable, HtmlExportOptions? options = null) =>
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(Export(timetable, options));

    // ------------------------------------------------------------------
    // Bo'limlar
    // ------------------------------------------------------------------

    private static string BuildMeta(PrintableTimetable timetable)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(timetable.AcademicYear))
            parts.Add(timetable.AcademicYear!);
        if (!string.IsNullOrWhiteSpace(timetable.Term))
            parts.Add(timetable.Term!);
        parts.Add(timetable.FormattedDate);
        return string.Join(" · ", parts);
    }

    private static string BuildNav(PrintableTimetable timetable)
    {
        if (timetable.Sections.Count < 2)
            return string.Empty;

        var nav = new StringBuilder("<nav class=\"sections\">");
        for (var i = 0; i < timetable.Sections.Count; i++)
            nav.Append($"<a href=\"#s{i.ToString(CultureInfo.InvariantCulture)}\">{Escape(timetable.Sections[i].Caption)}</a>");

        nav.Append("</nav>");
        return nav.ToString();
    }

    private static string BuildContent(PrintableTimetable timetable, HtmlExportOptions options)
    {
        if (timetable.Days.Count == 0 || timetable.Periods.Count == 0 || timetable.Sections.Count == 0)
            return $"<p class=\"empty\">{Escape(PrintableTimetable.EmptyMessage)}</p>";

        var html = new StringBuilder();
        var index = 0;

        foreach (var section in timetable.Sections)
        {
            var layout = TimetableGridLayout.Build(section, timetable.Days, timetable.Periods);

            html.Append($"<section class=\"grid\" id=\"s{index.ToString(CultureInfo.InvariantCulture)}\">");
            index++;

            html.Append("<h2>").Append(Escape(section.Caption));
            if (!string.IsNullOrWhiteSpace(section.SubCaption))
                html.Append(" <small>").Append(Escape(section.SubCaption!)).Append("</small>");
            html.Append("</h2>");

            if (section.Cards.Count == 0)
            {
                html.Append($"<p class=\"empty\">{Escape(PrintableTimetable.EmptyMessage)}</p>");
            }
            else
            {
                html.Append("<div class=\"scroll\">");
                AppendTable(html, timetable, layout, options);
                html.Append("</div>");

                if (options.IncludeSubjectLegend)
                    AppendLegend(html, section.Cards);
            }

            html.Append("</section>");
        }

        return html.ToString();
    }

    private static void AppendTable(
        StringBuilder html,
        PrintableTimetable timetable,
        TimetableLayout layout,
        HtmlExportOptions options)
    {
        var days = timetable.Days;
        var periods = timetable.Periods;
        var hasShifts = timetable.ShiftNames.Count > 1;

        html.Append("<table class=\"tt\"><thead><tr>");
        if (hasShifts)
            html.Append("<th class=\"shift\"></th>");
        html.Append("<th class=\"hour\">Soat</th>");
        foreach (var day in days)
            html.Append("<th>").Append(Escape(day.Name)).Append("</th>");
        html.Append("</tr></thead><tbody>");

        // Bloklarni (kun, qator) bo'yicha guruhlaymiz.
        var starts = new Dictionary<(int Day, int Row), List<TimetableBlock>>();
        var covered = new HashSet<(int Day, int Row)>();

        foreach (var block in layout.Blocks)
        {
            var key = (block.DayIndex, block.RowIndex);
            if (!starts.TryGetValue(key, out var list))
                starts[key] = list = new List<TimetableBlock>();
            list.Add(block);

            for (var r = block.RowIndex + 1; r <= block.LastRowIndex; r++)
                covered.Add((block.DayIndex, r));
        }

        // Har bir katak uchun rowspan: faqat konflikt bo'lmasa (o'rtada yangi
        // blok boshlanmasa) ishlatiladi — aks holda jadval tuzilishi buziladi.
        var rowSpan = new Dictionary<(int Day, int Row), int>();
        var skip = new HashSet<(int Day, int Row)>();

        foreach (var ((day, row), blocks) in starts)
        {
            var span = blocks.Max(b => b.RowSpan);
            if (span <= 1)
                continue;

            var conflict = false;
            for (var r = row + 1; r < row + span; r++)
            {
                if (starts.ContainsKey((day, r)))
                {
                    conflict = true;
                    break;
                }
            }

            if (conflict)
                continue;

            rowSpan[(day, row)] = span;
            for (var r = row + 1; r < row + span; r++)
                skip.Add((day, r));
        }

        for (var r = 0; r < periods.Count; r++)
        {
            html.Append("<tr>");

            if (hasShifts)
                AppendShiftCell(html, timetable, r);

            var period = periods[r];
            html.Append("<th class=\"hour\">").Append(Escape(period.Label));
            if (options.ShowTime && !string.IsNullOrWhiteSpace(period.TimeLabel))
                html.Append("<span class=\"time\">").Append(Escape(period.TimeLabel!)).Append("</span>");
            html.Append("</th>");

            for (var d = 0; d < days.Count; d++)
            {
                if (skip.Contains((d, r)))
                    continue;

                var span = rowSpan.TryGetValue((d, r), out var value) ? value : 1;
                html.Append(span > 1
                    ? $"<td class=\"slot\" rowspan=\"{span.ToString(CultureInfo.InvariantCulture)}\">"
                    : "<td class=\"slot\">");

                if (starts.TryGetValue((d, r), out var blocks))
                {
                    html.Append("<div class=\"lanes\">");
                    foreach (var block in blocks.OrderBy(b => b.Lane))
                        AppendCard(html, block, options);
                    html.Append("</div>");
                }
                else if (covered.Contains((d, r)))
                {
                    // rowspan ishlatilmagan holat — juft dars davomi ekani yozib qo'yiladi.
                    html.Append("<div class=\"cont\">↑ davomi</div>");
                }

                html.Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
    }

    private static void AppendShiftCell(StringBuilder html, PrintableTimetable timetable, int rowIndex)
    {
        var periods = timetable.Periods;
        var name = periods[rowIndex].ShiftName;

        // Faqat smenaning birinchi qatorida yozuv chiqadi (rowspan bilan birlashtiriladi).
        if (rowIndex > 0 && string.Equals(periods[rowIndex - 1].ShiftName, name, StringComparison.Ordinal))
            return;

        var span = 1;
        while (rowIndex + span < periods.Count &&
               string.Equals(periods[rowIndex + span].ShiftName, name, StringComparison.Ordinal))
        {
            span++;
        }

        html.Append($"<th class=\"shift\" rowspan=\"{span.ToString(CultureInfo.InvariantCulture)}\">");
        if (!string.IsNullOrWhiteSpace(name))
            html.Append("<span>").Append(Escape(name!)).Append("</span>");
        html.Append("</th>");
    }

    private static void AppendCard(StringBuilder html, TimetableBlock block, HtmlExportOptions options)
    {
        var card = block.Card;
        var background = string.IsNullOrWhiteSpace(card.ColorCode)
            ? PrintColor.FromName(card.SubjectName)
            : card.ColorCode!;

        html.Append("<div class=\"lane\">");
        html.Append(block.IsDouble
            ? $"<div class=\"card double\" style=\"background:{Escape(background)}\">"
            : $"<div class=\"card\" style=\"background:{Escape(background)}\">");

        if (options.ShowWeeks && card.WeekLabel is not null)
            html.Append("<span class=\"week\">").Append(Escape(card.WeekLabel!)).Append("</span>");

        html.Append("<div class=\"subject\">").Append(Escape(card.SubjectName));
        if (block.IsDouble)
            html.Append(" <span class=\"group\">(juft)</span>");
        html.Append("</div>");

        if (options.ShowGroup && !string.IsNullOrWhiteSpace(card.GroupName))
            html.Append("<div class=\"group\">").Append(Escape(card.GroupName!)).Append("</div>");

        if (options.ShowClass && !string.IsNullOrWhiteSpace(card.ClassName))
            html.Append("<div class=\"line\">").Append(Escape(card.ClassName!)).Append("</div>");

        if (options.ShowTeacher && card.TeacherNames.Count > 0)
        {
            var teachers = card.TeacherLine;
            if (teachers.Length > 0)
                html.Append("<div class=\"line\">").Append(Escape(teachers)).Append("</div>");
        }

        if (options.ShowRoom && !string.IsNullOrWhiteSpace(card.RoomName))
            html.Append("<div class=\"line\">").Append(Escape(card.RoomName!)).Append("</div>");

        html.Append("</div></div>");
    }

    private static void AppendLegend(StringBuilder html, IReadOnlyList<PrintableCard> cards)
    {
        var items = PrintLegendBuilder.Build(cards, PrintLegendKind.Subjects);
        if (items.Count == 0)
            return;

        html.Append("<div class=\"legend\"><h3>Fanlar</h3><ul>");
        foreach (var item in items)
        {
            html.Append("<li>");
            if (item.Color is not null)
                html.Append($"<span class=\"swatch\" style=\"background:{Escape(item.Color)}\"></span>");
            html.Append(Escape(item.Text));
            if (item.Detail is not null)
                html.Append(" <span class=\"detail\">— ").Append(Escape(item.Detail)).Append("</span>");
            html.Append("</li>");
        }

        html.Append("</ul></div>");
    }

    // ------------------------------------------------------------------
    // Yordamchi
    // ------------------------------------------------------------------

    private static string LoadTemplate()
    {
        if (_template is not null)
            return _template;

        var assembly = typeof(TimetableHtmlExporter).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(TemplateResource)
            ?? throw new InvalidOperationException(
                $"HTML shabloni topilmadi: {TemplateResource}. csproj dagi EmbeddedResource yozuvini tekshiring.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        _template = reader.ReadToEnd();
        return _template;
    }

    /// <summary>HTML ga xavfsiz joylash uchun belgilarni qochiradi.</summary>
    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var builder = new StringBuilder(text!.Length + 8);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\'': builder.Append("&#39;"); break;
                default: builder.Append(ch); break;
            }
        }

        return builder.ToString();
    }
}
