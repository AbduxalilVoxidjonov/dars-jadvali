using System.Globalization;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>Dizaynga asoslangan eksport uchun qo'shimcha sozlamalar.</summary>
public sealed record DesignExportOptions
{
    /// <summary>O'quv yili, masalan "2025/2026".</summary>
    public string? AcademicYear { get; init; }

    /// <summary>Chorak/semestr.</summary>
    public string? Term { get; init; }

    /// <summary>1-smenadagi darslar soni. 0 — bitta smena.</summary>
    public int FirstShiftPeriodCount { get; init; }

    /// <summary>Sinf jadvali dizayni kaliti.</summary>
    public string ClassDesignKey { get; init; } = BuiltInPrintDesigns.ClassBlue;

    /// <summary>O'qituvchi jadvali dizayni kaliti.</summary>
    public string TeacherDesignKey { get; init; } = BuiltInPrintDesigns.TeacherGreen;

    /// <summary>Maktab jadvali dizayni kaliti.</summary>
    public string SchoolDesignKey { get; init; } = BuiltInPrintDesigns.SchoolCompact;
}

/// <summary>
/// Dizayn shabloniga asoslangan PDF eksport — <see cref="IScopedTimetablePdfExporter"/>
/// kontraktini buzmasdan, eski <see cref="SchoolTimetablePdfExporter"/> ga YONMA-YON turadi.
/// </summary>
/// <remarks>
/// <para>
/// Eski eksportchi hech qanday o'zgarishsiz qoldi va DI hozircha o'shani beradi.
/// Bu sinfni DI ga ulash — bitta qatorlik o'zgarish (<c>IScopedTimetablePdfExporter</c>
/// registratsiyasini almashtirish), Desktop kodiga tegmasdan.
/// </para>
/// <para>
/// <b>Ikki ma'lumot manbasi.</b> Eksportchining ikkita konstruktori bor:
/// <list type="number">
///   <item>eski <c>ScheduleEntry</c> yo'li (<see cref="IScheduleService"/> va h.k.) —
///     juft dars, A/B hafta va guruh bo'linmasi YO'Q, chunki eski modelda ular yo'q;</item>
///   <item>yangi <c>Card</c>/<c>Lesson</c> yo'li (<see cref="ICardBoardService"/> +
///     <see cref="ISchedulingStore"/>) — <c>Length</c>, <c>WeeksMask</c>, <c>GroupName</c>
///     va xona HAQIQIY manbadan olinadi.</item>
/// </list>
/// Chizish kodi ikkalasida ham bir xil: farq faqat adapterda
/// (<see cref="CardPrintableAdapter"/> ↔ <see cref="ScheduleEntryPrintableAdapter"/>).
/// </para>
/// </remarks>
public sealed class DesignBasedTimetablePdfExporter : IScopedTimetablePdfExporter
{
    // --- Eski (ScheduleEntry) yo'l ---
    private readonly IScheduleService? _schedules;
    private readonly IWorkDayService? _workDays;
    private readonly ITeacherService? _teachers;
    private readonly IClassGroupService? _classGroups;

    // --- Yangi (Card/Lesson) yo'l ---
    private readonly ICardBoardService? _cardBoard;
    private readonly ISchedulingStore? _store;
    private readonly IUnitOfWork? _uow;

    private readonly DesignExportOptions _designOptions;
    private readonly PrintDesignPdfRenderer _renderer = new();
    private readonly TimetableHtmlExporter _html = new();

    /// <summary>Eski <c>ScheduleEntry</c> modelidan chiqaruvchi eksportchi.</summary>
    /// <param name="schedules">Jadval yozuvlari servisi.</param>
    /// <param name="workDays">Ish kunlari va dars soatlari servisi.</param>
    /// <param name="teachers">O'qituvchilar servisi.</param>
    /// <param name="classGroups">Sinflar servisi.</param>
    /// <param name="designOptions">Dizayn sozlamalari (<c>null</c> — standart).</param>
    public DesignBasedTimetablePdfExporter(
        IScheduleService schedules,
        IWorkDayService workDays,
        ITeacherService teachers,
        IClassGroupService classGroups,
        DesignExportOptions? designOptions = null)
    {
        _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        _workDays = workDays ?? throw new ArgumentNullException(nameof(workDays));
        _teachers = teachers ?? throw new ArgumentNullException(nameof(teachers));
        _classGroups = classGroups ?? throw new ArgumentNullException(nameof(classGroups));
        _designOptions = designOptions ?? new DesignExportOptions();
    }

