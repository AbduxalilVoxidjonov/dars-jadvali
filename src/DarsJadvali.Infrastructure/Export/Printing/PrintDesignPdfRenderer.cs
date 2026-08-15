using System.Globalization;
using DarsJadvali.Domain.Common;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>
/// Dizayn ta'rifi + <see cref="PrintableTimetable"/> → PDF.
/// </summary>
/// <remarks>
/// <para>
/// Kutubxona — <b>PDFsharp 6.2</b>, loyihada allaqachon mavjud (MIT, cheklovsiz).
/// Yangi paket QO'SHILMADI: HTML→PDF (Chromium/QuestPDF) yo'li desktop ilovaga
/// og'ir yuk va litsenziya muammosini olib keladi, PDFsharp esa shriftni fayl ichiga
/// o'rnatadi va offline ishlaydi.
/// </para>
/// <para>
/// Shrift — <see cref="EmbeddedFontResolver"/> orqali assembly ichiga o'rnatilgan
/// DejaVu Sans Condensed. U kirill (Кирилл) va o'zbek lotin belgilarini
/// (oʻ U+02BB, gʻ, ʼ U+02BC) to'liq qamrab oladi.
/// </para>
/// </remarks>
public sealed class PrintDesignPdfRenderer
{
    private const double MmToPoint = 72.0 / 25.4;
    private const double MinFontSize = 3.5;

    /// <summary>Jadvalni dizayn bo'yicha PDF ga chizadi.</summary>
    /// <param name="design">Dizayn ta'rifi.</param>
    /// <param name="timetable">Chop etiladigan jadval.</param>
    /// <param name="ct">Bekor qilish belgisi.</param>
    /// <returns>PDF baytlari (<c>%PDF</c> bilan boshlanadi).</returns>
    public byte[] Render(PrintDesign design, PrintableTimetable timetable, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(timetable);

        EmbeddedFontResolver.EnsureInstalled();

        var grid = design.Grid;
        var layouts = TimetableGridLayout.BuildAll(timetable);
        var pages = grid is null
            ? new List<IReadOnlyList<TimetableLayout>> { Array.Empty<TimetableLayout>() }
            : (List<IReadOnlyList<TimetableLayout>>)TimetableGridLayout
                .Paginate(layouts, grid.SectionsPerPage)
                .ToList();

        var resolver = new PrintTokenResolver(timetable);

        using var document = new PdfDocument();
        document.Info.Title = resolver.Resolve("{Scope.Title}");
        document.Info.Author = AppInfo.Author;
        document.Info.Creator = AppInfo.AppName;
        document.Info.Subject = design.Name;

        var (pageWidth, pageHeight) = PageSize(design.Page);

        for (var index = 0; index < pages.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            var page = document.AddPage();
            page.Size = design.Page.Size == PrintPageSize.A3 ? PdfSharp.PageSize.A3 : PdfSharp.PageSize.A4;
            page.Orientation = design.Page.Orientation == PrintOrientation.Landscape
                ? PdfSharp.PageOrientation.Landscape
                : PdfSharp.PageOrientation.Portrait;

            using var gfx = XGraphics.FromPdfPage(page);

            var pageLayouts = pages[index];
            resolver.SetPageContext(index + 1, pages.Count, pageLayouts.Count > 0 ? pageLayouts[0].Section : null);

            var margin = design.Page.MarginMm * MmToPoint;
            var content = new XRect(margin, margin, pageWidth - 2 * margin, pageHeight - 2 * margin);

            foreach (var element in design.Elements)
            {
                ct.ThrowIfCancellationRequested();
                var rect = Absolute(element.Rect, content);

                switch (element)
                {
                    case PrintTextElement text:
                        DrawText(gfx, text, rect, resolver, design.Theme, pageHeight);
                        break;

                    case PrintLineElement line:
                        DrawLine(gfx, line, rect, design.Theme);
                        break;

                    case PrintTimetableElement timetableElement:
                        DrawGrid(gfx, timetableElement, rect, timetable, pageLayouts, design.Theme, pageHeight);
                        break;

                    case PrintLegendElement legend:
                        DrawLegend(gfx, legend, rect, pageLayouts, design.Theme, pageHeight);
                        break;
                }
            }
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    // ------------------------------------------------------------------
    // Sahifa geometriyasi
    // ------------------------------------------------------------------

    private static (double Width, double Height) PageSize(PrintPage page)
    {
        var (shortSide, longSide) = page.Size == PrintPageSize.A3
            ? (841.89, 1190.55)
            : (595.28, 841.89);

        return page.Orientation == PrintOrientation.Landscape
            ? (longSide, shortSide)
            : (shortSide, longSide);
    }

    private static XRect Absolute(PrintRect rect, XRect content) => new(
        content.X + rect.Left * content.Width,
        content.Y + rect.Top * content.Height,
        Math.Max(0, rect.Width) * content.Width,
        Math.Max(0, rect.Height) * content.Height);

    // ------------------------------------------------------------------
    // Matn va chiziq
    // ------------------------------------------------------------------

    private static void DrawText(
        XGraphics gfx,
        PrintTextElement element,
        XRect rect,
        PrintTokenResolver resolver,
        PrintTheme theme,
        double pageHeight)
    {
        var text = resolver.Resolve(element.Text);
        if (element.Background is not null)
            gfx.DrawRectangle(new XSolidBrush(PrintColor.Parse(element.Background, XColors.White)), rect);

        if (text.Length == 0)
            return;

        var style = element.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
        if (element.Italic)
            style |= XFontStyleEx.Italic;

        var font = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, element.FontRatio * pageHeight), style);
        var brush = new XSolidBrush(PrintColor.Parse(element.Color ?? theme.Accent, XColors.Black));

        var fitted = Fit(gfx, text, font, rect.Width);
        gfx.DrawString(fitted, font, brush, rect, Format(element.Align, vertical: true));
    }

