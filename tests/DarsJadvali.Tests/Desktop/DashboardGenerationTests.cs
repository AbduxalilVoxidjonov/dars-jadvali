using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Desktop.ViewModels;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Export;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using DarsJadvali.Scheduling.Pipeline;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Bosh sahifadagi <b>generatsiya ekrani</b> yangi <see cref="IScheduleGenerationService"/> ga
/// ulanganini va hisobot maydonlari (faza, foiz, joylashtirilmagan darslar, hard buzilishlar,
/// tekshiruv xatolari, yumshatish tavsiyalari, jarima taqsimoti) UI ga chiqishini tekshiradi.
/// </summary>
public sealed class DashboardGenerationTests
{
    private sealed class SilentDialogService : IDialogService
    {
        public List<string> Errors { get; } = new();

        public Task InfoAsync(string message, string title = "Ma'lumot") => Task.CompletedTask;

        public Task ErrorAsync(string message, string title = "Xato")
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string message, string title = "Tasdiqlang") => Task.FromResult(true);

        public Task ShowValidationAsync(ValidationResult result) => Task.CompletedTask;

        public Task<bool> ConfirmWarningsAsync(ValidationResult result) => Task.FromResult(true);

        public Task CopyToClipboardAsync(string text) => Task.CompletedTask;