    /// <summary>
    /// Yangi <c>Card</c>/<c>Lesson</c> modelidan chiqaruvchi eksportchi.
    /// </summary>
    /// <remarks>
    /// Qamrov Id'lari ham v2 dan: <c>classId</c> — <c>SchoolClass.Id</c>,
    /// <c>teacherId</c> — <c>Teacher.Id</c>.
    /// </remarks>
    /// <param name="cardBoard">Kartochkalar servisi.</param>
    /// <param name="store">Jadval kirish ma'lumoti (kunlar, soatlar, smenalar, sinflar).</param>
    /// <param name="uow">Faol jadvalni aniqlash uchun.</param>
    /// <param name="designOptions">Dizayn sozlamalari (<c>null</c> — standart).</param>
    public DesignBasedTimetablePdfExporter(
        ICardBoardService cardBoard,
        ISchedulingStore store,
        IUnitOfWork uow,
        DesignExportOptions? designOptions = null)
    {
        _cardBoard = cardBoard ?? throw new ArgumentNullException(nameof(cardBoard));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _designOptions = designOptions ?? new DesignExportOptions();
    }

    /// <summary>Eksport yangi <c>Card</c> modelidan o'qiydimi.</summary>
    public bool UsesCardModel => _cardBoard is not null;

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

        var timetable = await BuildClassTimetableAsync(classGroupId, options, ct).ConfigureAwait(false);
        var design = PrepareDesign(_designOptions.ClassDesignKey, options);

