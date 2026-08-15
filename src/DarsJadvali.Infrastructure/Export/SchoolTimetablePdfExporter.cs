using System.Globalization;
using System.Text;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DarsJadvali.Infrastructure.Export;

/// <summary>
/// Butun maktab dars jadvalini bitta PDF jadval ko'rinishida chizadi.
/// Ustunlar: Sinf | Soat | faol ish kunlari. Har sinf N ta qator egallaydi,
/// sinf nomi shu qatorlar bo'ylab bir marta (vertikal birlashtirilgan) yoziladi.
/// </summary>
public sealed class SchoolTimetablePdfExporter : ISchoolTimetablePdfExporter, IScopedTimetablePdfExporter
{
    private readonly ITimetableExportModelBuilder _builder;
    private readonly IScheduleService? _schedules;
    private readonly IWorkDayService? _workDays;
    private readonly ITeacherService? _teachers;
    private readonly IClassGroupService? _classGroups;

    /// <summary>
    /// Faqat sinf va maktab qamrovi uchun yetarli qurilma.
    /// O'qituvchi jadvali bu qurilmada MAVJUD EMAS (unga jadval servislari kerak).
    /// </summary>
    public SchoolTimetablePdfExporter(ITimetableExportModelBuilder builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>
    /// To'liq qurilma — DI konteyner aynan shuni tanlaydi. O'qituvchi jadvali
    /// <see cref="PdfExportOptions"/> da qamrov sifatida yo'q (u faqat sinfni biladi),
    /// shuning uchun uning modeli shu servislar orqali shu yerda quriladi.
    /// </summary>
    public SchoolTimetablePdfExporter(
        ITimetableExportModelBuilder builder,
        IScheduleService schedules,
        IWorkDayService workDays,
        ITeacherService teachers,
        IClassGroupService classGroups)
        : this(builder)
    {
        _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        _workDays = workDays ?? throw new ArgumentNullException(nameof(workDays));
        _teachers = teachers ?? throw new ArgumentNullException(nameof(teachers));
        _classGroups = classGroups ?? throw new ArgumentNullException(nameof(classGroups));
    }

    // ------------------------------------------------------------------
    // O'lchamlar (punktda: 1 pt = 1/72 dyuym)
    // ------------------------------------------------------------------
    private const double MarginLeft = 28;
    private const double MarginRight = 28;
    private const double MarginTop = 26;
    private const double MarginBottom = 30;

    private const double HeaderBlockHeight = 44;   // maktab nomi + "Dars jadvali" + sana
    private const double FooterBlockHeight = 20;   // sahifa raqami + dastur nomi
    private const double TableHeaderHeight = 20;
    private const double MinRowHeight = 18;
    private const double CellPadX = 2.5;
    private const double CellPadY = 2;

    private const double ClassColumnWidth = 54;
    private const double LessonColumnWidth = 76;

    /// <inheritdoc />
    [Obsolete("Qamrov aniq emas: ClassGroupId null bo'lsa jimgina BUTUN MAKTAB jadvali chiqadi. " +
              "IScopedTimetablePdfExporter ning ExportClassScheduleAsync / ExportTeacherScheduleAsync / " +
              "ExportSchoolScheduleAsync metodlaridan foydalaning.",
              DiagnosticId = "DJ0001")]
    public async Task<byte[]> ExportAsync(PdfExportOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var model = await _builder.BuildAsync(options, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        return Render(model, options, DateTime.Now, ct);
    }

    /// <inheritdoc />
    public string SuggestFileName(PdfExportOptions options, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"Maktab-jadvali-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.pdf";
    }

    // ==================================================================
    // Qamrovi ANIQ eksport (IScopedTimetablePdfExporter)
    // ==================================================================

    /// <inheritdoc />
    public async Task<TimetablePdfDocument> ExportClassScheduleAsync(
        int classGroupId, PdfExportOptions? options = null, CancellationToken ct = default)
    {
        if (classGroupId <= 0)
        {
            throw new ArgumentException(
                "Sinf tanlanmagan: butun maktab jadvalini chiqarish uchun ExportSchoolScheduleAsync ishlatiladi.",
                nameof(classGroupId));
        }

        var scoped = (options ?? new PdfExportOptions()) with { ClassGroupId = classGroupId };

        var model = await _builder.BuildAsync(scoped, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var className = model.Blocks.Count > 0 ? model.Blocks[0].ClassName : classGroupId.ToString(CultureInfo.InvariantCulture);
        var bytes = Render(model, scoped, DateTime.Now, ct);

        return new TimetablePdfDocument(bytes, BuildFileName(className + "-sinf-jadvali", DateTime.Now));
    }

    /// <inheritdoc />
    public async Task<TimetablePdfDocument> ExportSchoolScheduleAsync(
        PdfExportOptions? options = null, CancellationToken ct = default)
    {
        // Qamrov ATAYLAB butun maktab — chaqiruvchi buni metod nomi bilan tasdiqlagan.
        var scoped = (options ?? new PdfExportOptions()) with { ClassGroupId = null };

        var model = await _builder.BuildAsync(scoped, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var bytes = Render(model, scoped, DateTime.Now, ct);

        return new TimetablePdfDocument(bytes, BuildFileName("Maktab-jadvali", DateTime.Now));
    }

    /// <inheritdoc />
    public async Task<TimetablePdfDocument> ExportTeacherScheduleAsync(
        int teacherId, PdfExportOptions? options = null, CancellationToken ct = default)
    {
        if (teacherId <= 0)
            throw new ArgumentException("O'qituvchi tanlanmagan.", nameof(teacherId));

        if (_schedules is null || _workDays is null || _teachers is null || _classGroups is null)
        {
            throw new InvalidOperationException(
                "O'qituvchi jadvali uchun eksportchi to'liq qurilma bilan yaratilishi kerak " +
                "(IScheduleService, IWorkDayService, ITeacherService, IClassGroupService).");
        }

        var teacher = await _teachers.GetByIdAsync(teacherId, ct).ConfigureAwait(false)
            ?? throw new ArgumentException($"O'qituvchi topilmadi (Id={teacherId}).", nameof(teacherId));

        var scoped = (options ?? new PdfExportOptions()) with { ClassGroupId = null };
        var model = await BuildTeacherModelAsync(teacher, scoped, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var bytes = Render(model, scoped, DateTime.Now, ct);

        return new TimetablePdfDocument(bytes, BuildFileName(teacher.FullName + "-jadvali", DateTime.Now));
    }

    /// <summary>
    /// O'qituvchi jadvalining hujjat modeli. Umumiy chizuvchi ishlatilgani uchun
    /// blok sifatida bitta "sinf" — o'qituvchining o'zi olinadi; katakning ikkinchi
    /// qatorida esa o'qituvchi ismi emas, DARS O'TILADIGAN SINF ko'rsatiladi
    /// (o'qituvchi jadvalida kerakli ma'lumot aynan shu).
    /// </summary>
    private async Task<TimetableDocumentModel> BuildTeacherModelAsync(
        Domain.Entities.Teacher teacher, PdfExportOptions options, CancellationToken ct)
    {
        var activeDays = await _workDays!.GetActiveAsync(ct).ConfigureAwait(false);
        var slots = await _workDays.GetLessonSlotsAsync(ct).ConfigureAwait(false);
        var maxLessons = await _workDays.GetMaxLessonNumberAsync(ct).ConfigureAwait(false);
        var groups = await _classGroups!.GetAllAsync(ct).ConfigureAwait(false);
        var entries = await _schedules!
            .GetByTeacherAsync(teacher.Id, options.ScheduleId, ct)
            .ConfigureAwait(false);

        var days = activeDays.Select(d => d.DayOfWeek).Distinct().OrderBy(d => (int)d).ToList();
        var dayNames = days.Select(d => d.ToUzbek()).ToList();
        var dayIndex = days.Select((d, i) => (d, i)).ToDictionary(x => x.d, x => x.i);

        var relevant = entries.Where(e => dayIndex.ContainsKey(e.DayOfWeek)).ToList();

        if (maxLessons <= 0)
            maxLessons = relevant.Count == 0 ? 0 : relevant.Max(e => e.LessonNumber);
        else if (relevant.Count > 0)
            maxLessons = Math.Max(maxLessons, relevant.Max(e => e.LessonNumber));

        var groupNames = groups.ToDictionary(g => g.Id, g => g.Name);
        var groupRooms = groups.ToDictionary(g => g.Id, g => g.RoomNumber);

        // (kun, soat) -> yozuv. Bir vaqtda ikkita dars bo'lsa (nomuvofiq jadval) — birinchisi.
        var byCell = new Dictionary<(Domain.Enums.WeekDay Day, int Lesson), Domain.Entities.ScheduleEntry>();
        foreach (var entry in relevant)
            byCell.TryAdd((entry.DayOfWeek, entry.LessonNumber), entry);

        var timeLabels = new Dictionary<int, string>();
        foreach (var slot in slots)
            timeLabels[slot.LessonNumber] = $"{FormatTime(slot.StartTime)}-{FormatTime(slot.EndTime)}";

        var rows = new List<TimetableRowModel>(Math.Max(maxLessons, 0));
        for (var lesson = 1; lesson <= maxLessons; lesson++)
        {
            var cells = new TimetableCellModel?[days.Count];
            for (var d = 0; d < days.Count; d++)
            {
                if (!byCell.TryGetValue((days[d], lesson), out var entry))
                    continue;

                var subject = entry.Subject?.Name;
                if (string.IsNullOrWhiteSpace(subject))
                    subject = entry.Subject?.Code;
                if (string.IsNullOrWhiteSpace(subject))
                    subject = "(fan ko'rsatilmagan)";

                groupNames.TryGetValue(entry.ClassGroupId, out var className);

                string? room = null;
                if (options.IncludeRoom)
                {
                    room = entry.RoomNumber;
                    if (string.IsNullOrWhiteSpace(room) &&
                        groupRooms.TryGetValue(entry.ClassGroupId, out var groupRoom))
                    {
                        room = groupRoom;
                    }

                    if (string.IsNullOrWhiteSpace(room))
                        room = null;
                }

                cells[d] = new TimetableCellModel(subject!, className, room);
            }

            timeLabels.TryGetValue(lesson, out var time);
            rows.Add(new TimetableRowModel(
                lesson,
                $"{lesson.ToString(CultureInfo.InvariantCulture)}-soat",
                time,
                cells));
        }

        var blocks = days.Count > 0 && maxLessons > 0
            ? new List<TimetableClassBlockModel> { new(0, teacher.FullName, rows) }
            : new List<TimetableClassBlockModel>();

        var schoolName = string.IsNullOrWhiteSpace(options.SchoolName)
            ? teacher.FullName
            : options.SchoolName!.Trim();

        return new TimetableDocumentModel(schoolName, days, dayNames, blocks, relevant.Count);
    }

    private static string FormatTime(TimeSpan time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hours:00}:{time.Minutes:00}");

    /// <summary>Fayl nomini xavfsiz belgilardan quradi.</summary>
    private static string BuildFileName(string label, DateTime now)
    {
        var cleaned = new string(label
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c)
            .ToArray())
            .Trim('-');

        if (cleaned.Length == 0)
            cleaned = "Jadval";

        return $"{cleaned}-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.pdf";
    }

    // ==================================================================
    // Chizish
    // ==================================================================

    private static byte[] Render(
        TimetableDocumentModel model,
        PdfExportOptions options,
        DateTime now,
        CancellationToken ct)
    {
        EmbeddedFontResolver.EnsureInstalled();

        var fonts = new FontSet();

        using var document = new PdfDocument();
        document.Info.Title = model.SchoolName is null
            ? "Dars jadvali"
            : $"{model.SchoolName} — dars jadvali";
        document.Info.Author = AppInfo.Author;
        document.Info.Creator = AppInfo.AppName;
        document.Info.Subject = "Maktab dars jadvali";

        var (pageWidth, pageHeight) = MeasurePageSize(options.Landscape);
        var contentWidth = pageWidth - MarginLeft - MarginRight;
        var tableTop = MarginTop + HeaderBlockHeight;
        var tableBottomLimit = pageHeight - MarginBottom - FooterBlockHeight;

        var dayCount = Math.Max(model.Days.Count, 1);
        var dayColumnWidth = (contentWidth - ClassColumnWidth - LessonColumnWidth) / dayCount;
        if (dayColumnWidth < 30)
            dayColumnWidth = 30;

        if (model.IsEmpty)
        {
            RenderEmptyDocument(document, model, options, now, fonts, pageWidth, pageHeight);
            return ToBytes(document);
        }

        // 1-bosqich: matnni o'lchash va qatorlar balandligini hisoblash.
        List<RenderBlock> blocks;
        using (var measureDocument = new PdfDocument())
        {
            var measurePage = CreatePage(measureDocument, options.Landscape);
            using var measureGfx = XGraphics.FromPdfPage(measurePage);
            blocks = BuildRenderBlocks(measureGfx, model, fonts, dayColumnWidth, ct);
        }

        // 2-bosqich: sahifalarga bo'lish (sinf bloki imkon qadar bo'linmasin).
        var pages = Paginate(blocks, tableTop, tableBottomLimit);

        // 3-bosqich: chizish. Sahifalar soni allaqachon ma'lum — kolontitul to'g'ri chiqadi.
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            ct.ThrowIfCancellationRequested();

            var page = CreatePage(document, options.Landscape);
            using var gfx = XGraphics.FromPdfPage(page);

            DrawDocumentHeader(gfx, model, fonts, now, MarginLeft, MarginTop, contentWidth);

            var y = tableTop;
            DrawTableHeaderRow(gfx, model, fonts, y, dayColumnWidth);
            y += TableHeaderHeight;

            foreach (var segment in pages[pageIndex].Segments)
            {
                y = DrawSegment(gfx, segment, fonts, y, dayColumnWidth, model.Days.Count);
            }

            DrawColumnBorders(gfx, tableTop, y, dayColumnWidth, model.Days.Count);
            DrawFooter(gfx, fonts, pageIndex + 1, pages.Count, pageWidth, pageHeight);
        }

        return ToBytes(document);
    }

    private static void RenderEmptyDocument(
        PdfDocument document,
        TimetableDocumentModel model,
        PdfExportOptions options,
        DateTime now,
        FontSet fonts,
        double pageWidth,
        double pageHeight)
    {
        var page = CreatePage(document, options.Landscape);
        using var gfx = XGraphics.FromPdfPage(page);

        var contentWidth = pageWidth - MarginLeft - MarginRight;
        DrawDocumentHeader(gfx, model, fonts, now, MarginLeft, MarginTop, contentWidth);

        var box = new XRect(MarginLeft, MarginTop + HeaderBlockHeight + 40, contentWidth, 40);
        gfx.DrawString(TimetableDocumentModel.EmptyMessage, fonts.Subtitle, XBrushes.Black, box, XStringFormats.Center);

        DrawFooter(gfx, fonts, 1, 1, pageWidth, pageHeight);
    }

    private static byte[] ToBytes(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static PdfPage CreatePage(PdfDocument document, bool landscape)
    {
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        page.Orientation = landscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
        return page;
    }

    private static (double Width, double Height) MeasurePageSize(bool landscape)
    {
        // A4: 595.28 x 841.89 pt
        const double a4Short = 595.28;
        const double a4Long = 841.89;
        return landscape ? (a4Long, a4Short) : (a4Short, a4Long);
    }

    // ------------------------------------------------------------------
    // Sarlavha va kolontitul
    // ------------------------------------------------------------------

    private static void DrawDocumentHeader(
        XGraphics gfx,
        TimetableDocumentModel model,
        FontSet fonts,
        DateTime now,
        double left,
        double top,
        double width)
    {
        var y = top;

        if (model.SchoolName is not null)
        {
            gfx.DrawString(model.SchoolName, fonts.Title, XBrushes.Black,
                new XRect(left, y, width, 18), XStringFormats.TopLeft);
            y += 18;
            gfx.DrawString("Dars jadvali", fonts.Subtitle, XBrushes.Black,
                new XRect(left, y, width, 14), XStringFormats.TopLeft);
        }
        else
        {
            gfx.DrawString("Dars jadvali", fonts.Title, XBrushes.Black,
                new XRect(left, y, width, 18), XStringFormats.TopLeft);
        }

        var dateText = "Sana: " + now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        gfx.DrawString(dateText, fonts.Meta, XBrushes.Black,
            new XRect(left, top, width, 14), XStringFormats.TopRight);

        var lineY = top + HeaderBlockHeight - 8;
        gfx.DrawLine(new XPen(XColors.Black, 1.0), left, lineY, left + width, lineY);
    }

    private static void DrawFooter(
        XGraphics gfx,
        FontSet fonts,
        int pageNumber,
        int pageCount,
        double pageWidth,
        double pageHeight)
    {
        var width = pageWidth - MarginLeft - MarginRight;
        var y = pageHeight - MarginBottom - 12;

        gfx.DrawLine(new XPen(XColor.FromArgb(150, 150, 150), 0.5),
            MarginLeft, y - 3, MarginLeft + width, y - 3);

        gfx.DrawString(AppInfo.AppName, fonts.Footer, XBrushes.Gray,
            new XRect(MarginLeft, y, width, 12), XStringFormats.TopLeft);

        var pageText = $"{pageNumber.ToString(CultureInfo.InvariantCulture)} / {pageCount.ToString(CultureInfo.InvariantCulture)}";
        gfx.DrawString(pageText, fonts.Footer, XBrushes.Black,
            new XRect(MarginLeft, y, width, 12), XStringFormats.TopCenter);
    }

    // ------------------------------------------------------------------
    // Jadval sarlavhasi va kataklar
    // ------------------------------------------------------------------

    private static double ColumnX(int columnIndex, double dayColumnWidth)
    {
        // 0 — Sinf, 1 — Soat, 2.. — kunlar
        if (columnIndex == 0) return MarginLeft;
        if (columnIndex == 1) return MarginLeft + ClassColumnWidth;
        return MarginLeft + ClassColumnWidth + LessonColumnWidth + (columnIndex - 2) * dayColumnWidth;
    }

    private static double TableWidth(double dayColumnWidth, int dayCount) =>
        ClassColumnWidth + LessonColumnWidth + dayCount * dayColumnWidth;

    private static void DrawTableHeaderRow(
        XGraphics gfx,
        TimetableDocumentModel model,
        FontSet fonts,
        double y,
        double dayColumnWidth)
    {
        var dayCount = model.Days.Count;
        var totalWidth = TableWidth(dayColumnWidth, dayCount);
        var fill = new XSolidBrush(XColor.FromArgb(224, 228, 232));

        gfx.DrawRectangle(fill, MarginLeft, y, totalWidth, TableHeaderHeight);

        DrawCenteredText(gfx, "SINF", fonts.TableHeader,
            new XRect(ColumnX(0, dayColumnWidth), y, ClassColumnWidth, TableHeaderHeight));
        DrawCenteredText(gfx, "SOAT", fonts.TableHeader,
            new XRect(ColumnX(1, dayColumnWidth), y, LessonColumnWidth, TableHeaderHeight));

        for (var d = 0; d < dayCount; d++)
        {
            DrawCenteredText(gfx, model.DayNames[d], fonts.TableHeader,
                new XRect(ColumnX(d + 2, dayColumnWidth), y, dayColumnWidth, TableHeaderHeight));
        }

        var thick = new XPen(XColors.Black, 1.1);
        gfx.DrawLine(thick, MarginLeft, y, MarginLeft + totalWidth, y);
        gfx.DrawLine(thick, MarginLeft, y + TableHeaderHeight, MarginLeft + totalWidth, y + TableHeaderHeight);
    }

    private static void DrawCenteredText(XGraphics gfx, string text, XFont font, XRect rect)
    {
        if (string.IsNullOrEmpty(text))
            return;
        gfx.DrawString(text, font, XBrushes.Black, rect, XStringFormats.Center);
    }

    /// <summary>Bitta sinf blokining (yoki uning bir qismining) qatorlarini chizadi.</summary>
    private static double DrawSegment(
        XGraphics gfx,
        RenderSegment segment,
        FontSet fonts,
        double y,
        double dayColumnWidth,
        int dayCount)
    {
        var thin = new XPen(XColor.FromArgb(120, 120, 120), 0.4);
        var thick = new XPen(XColors.Black, 1.1);
        var segmentTop = y;

        for (var r = 0; r < segment.Rows.Count; r++)
        {
            var row = segment.Rows[r];

            // Soat ustuni: "3-soat" + vaqt oralig'i.
            var lessonRect = new XRect(ColumnX(1, dayColumnWidth), y, LessonColumnWidth, row.Height);
            if (row.TimeLabel is null)
            {
                DrawCenteredText(gfx, row.LessonLabel, fonts.Lesson, lessonRect);
            }
            else
            {
                var half = row.Height / 2;
                gfx.DrawString(row.LessonLabel, fonts.Lesson, XBrushes.Black,
                    new XRect(lessonRect.X, y, LessonColumnWidth, half), XStringFormats.Center);
                gfx.DrawString(row.TimeLabel, fonts.Time, XBrushes.Gray,
                    new XRect(lessonRect.X, y + half, LessonColumnWidth, half), XStringFormats.Center);
            }

            // Kun kataklari.
            for (var d = 0; d < dayCount; d++)
            {
                var lines = row.Cells[d];
                if (lines.Count == 0)
                    continue;   // bo'sh katak — faqat chiziq qoladi

                var totalTextHeight = lines.Sum(l => l.Font.GetHeight());
                var cellX = ColumnX(d + 2, dayColumnWidth);
                var textY = y + Math.Max(CellPadY, (row.Height - totalTextHeight) / 2);

                foreach (var line in lines)
                {
                    var lineHeight = line.Font.GetHeight();
                    gfx.DrawString(line.Text, line.Font, line.Brush,
                        new XRect(cellX + CellPadX, textY, dayColumnWidth - 2 * CellPadX, lineHeight),
                        XStringFormats.Center);
                    textY += lineHeight;
                }
            }

            // Qatorlar orasidagi ingichka chiziq (oxirgisidan keyin qalin chiziq chiziladi).
            if (r < segment.Rows.Count - 1)
            {
                var lineY = y + row.Height;
                gfx.DrawLine(thin, ColumnX(1, dayColumnWidth), lineY,
                    MarginLeft + TableWidth(dayColumnWidth, dayCount), lineY);
            }

            y += row.Height;
        }

        // Sinf nomi — blok qatorlari bo'ylab bir marta (vertikal birlashtirilgan katak).
        var classRect = new XRect(ColumnX(0, dayColumnWidth), segmentTop, ClassColumnWidth, y - segmentTop);
        var className = segment.IsContinuation ? segment.ClassName + " (dav.)" : segment.ClassName;
        DrawCenteredText(gfx, className, fonts.ClassName, classRect);

        // Sinf guruhlari orasidagi qalinroq chiziq.
        gfx.DrawLine(thick, MarginLeft, y, MarginLeft + TableWidth(dayColumnWidth, dayCount), y);

        return y;
    }

    private static void DrawColumnBorders(
        XGraphics gfx,
        double tableTop,
        double tableBottom,
        double dayColumnWidth,
        int dayCount)
    {
        var thin = new XPen(XColor.FromArgb(90, 90, 90), 0.6);
        var thick = new XPen(XColors.Black, 1.1);

        gfx.DrawLine(thick, ColumnX(0, dayColumnWidth), tableTop, ColumnX(0, dayColumnWidth), tableBottom);
        gfx.DrawLine(thick, ColumnX(1, dayColumnWidth), tableTop, ColumnX(1, dayColumnWidth), tableBottom);
        gfx.DrawLine(thick, ColumnX(2, dayColumnWidth), tableTop, ColumnX(2, dayColumnWidth), tableBottom);

        for (var d = 1; d <= dayCount; d++)
        {
            var x = ColumnX(d + 2, dayColumnWidth);
            var pen = d == dayCount ? thick : thin;
            gfx.DrawLine(pen, x, tableTop, x, tableBottom);
        }
    }

    // ------------------------------------------------------------------
    // O'lchash va sahifalash
    // ------------------------------------------------------------------

    private static List<RenderBlock> BuildRenderBlocks(
        XGraphics gfx,
        TimetableDocumentModel model,
        FontSet fonts,
        double dayColumnWidth,
        CancellationToken ct)
    {
        var textWidth = dayColumnWidth - 2 * CellPadX;
        var result = new List<RenderBlock>(model.Blocks.Count);

        foreach (var block in model.Blocks)
        {
            ct.ThrowIfCancellationRequested();

            var rows = new List<RenderRow>(block.Rows.Count);
            foreach (var row in block.Rows)
            {
                var cells = new List<CellLine>[model.Days.Count];
                var maxTextHeight = 0.0;

                for (var d = 0; d < model.Days.Count; d++)
                {
                    var cell = d < row.Cells.Count ? row.Cells[d] : null;
                    var lines = cell is null
                        ? new List<CellLine>()
                        : BuildCellLines(gfx, cell, fonts, textWidth);

                    cells[d] = lines;
                    maxTextHeight = Math.Max(maxTextHeight, lines.Sum(l => l.Font.GetHeight()));
                }

                var height = Math.Max(MinRowHeight, maxTextHeight + 2 * CellPadY);
                rows.Add(new RenderRow(row.LessonLabel, row.TimeLabel, cells, height));
            }

            result.Add(new RenderBlock(block.ClassName, rows));
        }

        return result;
    }

    private static List<CellLine> BuildCellLines(
        XGraphics gfx,
        TimetableCellModel cell,
        FontSet fonts,
        double maxWidth)
    {
        var lines = new List<CellLine>(4);

        foreach (var text in Wrap(gfx, cell.SubjectName, fonts.Subject, maxWidth, maxLines: 2))
            lines.Add(new CellLine(text, fonts.Subject, XBrushes.Black));

        if (!string.IsNullOrWhiteSpace(cell.TeacherName))
        {
            foreach (var text in Wrap(gfx, cell.TeacherName!, fonts.Teacher, maxWidth, maxLines: 2))
                lines.Add(new CellLine(text, fonts.Teacher, XBrushes.DimGray));
        }

        if (!string.IsNullOrWhiteSpace(cell.RoomNumber))
        {
            var roomText = "xona: " + cell.RoomNumber!.Trim();
            foreach (var text in Wrap(gfx, roomText, fonts.Room, maxWidth, maxLines: 1))
                lines.Add(new CellLine(text, fonts.Room, XBrushes.DimGray));
        }

        return lines;
    }

    /// <summary>Matnni ustun kengligiga qarab so'zlar bo'yicha bo'ladi.</summary>
    private static List<string> Wrap(XGraphics gfx, string text, XFont font, double maxWidth, int maxLines)
    {
        var result = new List<string>(maxLines);
        text = text.Trim();
        if (text.Length == 0)
            return result;

        if (maxWidth <= 1)
        {
            result.Add(text);
            return result;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                current.Clear().Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
                if (result.Count == maxLines)
                {
                    TruncateLast(gfx, result, font, maxWidth);
                    return result;
                }
            }

            // Bitta so'z ham sig'masa — belgilar bo'yicha bo'linadi.
            var rest = word;
            while (gfx.MeasureString(rest, font).Width > maxWidth && rest.Length > 1)
            {
                var take = rest.Length;
                while (take > 1 && gfx.MeasureString(rest[..take], font).Width > maxWidth)
                    take--;

                result.Add(rest[..take]);
                rest = rest[take..];
                if (result.Count == maxLines)
                {
                    TruncateLast(gfx, result, font, maxWidth);
                    return result;
                }
            }

            current.Append(rest);
        }

        if (current.Length > 0 && result.Count < maxLines)
            result.Add(current.ToString());

        return result;
    }

    /// <summary>Oxirgi qatorga "…" qo'yadi — matn kesilganini bildiradi.</summary>
    private static void TruncateLast(XGraphics gfx, List<string> lines, XFont font, double maxWidth)
    {
        if (lines.Count == 0)
            return;

        var last = lines[^1];
        var candidate = last + "…";
        while (candidate.Length > 1 && gfx.MeasureString(candidate, font).Width > maxWidth)
        {
            last = last[..^1];
            candidate = last + "…";
        }

        lines[^1] = candidate;
    }

    private static List<RenderPage> Paginate(List<RenderBlock> blocks, double tableTop, double bottomLimit)
    {
        var usableHeight = bottomLimit - (tableTop + TableHeaderHeight);
        if (usableHeight < MinRowHeight)
            usableHeight = MinRowHeight;

        var pages = new List<RenderPage>();
        var currentPage = new RenderPage();
        var y = 0.0;   // joriy sahifada jadval tanasida band bo'lgan balandlik

        foreach (var block in blocks)
        {
            var rowsLeft = (IReadOnlyList<RenderRow>)block.Rows;
            var isContinuation = false;

            while (rowsLeft.Count > 0)
            {
                var remaining = usableHeight - y;
                var blockHeight = rowsLeft.Sum(r => r.Height);

                // To'liq sig'adi.
                if (blockHeight <= remaining)
                {
                    currentPage.Segments.Add(new RenderSegment(block.ClassName, rowsLeft, isContinuation));
                    y += blockHeight;
                    break;
                }

                // Sig'masa-yu, bo'sh sahifaga to'liq sig'sa — keyingi sahifaga o'tkazamiz
                // (sinf guruhi imkon qadar bo'linmasin).
                if (blockHeight <= usableHeight && currentPage.Segments.Count > 0)
                {
                    pages.Add(currentPage);
                    currentPage = new RenderPage();
                    y = 0;
                    continue;
                }

                // Baribir bo'linishi kerak: sig'adigan qatorlarni olamiz (kamida bittasi).
                var taken = 0;
                var used = 0.0;
                while (taken < rowsLeft.Count && used + rowsLeft[taken].Height <= remaining)
                {
                    used += rowsLeft[taken].Height;
                    taken++;
                }

                if (taken == 0)
                {
                    if (currentPage.Segments.Count > 0)
                    {
                        pages.Add(currentPage);
                        currentPage = new RenderPage();
                        y = 0;
                        continue;
                    }

                    taken = 1;   // bitta qator sahifadan baland — baribir chizamiz
                    used = rowsLeft[0].Height;
                }

                currentPage.Segments.Add(new RenderSegment(
                    block.ClassName,
                    rowsLeft.Take(taken).ToList(),
                    isContinuation));

                rowsLeft = rowsLeft.Skip(taken).ToList();
                isContinuation = true;

                pages.Add(currentPage);
                currentPage = new RenderPage();
                y = 0;
            }
        }

        if (currentPage.Segments.Count > 0 || pages.Count == 0)
            pages.Add(currentPage);

        return pages;
    }

    // ------------------------------------------------------------------
    // Yordamchi turlar
    // ------------------------------------------------------------------

    private sealed class FontSet
    {
        private const string Family = EmbeddedFontResolver.FamilyName;

        public XFont Title { get; } = new(Family, 13, XFontStyleEx.Bold);
        public XFont Subtitle { get; } = new(Family, 10.5, XFontStyleEx.Regular);
        public XFont Meta { get; } = new(Family, 8.5, XFontStyleEx.Regular);
        public XFont TableHeader { get; } = new(Family, 8.5, XFontStyleEx.Bold);
        public XFont ClassName { get; } = new(Family, 10, XFontStyleEx.Bold);
        public XFont Lesson { get; } = new(Family, 7.5, XFontStyleEx.Regular);
        public XFont Time { get; } = new(Family, 6.5, XFontStyleEx.Regular);
        public XFont Subject { get; } = new(Family, 7.5, XFontStyleEx.Bold);
        public XFont Teacher { get; } = new(Family, 6.5, XFontStyleEx.Regular);
        public XFont Room { get; } = new(Family, 6, XFontStyleEx.Regular);
        public XFont Footer { get; } = new(Family, 7, XFontStyleEx.Regular);
    }

    private sealed record CellLine(string Text, XFont Font, XBrush Brush);

    private sealed record RenderRow(
        string LessonLabel,
        string? TimeLabel,
        IReadOnlyList<List<CellLine>> Cells,
        double Height);

    private sealed record RenderBlock(string ClassName, List<RenderRow> Rows);

    private sealed record RenderSegment(string ClassName, IReadOnlyList<RenderRow> Rows, bool IsContinuation);

    private sealed class RenderPage
    {
        public List<RenderSegment> Segments { get; } = new();
    }
}