        public Task<string?> SaveFileAsync(
            string suggestedFileName, string filterName = "PDF hujjat", string extension = "pdf")
            => Task.FromResult<string?>(null);
    }

    private sealed class StubPdfExporter : IScopedTimetablePdfExporter
    {
        public Task<TimetablePdfDocument> ExportClassScheduleAsync(
            int classGroupId, PdfExportOptions? options = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<TimetablePdfDocument> ExportTeacherScheduleAsync(
            int teacherId, PdfExportOptions? options = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<TimetablePdfDocument> ExportSchoolScheduleAsync(
            PdfExportOptions? options = null, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static async Task<(TestDbFactory Db, DashboardViewModel Vm, SilentDialogService Dialogs)>
        BuildAsync()
    {
        var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 6);

        var t1 = db.AddTeacher("Voxidjonov Abduxalil");
        var t2 = db.AddTeacher("Karimova Nodira");
        var math = db.AddSubject("Matematika");
        var physics = db.AddSubject("Fizika");
        var a = db.AddClassGroup("5-A", room: "201");
        var b = db.AddClassGroup("5-B", room: "202");

        db.AddAssignment(t1, math, a, weeklyHours: 3);
        db.AddAssignment(t2, physics, a, weeklyHours: 2);
        db.AddAssignment(t1, math, b, weeklyHours: 3);
        db.AddAssignment(t2, physics, b, weeklyHours: 2);

        db.EnsureActiveSchedule();
        db.AddEntry(a, math, t1, WeekDay.Dushanba, 1, room: "201");
        db.AddEntry(b, physics, t2, WeekDay.Dushanba, 2, room: "202");

        await new LegacyToV2Backfill(db.Context, db.Get<ICardOccurrenceProjector>()).RunAsync();

        var dialogs = new SilentDialogService();
        var rewriter = new BoardCardRewriter(
            db.Get<IUnitOfWork>(), db.Get<ISchedulingStore>(), db.Get<ICardOccurrenceProjector>());

        var board = new TimetableBoardViewModel(
            db.Get<ICardBoardService>(),
            db.Get<IScheduleSnapshotProvider>(),
            db.Get<ISchedulingStore>(),
            db.Get<IScheduleSetService>(),
            db.Get<ITeacherService>(),
            db.Get<ISubjectService>(),
            db.Get<IAvailabilityService>(),
            dialogs);

        var vm = new DashboardViewModel(
            db.Get<ITeacherService>(),
            db.Get<ISubjectService>(),
            db.Get<IClassGroupService>(),
            db.Get<IAssignmentService>(),
            db.Get<IScheduleSetService>(),
            db.Get<ICardBoardService>(),
            db.Get<IScheduleGenerationService>(),
            new PlanCapacityService(
                db.Get<IUnitOfWork>(), db.Get<ISchedulingStore>(), db.Get<ISchedulingMapper>()),
            rewriter,
            new StubPdfExporter(),
            dialogs,
            board);

        return (db, vm, dialogs);
    }

    [Fact]
    public async Task Sozlamalar_foydalanuvchiga_ochiq()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        // Takrorlanuvchi natija uchun urug' va qidiruv byudjeti sozlanadi.
        Assert.Equal(12345, vm.Seed);
        Assert.Equal(4, vm.Complexities.Count);
        Assert.Equal(Complexity.Normal, vm.SelectedComplexity!.Value);
        Assert.True(vm.KeepLocked);
        Assert.True(vm.SavePartial);

        // Generator nomi yangi servisdan keladi.
        Assert.Contains("Algoritm:", vm.GeneratorName, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(vm.GeneratorDescription));
    }

    [Fact]
    public async Task Generatsiya_ishlaydi_va_hisobot_UI_ga_chiqadi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        await vm.LoadAsync();
        Assert.Empty(dialogs.Errors);

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.True(vm.HasGenerationReport);
        Assert.False(vm.IsGenerating);

        // "Joylashgan / jami" matni.
        Assert.Contains("Joylashgan:", vm.GenerationSummary, StringComparison.Ordinal);
        Assert.Contains("Yumshoq jarima:", vm.SoftCostText, StringComparison.Ordinal);

        // Faza nomi o'zbekcha va foiz ko'rsatiladi.
        Assert.False(string.IsNullOrWhiteSpace(vm.GenerationPhase));
        Assert.EndsWith("%", vm.GenerationPercentText, StringComparison.Ordinal);

        // Jadval bazaga yozildi.
        var cards = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync();
        Assert.NotEmpty(cards);
        Assert.Equal(cards.Sum(c => Math.Max(1, c.Length)), vm.PlacedLessonCount);
    }

    [Fact]
    public async Task Takrorlanuvchi_natija_urugga_boglangan()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        await vm.LoadAsync();

        vm.Seed = 2024;
        await vm.GenerateCommand.ExecuteAsync(null);
        var first = (await db.GetFromNewScope<ICardBoardService>().GetCardsAsync())
            .OrderBy(c => c.LessonId).ThenBy(c => c.DayNo).ThenBy(c => c.PeriodNo)
            .Select(c => $"{c.LessonId}:{c.DayNo}:{c.PeriodNo}")
            .ToList();

        await vm.GenerateCommand.ExecuteAsync(null);
        var second = (await db.GetFromNewScope<ICardBoardService>().GetCardsAsync())
            .OrderBy(c => c.LessonId).ThenBy(c => c.DayNo).ThenBy(c => c.PeriodNo)
            .Select(c => $"{c.LessonId}:{c.DayNo}:{c.PeriodNo}")
            .ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Joylashtirilmagan_darslar_hisobotda_aniq_royxat_bolib_chiqadi()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        await vm.LoadAsync();
        await vm.GenerateCommand.ExecuteAsync(null);

        // Ro'yxat servisdan keladi — "me'yor − qo'yilgan" taxmini emas.
        var unplaced = await db.GetFromNewScope<ICardBoardService>().GetUnplacedAsync();
        Assert.Equal(unplaced.Count(u => u.RemainingPeriods > 0), vm.UnplacedLessons.Count);
        Assert.Equal(vm.UnplacedLessons.Count > 0, vm.HasUnplacedLessons);
    }

    [Fact]
    public async Task Tekshiruv_yangi_servis_orqali_ketadi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        await vm.LoadAsync();
        await vm.ValidateAllCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.True(vm.HasValidationResult);
        Assert.False(string.IsNullOrWhiteSpace(vm.ValidationSummary));
    }

    [Fact]
    public async Task Sigim_tekshiruvi_alohida_amal_sifatida_ishlaydi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        await vm.LoadAsync();
        await vm.CheckCapacityCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.True(vm.HasCapacityResult);
        Assert.False(string.IsNullOrWhiteSpace(vm.CapacitySummary));

        // Bu ma'lumot bazasida reja sig'imga mos — ogohlantirish yo'q.
        Assert.False(vm.HasCapacityWarnings);
    }

    [Fact]
    public async Task Sigimdan_oshgan_reja_generatsiyadan_oldin_ogohlantiradi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        // Rejani ataylab sig'imdan oshiramiz (6 soat × 5 kun = 30 slot).
        var lesson = db.Context.Lessons.OrderBy(l => l.Id).First();
        lesson.PeriodsPerWeek = 60;
        await db.Context.SaveChangesAsync();

        await vm.LoadAsync();
        await vm.CheckCapacityCommand.ExecuteAsync(null);

        Assert.True(vm.HasCapacityWarnings);

        var warning = vm.CapacityWarnings.First();
        Assert.Contains("soat rejalashtirilgan", warning.Message, StringComparison.Ordinal);
        Assert.Contains("sig'maydi", warning.Message, StringComparison.Ordinal);

        // Yadroning Verify fazasi xatolari ham AYNI ekranga chiqadi.
        Assert.True(vm.HasVerificationFaults);
        Assert.Contains(vm.VerificationFaults, f => f.Contains("OVERLOADED", StringComparison.Ordinal));

        // Generatsiya natijasida ham ogohlantirish ekranda qoladi.
        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.True(vm.HasCapacityWarnings);
    }

    [Fact]
    public async Task Jadvalni_tozalash_barcha_kartochkalarni_ochiradi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        await vm.LoadAsync();
        await vm.GenerateCommand.ExecuteAsync(null);
        Assert.NotEmpty(await db.GetFromNewScope<ICardBoardService>().GetCardsAsync());

        await vm.ClearScheduleCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.Empty(await db.GetFromNewScope<ICardBoardService>().GetCardsAsync());
        Assert.Equal(0, vm.PlacedLessonCount);
    }
}