        return new TimetablePdfDocument(
            _renderer.Render(design, timetable, ct),
            FileName($"{timetable.ScopeName}-sinf-jadvali", timetable.GeneratedAt, "pdf"));
    }

    /// <inheritdoc />
    public async Task<TimetablePdfDocument> ExportTeacherScheduleAsync(
        int teacherId, PdfExportOptions? options = null, CancellationToken ct = default)
    {
        if (teacherId <= 0)
            throw new ArgumentException("O'qituvchi tanlanmagan.", nameof(teacherId));

        var timetable = await BuildTeacherTimetableAsync(teacherId, options, ct).ConfigureAwait(false);
        var design = PrepareDesign(_designOptions.TeacherDesignKey, options);

        return new TimetablePdfDocument(
            _renderer.Render(design, timetable, ct),
            FileName($"{timetable.ScopeName}-jadvali", timetable.GeneratedAt, "pdf"));
    }

    /// <inheritdoc />
    public async Task<TimetablePdfDocument> ExportSchoolScheduleAsync(
        PdfExportOptions? options = null, CancellationToken ct = default)
    {
        var timetable = await BuildSchoolTimetableAsync(options, ct).ConfigureAwait(false);

        // Maktab dizayni o'z qog'ozini (A3) o'zi belgilaydi — Landscape bayrog'i bu yerda
        // ataylab qo'llanmaydi, aks holda 15 sinfli varaq bo'yiga siqilib qoladi.
        var design = BuiltInPrintDesigns.Get(_designOptions.SchoolDesignKey);

        return new TimetablePdfDocument(
            _renderer.Render(design, timetable, ct),
            FileName("Maktab-jadvali", timetable.GeneratedAt, "pdf"));
    }

    /// <summary>Sinf jadvalini mustaqil (offline) HTML fayl sifatida chiqaradi.</summary>
    /// <param name="classGroupId">Sinf Id.</param>
    /// <param name="options">Sozlamalar.</param>
    /// <param name="ct">Bekor qilish belgisi.</param>
    public async Task<TimetablePdfDocument> ExportClassScheduleHtmlAsync(
        int classGroupId, PdfExportOptions? options = null, CancellationToken ct = default)
    {
        if (classGroupId <= 0)
            throw new ArgumentException("Sinf tanlanmagan.", nameof(classGroupId));

        var timetable = await BuildClassTimetableAsync(classGroupId, options, ct).ConfigureAwait(false);

        return new TimetablePdfDocument(
            _html.ExportBytes(timetable, HtmlOptions(timetable.Scope, options)),
            FileName($"{timetable.ScopeName}-sinf-jadvali", timetable.GeneratedAt, "html"));
    }

    /// <summary>O'qituvchi jadvalini mustaqil (offline) HTML fayl sifatida chiqaradi.</summary>
    /// <param name="teacherId">O'qituvchi Id.</param>
    /// <param name="options">Sozlamalar.</param>
    /// <param name="ct">Bekor qilish belgisi.</param>
    public async Task<TimetablePdfDocument> ExportTeacherScheduleHtmlAsync(
        int teacherId, PdfExportOptions? options = null, CancellationToken ct = default)
    {
        if (teacherId <= 0)
            throw new ArgumentException("O'qituvchi tanlanmagan.", nameof(teacherId));

        var timetable = await BuildTeacherTimetableAsync(teacherId, options, ct).ConfigureAwait(false);

        return new TimetablePdfDocument(
            _html.ExportBytes(timetable, HtmlOptions(timetable.Scope, options)),
            FileName($"{timetable.ScopeName}-jadvali", timetable.GeneratedAt, "html"));
    }

    /// <summary>Butun maktab jadvalini bitta offline HTML faylga chiqaradi.</summary>
    /// <param name="options">Sozlamalar.</param>
    /// <param name="ct">Bekor qilish belgisi.</param>
    public async Task<TimetablePdfDocument> ExportSchoolScheduleHtmlAsync(
        PdfExportOptions? options = null, CancellationToken ct = default)
    {
        var timetable = await BuildSchoolTimetableAsync(options, ct).ConfigureAwait(false);

        return new TimetablePdfDocument(
            _html.ExportBytes(timetable, HtmlOptions(timetable.Scope, options)),
            FileName("Maktab-jadvali", timetable.GeneratedAt, "html"));
    }

    // ==================================================================
    // Model qurish
    // ==================================================================

    private Task<PrintableTimetable> BuildClassTimetableAsync(
        int classGroupId, PdfExportOptions? options, CancellationToken ct) =>
        _cardBoard is null
            ? BuildClassFromEntriesAsync(classGroupId, options, ct)
            : BuildClassFromCardsAsync(classGroupId, options, ct);

    private Task<PrintableTimetable> BuildTeacherTimetableAsync(
        int teacherId, PdfExportOptions? options, CancellationToken ct) =>
        _cardBoard is null
            ? BuildTeacherFromEntriesAsync(teacherId, options, ct)
            : BuildTeacherFromCardsAsync(teacherId, options, ct);

    private Task<PrintableTimetable> BuildSchoolTimetableAsync(
        PdfExportOptions? options, CancellationToken ct) =>
        _cardBoard is null
            ? BuildSchoolFromEntriesAsync(options, ct)
            : BuildSchoolFromCardsAsync(options, ct);

    // ------------------------------------------------------------------
    // Yangi yo'l: Card / Lesson
    // ------------------------------------------------------------------

    /// <summary>Bitta jadval varianti uchun kerak bo'ladigan hamma narsa BIR MARTA o'qiladi.</summary>
    private sealed record CardWorld(
        SchedulingInput Input,
        IReadOnlyList<CardView> Cards,
        CardPrintAxes Axes,
        CardPrintNames Names);

    private async Task<CardWorld> LoadCardWorldAsync(PdfExportOptions? options, CancellationToken ct)
    {
        var scheduleId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow!, options?.ScheduleId, ct).ConfigureAwait(false);

        var input = await _store!.LoadAsync(scheduleId, ct).ConfigureAwait(false);
        var cards = await _cardBoard!.GetCardsAsync(scheduleId, ct).ConfigureAwait(false);

        var weeksInCycle = Math.Max(1, input.Schedule.WeeksInCycle);

        var axes = CardPrintableAdapter.ToAxes(
            input.WorkDays, input.Periods, input.Shifts, weeksInCycle);

        var names = new CardPrintNames(
            input.Subjects.ToDictionary(s => s.Id, s => s.ShortName),
            input.Subjects.ToDictionary(s => s.Id, s => (string?)s.ColorCode),
            input.Teachers.ToDictionary(t => t.Id, t => (string?)t.ColorCode));

        return new CardWorld(input, cards, axes, names);
    }

    private async Task<PrintableTimetable> BuildClassFromCardsAsync(
        int classId, PdfExportOptions? options, CancellationToken ct)
    {
        var world = await LoadCardWorldAsync(options, ct).ConfigureAwait(false);

        var schoolClass = world.Input.Classes.FirstOrDefault(c => c.Id == classId)
            ?? throw new ArgumentException($"Sinf topilmadi (Id={classId}).", nameof(classId));

        return CardPrintableAdapter.BuildClass(
            schoolClass.Name,
            ClassSubCaption(world.Input, schoolClass),
            world.Cards.Where(c => c.SchoolClassIds.Contains(classId)),
            world.Axes,
            Context(options),
            world.Names);
    }

    private async Task<PrintableTimetable> BuildTeacherFromCardsAsync(
        int teacherId, PdfExportOptions? options, CancellationToken ct)
    {
        var world = await LoadCardWorldAsync(options, ct).ConfigureAwait(false);

        var teacher = world.Input.Teachers.FirstOrDefault(t => t.Id == teacherId)
            ?? throw new ArgumentException($"O'qituvchi topilmadi (Id={teacherId}).", nameof(teacherId));

        return CardPrintableAdapter.BuildTeacher(
            teacher.FullName,
            world.Cards.Where(c => c.TeacherIds.Contains(teacherId)),
            world.Axes,
            Context(options),
            world.Names);
    }

    private async Task<PrintableTimetable> BuildSchoolFromCardsAsync(
        PdfExportOptions? options, CancellationToken ct)
    {
        var world = await LoadCardWorldAsync(options, ct).ConfigureAwait(false);

        var classes = world.Input.Classes
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CardPrintClass(c.Id, c.Name, ClassSubCaption(world.Input, c)))
            .ToList();

        return CardPrintableAdapter.BuildSchool(
            classes, world.Cards, world.Axes, Context(options), world.Names);
    }

    /// <summary>Sinf to'ri ostidagi qator: asosiy xona, bo'lmasa smena nomi.</summary>
    private static string? ClassSubCaption(SchedulingInput input, Domain.Entities.SchoolClass schoolClass)
    {
        if (schoolClass.HomeClassroomId is int roomId)
        {
            var room = input.Classrooms.FirstOrDefault(r => r.Id == roomId);
            if (room is not null && !string.IsNullOrWhiteSpace(room.Name))
                return $"Xona: {room.Name.Trim()}";
        }

        if (schoolClass.ShiftId is int shiftId)
        {
            var shift = input.Shifts.FirstOrDefault(s => s.Id == shiftId);
            if (shift is not null && !string.IsNullOrWhiteSpace(shift.Name))
                return shift.Name.Trim();
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Eski yo'l: ScheduleEntry (Desktop hali shu yerda)
    // ------------------------------------------------------------------

    private async Task<PrintableTimetable> BuildClassFromEntriesAsync(
        int classGroupId, PdfExportOptions? options, CancellationToken ct)
    {
        var classGroup = await _classGroups!.GetByIdAsync(classGroupId, ct).ConfigureAwait(false)
            ?? throw new ArgumentException($"Sinf topilmadi (Id={classGroupId}).", nameof(classGroupId));

        var (dayOrder, periods) = await LoadAxesAsync(ct).ConfigureAwait(false);
        var entries = await _schedules!
            .GetByClassGroupAsync(classGroupId, options?.ScheduleId, ct)
            .ConfigureAwait(false);

        return ScheduleEntryPrintableAdapter.BuildClass(
            classGroup, entries, dayOrder, periods, Context(options));
    }

    private async Task<PrintableTimetable> BuildTeacherFromEntriesAsync(
        int teacherId, PdfExportOptions? options, CancellationToken ct)
    {
        var teacher = await _teachers!.GetByIdAsync(teacherId, ct).ConfigureAwait(false)
            ?? throw new ArgumentException($"O'qituvchi topilmadi (Id={teacherId}).", nameof(teacherId));

        var (dayOrder, periods) = await LoadAxesAsync(ct).ConfigureAwait(false);
        var entries = await _schedules!.GetByTeacherAsync(teacherId, options?.ScheduleId, ct).ConfigureAwait(false);
        var classGroups = await _classGroups!.GetAllAsync(ct).ConfigureAwait(false);

        return ScheduleEntryPrintableAdapter.BuildTeacher(
            teacher, entries, dayOrder, periods, classGroups, Context(options));
    }

    private async Task<PrintableTimetable> BuildSchoolFromEntriesAsync(
        PdfExportOptions? options, CancellationToken ct)
    {
        var (dayOrder, periods) = await LoadAxesAsync(ct).ConfigureAwait(false);
        var entries = await _schedules!.GetAllAsync(options?.ScheduleId, ct).ConfigureAwait(false);
        var classGroups = await _classGroups!.GetAllAsync(ct).ConfigureAwait(false);

        return ScheduleEntryPrintableAdapter.BuildSchool(
            classGroups, entries, dayOrder, periods, Context(options));
    }

    private async Task<(IReadOnlyList<WeekDay> Days, IReadOnlyList<PrintablePeriod> Periods)> LoadAxesAsync(
        CancellationToken ct)
    {
        var activeDays = await _workDays!.GetActiveAsync(ct).ConfigureAwait(false);
        var slots = await _workDays.GetLessonSlotsAsync(ct).ConfigureAwait(false);
        var maxLesson = await _workDays.GetMaxLessonNumberAsync(ct).ConfigureAwait(false);

        var days = activeDays
            .Select(d => d.DayOfWeek)
            .Distinct()
            .OrderBy(d => (int)d)
            .ToList();

        if (maxLesson <= 0 && slots.Count > 0)
            maxLesson = slots.Max(s => s.LessonNumber);

        var periods = ScheduleEntryPrintableAdapter.ToPeriods(
            slots, maxLesson, _designOptions.FirstShiftPeriodCount);

        return (days, periods);
    }

    private PrintableContext Context(PdfExportOptions? options) => new(
        string.IsNullOrWhiteSpace(options?.SchoolName) ? null : options!.SchoolName!.Trim(),
        _designOptions.AcademicYear,
        _designOptions.Term,
        _designOptions.FirstShiftPeriodCount);

    // ==================================================================
    // Dizayn
    // ==================================================================

    /// <summary>
    /// Tayyor dizaynni oladi va <see cref="PdfExportOptions"/> dagi umumiy
    /// bayroqlarni (yo'nalish, xona, o'qituvchi) unga qo'llaydi.
    /// </summary>
    private static PrintDesign PrepareDesign(string key, PdfExportOptions? options)
    {
        var design = BuiltInPrintDesigns.Get(key);
        if (options is null)
            return design;

        var orientation = options.Landscape ? PrintOrientation.Landscape : PrintOrientation.Portrait;
        var page = design.Page.Orientation == orientation
            ? design.Page
            : design.Page with { Orientation = orientation };

        var elements = design.Elements
            .Select(e => e is PrintTimetableElement grid
                ? grid with
                {
                    ShowRoom = grid.ShowRoom && options.IncludeRoom,
                    ShowTeacher = grid.ShowTeacher && options.IncludeTeacherName,
                }
                : e)
            .ToList();

        return design with { Page = page, Elements = elements };
    }

    private static HtmlExportOptions HtmlOptions(PrintScope scope, PdfExportOptions? options)
    {
        var result = HtmlExportOptions.ForScope(scope);
        if (options is null)
            return result;

        return result with
        {
            ShowRoom = result.ShowRoom && options.IncludeRoom,
            ShowTeacher = result.ShowTeacher && options.IncludeTeacherName,
        };
    }

    private static string FileName(string label, DateTime now, string extension)
    {
        var cleaned = new string((label ?? string.Empty)
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c)
            .ToArray())
            .Trim('-');

        if (cleaned.Length == 0)
            cleaned = "Jadval";

        return $"{cleaned}-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.{extension}";
    }
}