    private static void DrawLine(XGraphics gfx, PrintLineElement element, XRect rect, PrintTheme theme)
    {
        var color = PrintColor.Parse(element.Color ?? theme.Accent, XColors.Black);

        if (element.Box)
        {
            if (element.Fill is not null)
                gfx.DrawRectangle(new XSolidBrush(PrintColor.Parse(element.Fill, XColors.White)), rect);

            if (element.Thickness > 0)
                gfx.DrawRectangle(new XPen(color, element.Thickness), rect);

            return;
        }

        if (element.Thickness <= 0)
            return;

        // Balandligi kengligidan kichik bo'lsa — gorizontal, aks holda vertikal chiziq.
        var pen = new XPen(color, element.Thickness);
        if (rect.Height <= rect.Width)
        {
            var y = rect.Y + rect.Height / 2;
            gfx.DrawLine(pen, rect.X, y, rect.X + rect.Width, y);
        }
        else
        {
            var x = rect.X + rect.Width / 2;
            gfx.DrawLine(pen, x, rect.Y, x, rect.Y + rect.Height);
        }
    }

    // ------------------------------------------------------------------
    // Jadval to'ri
    // ------------------------------------------------------------------

    private static void DrawGrid(
        XGraphics gfx,
        PrintTimetableElement element,
        XRect rect,
        PrintableTimetable timetable,
        IReadOnlyList<TimetableLayout> layouts,
        PrintTheme theme,
        double pageHeight)
    {
        if (rect.Width <= 1 || rect.Height <= 1)
            return;

        if (timetable.Days.Count == 0 || timetable.Periods.Count == 0 || layouts.Count == 0)
        {
            var font = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, element.CaptionFontRatio * pageHeight), XFontStyleEx.Regular);
            gfx.DrawString(PrintableTimetable.EmptyMessage, font, XBrushes.Gray, rect, XStringFormats.Center);
            return;
        }

        var gap = layouts.Count > 1 ? Math.Min(10, rect.Height * 0.02) : 0;
        var sectionHeight = (rect.Height - gap * (layouts.Count - 1)) / layouts.Count;

        for (var i = 0; i < layouts.Count; i++)
        {
            var area = new XRect(rect.X, rect.Y + i * (sectionHeight + gap), rect.Width, sectionHeight);
            DrawSection(gfx, element, area, timetable, layouts[i], theme, pageHeight);
        }
    }

    private static void DrawSection(
        XGraphics gfx,
        PrintTimetableElement element,
        XRect area,
        PrintableTimetable timetable,
        TimetableLayout layout,
        PrintTheme theme,
        double pageHeight)
    {
        var captionFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, element.CaptionFontRatio * pageHeight), XFontStyleEx.Bold);

        if (element.ShowSectionCaption)
        {
            var captionHeight = Math.Min(area.Height * 0.25, captionFont.GetHeight() * 1.5);
            var captionRect = new XRect(area.X, area.Y, area.Width, captionHeight);

            gfx.DrawRectangle(new XSolidBrush(PrintColor.Parse(theme.HeaderBackground, XColors.SteelBlue)), captionRect);

            var caption = string.IsNullOrWhiteSpace(layout.Section.SubCaption)
                ? layout.Section.Caption
                : $"{layout.Section.Caption}   ·   {layout.Section.SubCaption}";

            gfx.DrawString(
                Fit(gfx, caption, captionFont, captionRect.Width - 8),
                captionFont,
                new XSolidBrush(PrintColor.Parse(theme.HeaderForeground, XColors.White)),
                new XRect(captionRect.X + 4, captionRect.Y, captionRect.Width - 8, captionRect.Height),
                XStringFormats.CenterLeft);

            area = new XRect(area.X, area.Y + captionHeight, area.Width, area.Height - captionHeight);
        }

        if (area.Height <= 2)
            return;

        var shifts = timetable.ShiftNames;
        var showShiftBand = element.ShowShift && shifts.Count > 1;

        var headerFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, element.HeaderFontRatio * pageHeight), XFontStyleEx.Bold);
        var cellFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, element.CellFontRatio * pageHeight), XFontStyleEx.Regular);

        var dayCount = timetable.Days.Count;
        var periodCount = timetable.Periods.Count;

        var gridLine = PrintColor.Parse(theme.GridLine, XColors.Gray);
        var thin = new XPen(gridLine, 0.4);
        var thick = new XPen(gridLine, 1.0);

        if (element.Axis == PrintGridAxis.DaysAsColumns)
        {
            var bandWidth = showShiftBand ? Math.Min(16.0, area.Width * 0.035) : 0;
            var headerWidth = Math.Min(area.Width * (element.ShowTime ? 0.13 : 0.08), 90);
            var headerHeight = Math.Min(area.Height * 0.16, headerFont.GetHeight() * 1.8);

            var bodyX = area.X + bandWidth + headerWidth;
            var bodyY = area.Y + headerHeight;
            var dayWidth = (area.Width - bandWidth - headerWidth) / dayCount;
            var rowHeight = (area.Height - headerHeight) / periodCount;

            // Kun sarlavhalari.
            gfx.DrawRectangle(
                new XSolidBrush(PrintColor.Parse(theme.HeaderBackground, XColors.SteelBlue)),
                new XRect(area.X, area.Y, area.Width, headerHeight));

            var headerBrush = new XSolidBrush(PrintColor.Parse(theme.HeaderForeground, XColors.White));
            for (var d = 0; d < dayCount; d++)
            {
                var cell = new XRect(bodyX + d * dayWidth, area.Y, dayWidth, headerHeight);
                gfx.DrawString(DayLabel(gfx, timetable.Days[d], headerFont, cell.Width - 2), headerFont, headerBrush, cell, XStringFormats.Center);
            }

            gfx.DrawString("Soat", headerFont, headerBrush,
                new XRect(area.X + bandWidth, area.Y, headerWidth, headerHeight), XStringFormats.Center);

            // Soat ustuni + smena polosasi.
            DrawPeriodHeadersVertical(gfx, timetable, element, area, bandWidth, headerWidth, bodyY, rowHeight, headerFont, theme, showShiftBand);

            // Bo'sh kataklar foni + to'r chiziqlari.
            gfx.DrawRectangle(
                new XSolidBrush(PrintColor.Parse(theme.EmptyBackground, XColors.White)),
                new XRect(bodyX, bodyY, dayWidth * dayCount, rowHeight * periodCount));

            for (var r = 0; r <= periodCount; r++)
            {
                var y = bodyY + r * rowHeight;
                gfx.DrawLine(r == 0 || r == periodCount ? thick : thin, area.X + bandWidth, y, bodyX + dayWidth * dayCount, y);
            }

            for (var d = 0; d <= dayCount; d++)
            {
                var x = bodyX + d * dayWidth;
                gfx.DrawLine(thick, x, area.Y, x, bodyY + rowHeight * periodCount);
            }

            gfx.DrawLine(thick, area.X + bandWidth, area.Y, area.X + bandWidth, bodyY + rowHeight * periodCount);

            // Bloklar.
            foreach (var block in layout.Blocks)
            {
                var laneWidth = dayWidth / block.LaneCount;
                var blockRect = new XRect(
                    bodyX + block.DayIndex * dayWidth + block.Lane * laneWidth,
                    bodyY + block.RowIndex * rowHeight,
                    laneWidth,
                    rowHeight * block.RowSpan);

                DrawBlock(gfx, block, blockRect, element, theme, cellFont);
            }
        }
        else
        {
            var bandHeight = showShiftBand ? Math.Min(14.0, area.Height * 0.06) : 0;
            var headerWidth = Math.Min(area.Width * 0.12, 90);
            var headerHeight = Math.Min(area.Height * 0.16, headerFont.GetHeight() * 1.8);

            var bodyX = area.X + headerWidth;
            var bodyY = area.Y + bandHeight + headerHeight;
            var periodWidth = (area.Width - headerWidth) / periodCount;
            var dayHeight = (area.Height - bandHeight - headerHeight) / dayCount;

            gfx.DrawRectangle(
                new XSolidBrush(PrintColor.Parse(theme.HeaderBackground, XColors.SteelBlue)),
                new XRect(area.X, area.Y + bandHeight, area.Width, headerHeight));

            var headerBrush = new XSolidBrush(PrintColor.Parse(theme.HeaderForeground, XColors.White));

            if (showShiftBand)
                DrawShiftBandHorizontal(gfx, timetable, area, headerWidth, periodWidth, bandHeight, headerFont, theme);

            for (var p = 0; p < periodCount; p++)
            {
                var cell = new XRect(bodyX + p * periodWidth, area.Y + bandHeight, periodWidth, headerHeight);
                var label = element.ShowTime && timetable.Periods[p].TimeLabel is not null
                    ? $"{timetable.Periods[p].Label}"
                    : timetable.Periods[p].Label;
                gfx.DrawString(Fit(gfx, label, headerFont, cell.Width - 2), headerFont, headerBrush, cell, XStringFormats.Center);
            }

            gfx.DrawString("Kun", headerFont, headerBrush,
                new XRect(area.X, area.Y + bandHeight, headerWidth, headerHeight), XStringFormats.Center);

            gfx.DrawRectangle(
                new XSolidBrush(PrintColor.Parse(theme.EmptyBackground, XColors.White)),
                new XRect(bodyX, bodyY, periodWidth * periodCount, dayHeight * dayCount));

            for (var d = 0; d < dayCount; d++)
            {
                var cell = new XRect(area.X, bodyY + d * dayHeight, headerWidth, dayHeight);
                gfx.DrawString(
                    Fit(gfx, timetable.Days[d].DisplayShort, headerFont, cell.Width - 2),
                    headerFont,
                    new XSolidBrush(PrintColor.Parse(theme.CardForeground, XColors.Black)),
                    cell,
                    XStringFormats.Center);
            }

            for (var d = 0; d <= dayCount; d++)
            {
                var y = bodyY + d * dayHeight;
                gfx.DrawLine(thick, area.X, y, bodyX + periodWidth * periodCount, y);
            }

            for (var p = 0; p <= periodCount; p++)
            {
                var x = bodyX + p * periodWidth;
                gfx.DrawLine(p == 0 || p == periodCount ? thick : thin, x, area.Y + bandHeight, x, bodyY + dayHeight * dayCount);
            }

            foreach (var block in layout.Blocks)
            {
                var laneHeight = dayHeight / block.LaneCount;
                var blockRect = new XRect(
                    bodyX + block.RowIndex * periodWidth,
                    bodyY + block.DayIndex * dayHeight + block.Lane * laneHeight,
                    periodWidth * block.RowSpan,
                    laneHeight);

                DrawBlock(gfx, block, blockRect, element, theme, cellFont);
            }
        }
    }

    private static void DrawPeriodHeadersVertical(
        XGraphics gfx,
        PrintableTimetable timetable,
        PrintTimetableElement element,
        XRect area,
        double bandWidth,
        double headerWidth,
        double bodyY,
        double rowHeight,
        XFont headerFont,
        PrintTheme theme,
        bool showShiftBand)
    {
        var textBrush = new XSolidBrush(PrintColor.Parse(theme.CardForeground, XColors.Black));
        var mutedBrush = new XSolidBrush(PrintColor.Parse(theme.Muted, XColors.Gray));
        var timeFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, headerFont.Size * 0.72), XFontStyleEx.Regular);

        for (var p = 0; p < timetable.Periods.Count; p++)
        {
            var period = timetable.Periods[p];
            var cell = new XRect(area.X + bandWidth, bodyY + p * rowHeight, headerWidth, rowHeight);

            if (element.ShowTime && !string.IsNullOrWhiteSpace(period.TimeLabel) && rowHeight > headerFont.GetHeight() * 1.6)
            {
                var half = cell.Height / 2;
                gfx.DrawString(period.Label, headerFont, textBrush, new XRect(cell.X, cell.Y, cell.Width, half), XStringFormats.Center);

                // Vaqt oralig'i ("08:00-08:45") kesilmasin: sig'maguncha shrift kichrayadi.
                var fitted = Shrink(gfx, period.TimeLabel!, timeFont, cell.Width - 2);
                gfx.DrawString(
                    Fit(gfx, period.TimeLabel!, fitted, cell.Width - 2),
                    fitted, mutedBrush,
                    new XRect(cell.X, cell.Y + half, cell.Width, half), XStringFormats.Center);
            }
            else
            {
                gfx.DrawString(period.Label, headerFont, textBrush, cell, XStringFormats.Center);
            }
        }

        if (!showShiftBand || bandWidth <= 0)
            return;

        // Smena polosasi — chapdagi tor ustun, matn 90° burilgan.
        var shiftFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, headerFont.Size * 0.8), XFontStyleEx.Bold);
        var start = 0;

        while (start < timetable.Periods.Count)
        {
            var name = timetable.Periods[start].ShiftName;
            var end = start;
            while (end + 1 < timetable.Periods.Count &&
                   string.Equals(timetable.Periods[end + 1].ShiftName, name, StringComparison.Ordinal))
            {
                end++;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var band = new XRect(area.X, bodyY + start * rowHeight, bandWidth, (end - start + 1) * rowHeight);
                gfx.DrawRectangle(new XSolidBrush(PrintColor.Parse(theme.Accent, XColors.SteelBlue)), band);
                DrawRotatedText(gfx, name!, shiftFont, new XSolidBrush(PrintColor.Parse(theme.HeaderForeground, XColors.White)), band);
            }

            start = end + 1;
        }
    }

    private static void DrawShiftBandHorizontal(
        XGraphics gfx,
        PrintableTimetable timetable,
        XRect area,
        double headerWidth,
        double periodWidth,
        double bandHeight,
        XFont headerFont,
        PrintTheme theme)
    {
        var shiftFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, headerFont.Size * 0.8), XFontStyleEx.Bold);
        var start = 0;

        while (start < timetable.Periods.Count)
        {
            var name = timetable.Periods[start].ShiftName;
            var end = start;
            while (end + 1 < timetable.Periods.Count &&
                   string.Equals(timetable.Periods[end + 1].ShiftName, name, StringComparison.Ordinal))
            {
                end++;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var band = new XRect(area.X + headerWidth + start * periodWidth, area.Y, (end - start + 1) * periodWidth, bandHeight);
                gfx.DrawRectangle(new XSolidBrush(PrintColor.Parse(theme.Accent, XColors.SteelBlue)), band);
                gfx.DrawString(
                    Fit(gfx, name!, shiftFont, band.Width - 2),
                    shiftFont,
                    new XSolidBrush(PrintColor.Parse(theme.HeaderForeground, XColors.White)),
                    band,
                    XStringFormats.Center);
            }

            start = end + 1;
        }
    }

    /// <summary>Matnni to'rtburchak markazida 90° burib chizadi (tor vertikal polosalar uchun).</summary>
    private static void DrawRotatedText(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect)
    {
        var state = gfx.Save();
        gfx.TranslateTransform(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        gfx.RotateTransform(-90);
        gfx.DrawString(
            Fit(gfx, text, font, rect.Height - 4),
            font,
            brush,
            new XRect(-rect.Height / 2, -rect.Width / 2, rect.Height, rect.Width),
            XStringFormats.Center);
        gfx.Restore(state);
    }

    /// <summary>
    /// Bitta dars blokini chizadi. Juft dars (<c>RowSpan &gt; 1</c>) bitta yaxlit
    /// to'rtburchak bo'lib chiqadi — ichida ajratuvchi chiziq yo'q.
    /// </summary>
    private static void DrawBlock(
        XGraphics gfx,
        TimetableBlock block,
        XRect rect,
        PrintTimetableElement element,
        PrintTheme theme,
        XFont baseFont)
    {
        if (rect.Width <= 1 || rect.Height <= 1)
            return;

        var card = block.Card;

        var background = element.ColorBy switch
        {
            PrintColorSource.None => PrintColor.Parse(theme.CardBackground, XColors.LightSteelBlue),
            PrintColorSource.Subject => PrintColor.Parse(PrintColor.FromName(card.SubjectName), XColors.LightSteelBlue),
            _ => PrintColor.Parse(card.ColorCode ?? theme.CardBackground, XColors.LightSteelBlue),
        };

        var inner = new XRect(rect.X + 0.6, rect.Y + 0.6, Math.Max(0, rect.Width - 1.2), Math.Max(0, rect.Height - 1.2));
        gfx.DrawRectangle(new XSolidBrush(background), inner);

        // Juft dars chekkasi qalinroq — yaxlit blok ekani ko'zga tashlansin.
        if (block.IsDouble)
            gfx.DrawRectangle(new XPen(PrintColor.Parse(theme.Accent, XColors.SteelBlue), 1.2), inner);

        // Guruh darsi: yo'lakchalar orasida ingichka ajratuvchi.
        if (block.IsShared && block.Lane > 0)
            gfx.DrawLine(new XPen(PrintColor.Parse(theme.GridLine, XColors.Gray), 0.6), rect.X, rect.Y, rect.X, rect.Y + rect.Height);

        var textColor = PrintColor.IsLight(background)
            ? PrintColor.Parse(theme.CardForeground, XColors.Black)
            : XColors.White;

        var lines = BuildCellLines(card, element, block.IsShared);
        if (lines.Count == 0)
            return;

        // Shriftni katakka sig'dirish.
        var size = baseFont.Size;
        if (block.IsShared)
            size *= 0.88;

        var font = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, size), XFontStyleEx.Bold);
        var lineHeight = font.GetHeight();

        while (lineHeight * lines.Count > inner.Height - 1 && size > MinFontSize)
        {
            size -= 0.5;
            font = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, size), XFontStyleEx.Bold);
            lineHeight = font.GetHeight();
        }

        var mutedFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, size * 0.86), XFontStyleEx.Regular);
        var mutedBrush = new XSolidBrush(PrintColor.IsLight(background)
            ? PrintColor.Parse(theme.Muted, XColors.DimGray)
            : XColors.White);
        var mainBrush = new XSolidBrush(textColor);

        var totalHeight = lineHeight + mutedFont.GetHeight() * (lines.Count - 1);
        var y = inner.Y + Math.Max(0, (inner.Height - totalHeight) / 2);

        for (var i = 0; i < lines.Count; i++)
        {
            var isFirst = i == 0;
            var lineFont = isFirst ? font : mutedFont;
            var height = lineFont.GetHeight();

            if (y + height > inner.Y + inner.Height + 0.5)
                break;

            gfx.DrawString(
                Fit(gfx, lines[i], lineFont, inner.Width - 2),
                lineFont,
                isFirst ? mainBrush : mutedBrush,
                new XRect(inner.X + 1, y, inner.Width - 2, height),
                XStringFormats.Center);

            y += height;
        }

        // A/B hafta belgisi — o'ng yuqori burchakda.
        if (element.ShowWeeks && card.WeekLabel is not null && inner.Width > 10 && inner.Height > 8)
        {
            var badgeFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, size * 0.8), XFontStyleEx.Bold);
            var badgeSize = Math.Min(badgeFont.GetHeight(), Math.Min(inner.Width / 3, inner.Height / 2));
            var badge = new XRect(inner.X + inner.Width - badgeSize - 1, inner.Y + 1, badgeSize, badgeSize);

            gfx.DrawRectangle(new XSolidBrush(PrintColor.Parse(theme.Accent, XColors.SteelBlue)), badge);
            gfx.DrawString(card.WeekLabel!, badgeFont, XBrushes.White, badge, XStringFormats.Center);
        }
    }

    /// <summary>Katakdagi matn qatorlari: 1-qator — fan, keyingilari — qo'shimcha ma'lumot.</summary>
    /// <param name="card">Karta.</param>
    /// <param name="element">To'r sozlamalari.</param>
    /// <param name="shared">Katak guruhlarga bo'lingan (yo'lakcha tor) — matn qisqaroq bo'lsin.</param>
    private static List<string> BuildCellLines(PrintableCard card, PrintTimetableElement element, bool shared)
    {
        var lines = new List<string>(4);

        var subject = element.UseShortSubject ? card.DisplaySubject : card.SubjectName;
        var hasGroup = element.ShowGroup && !string.IsNullOrWhiteSpace(card.GroupName);

        // Tor yo'lakchada "Fan (1-guruh)" sig'maydi va fan nomi kesiladi —
        // guruh alohida qatorga tushadi.
        if (hasGroup && !shared)
            subject = $"{subject} ({card.GroupName})";

        if (!string.IsNullOrWhiteSpace(subject))
            lines.Add(subject);

        if (hasGroup && shared)
            lines.Add(card.GroupName!);

        if (element.ShowClass && !string.IsNullOrWhiteSpace(card.ClassName))
            lines.Add(card.ClassName!);

        if (element.ShowTeacher && card.TeacherNames.Count > 0)
        {
            var teachers = card.TeacherLine;
            if (teachers.Length > 0)
                lines.Add(teachers);
        }

        if (element.ShowRoom && !string.IsNullOrWhiteSpace(card.RoomName))
            lines.Add(card.RoomName!.Trim());

        return lines;
    }

    // ------------------------------------------------------------------
    // Legenda
    // ------------------------------------------------------------------

    private static void DrawLegend(
        XGraphics gfx,
        PrintLegendElement element,
        XRect rect,
        IReadOnlyList<TimetableLayout> layouts,
        PrintTheme theme,
        double pageHeight)
    {
        if (rect.Width <= 1 || rect.Height <= 1)
            return;

        var cards = layouts.SelectMany(l => l.Section.Cards).ToList();
        if (cards.Count == 0)
            return;

        var items = PrintLegendBuilder.Build(cards, element.Legend, PrintColorSource.Card, element.MaxItems);
        if (items.Count == 0)
            return;

        var titleFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, element.FontRatio * pageHeight * 1.25), XFontStyleEx.Bold);
        var itemFont = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, element.FontRatio * pageHeight), XFontStyleEx.Regular);

        var y = rect.Y;
        var title = element.Title ?? PrintLegendBuilder.DefaultTitle(element.Legend);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleHeight = titleFont.GetHeight();
            gfx.DrawString(
                Fit(gfx, title, titleFont, rect.Width),
                titleFont,
                new XSolidBrush(PrintColor.Parse(theme.Accent, XColors.SteelBlue)),
                new XRect(rect.X, y, rect.Width, titleHeight),
                XStringFormats.CenterLeft);

            y += titleHeight + 1;
        }

        var availableHeight = rect.Y + rect.Height - y;
        if (availableHeight <= 2)
            return;

        var rowHeight = itemFont.GetHeight() * 1.15;
        var rowsPerColumn = Math.Max(1, (int)(availableHeight / rowHeight));
        var columns = Math.Max(1, Math.Min(element.Columns, (int)Math.Ceiling(items.Count / (double)rowsPerColumn)));
        var columnWidth = rect.Width / columns;

        var textBrush = new XSolidBrush(PrintColor.Parse(theme.CardForeground, XColors.Black));
        var mutedBrush = new XSolidBrush(PrintColor.Parse(theme.Muted, XColors.DimGray));

        for (var i = 0; i < items.Count; i++)
        {
            var column = i / rowsPerColumn;
            if (column >= columns)
                break;

            var row = i % rowsPerColumn;
            var x = rect.X + column * columnWidth;
            var itemY = y + row * rowHeight;

            var textX = x;
            var textWidth = columnWidth - 4;

            if (element.ShowColors && items[i].Color is not null)
            {
                var swatch = Math.Min(rowHeight * 0.6, 8);
                gfx.DrawRectangle(
                    new XSolidBrush(PrintColor.Parse(items[i].Color, XColors.LightGray)),
                    new XRect(x, itemY + (rowHeight - swatch) / 2, swatch, swatch));
                gfx.DrawRectangle(
                    new XPen(PrintColor.Parse(theme.GridLine, XColors.Gray), 0.3),
                    new XRect(x, itemY + (rowHeight - swatch) / 2, swatch, swatch));

                textX += swatch + 3;
                textWidth -= swatch + 3;
            }

            if (textWidth <= 2)
                continue;

            var detail = items[i].Detail;
            var mainWidth = detail is null ? textWidth : textWidth * 0.6;

            gfx.DrawString(
                Fit(gfx, items[i].Text, itemFont, mainWidth),
                itemFont, textBrush,
                new XRect(textX, itemY, mainWidth, rowHeight),
                XStringFormats.CenterLeft);

            if (detail is not null && textWidth - mainWidth > 4)
            {
                gfx.DrawString(
                    Fit(gfx, detail, itemFont, textWidth - mainWidth),
                    itemFont, mutedBrush,
                    new XRect(textX + mainWidth, itemY, textWidth - mainWidth, rowHeight),
                    XStringFormats.CenterRight);
            }
        }
    }

    // ------------------------------------------------------------------
    // Yordamchi
    // ------------------------------------------------------------------

    private static XStringFormat Format(PrintAlign align, bool vertical) => (align, vertical) switch
    {
        (PrintAlign.Center, true) => XStringFormats.Center,
        (PrintAlign.Right, true) => XStringFormats.CenterRight,
        (PrintAlign.Center, false) => XStringFormats.TopCenter,
        (PrintAlign.Right, false) => XStringFormats.TopRight,
        (_, true) => XStringFormats.CenterLeft,
        _ => XStringFormats.TopLeft,
    };

    /// <summary>
    /// Kun sarlavhasi: to'liq nom sig'masa qisqartmaga ("Dushanba" → "Du") o'tadi,
    /// chunki "Dusha…" hech kimga hech narsa aytmaydi.
    /// </summary>
    private static string DayLabel(XGraphics gfx, PrintableDay day, XFont font, double maxWidth)
    {
        if (maxWidth <= 1 || gfx.MeasureString(day.Name, font).Width <= maxWidth)
            return day.Name;

        var shortName = day.DisplayShort;
        return gfx.MeasureString(shortName, font).Width <= maxWidth
            ? shortName
            : Fit(gfx, shortName, font, maxWidth);
    }

    /// <summary>Matn sig'maguncha shriftni kichraytiradi (kesish o'rniga).</summary>
    private static XFont Shrink(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        if (maxWidth <= 1 || string.IsNullOrEmpty(text))
            return font;

        var current = font;
        var size = font.Size;

        while (gfx.MeasureString(text, current).Width > maxWidth && size > MinFontSize)
        {
            size -= 0.25;
            current = new XFont(EmbeddedFontResolver.FamilyName, Math.Max(MinFontSize, size), font.Style);
        }

        return current;
    }

    /// <summary>Matnni kenglikka sig'diradi, kerak bo'lsa "…" bilan qisqartiradi.</summary>
    private static string Fit(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 1)
            return text ?? string.Empty;

        if (gfx.MeasureString(text, font).Width <= maxWidth)
            return text;

        var candidate = text;
        while (candidate.Length > 1)
        {
            candidate = candidate[..^1];
            if (gfx.MeasureString(candidate + "…", font).Width <= maxWidth)
                return candidate.TrimEnd() + "…";
        }

        return candidate;
    }

    /// <summary>Sahifalar sonini oldindan hisoblaydi (test va oldindan ko'rish uchun).</summary>
    /// <param name="design">Dizayn.</param>
    /// <param name="timetable">Jadval.</param>
    public static int CountPages(PrintDesign design, PrintableTimetable timetable)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(timetable);

        var grid = design.Grid;
        if (grid is null)
            return 1;

        var layouts = TimetableGridLayout.BuildAll(timetable);
        return TimetableGridLayout.Paginate(layouts, grid.SectionsPerPage).Count;
    }

    /// <summary>Sana matni (kolontitulda foydali).</summary>
    /// <param name="value">Sana.</param>
    public static string FormatDate(DateTime value) => value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}
