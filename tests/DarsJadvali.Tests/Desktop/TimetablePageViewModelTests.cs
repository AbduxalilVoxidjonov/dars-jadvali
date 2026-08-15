using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Desktop.ViewModels;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Export;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// «Dars jadvali» sahifasi ham yangi <c>Card</c>/<c>Lesson</c> modelida ishlashini
/// tekshiradi: ilgari u eski <c>ScheduleEntry</c> ga yozar va bosh sahifadagi
/// jadval yadrosi bilan <b>ikki xil</b> jadvalni ko'rsatishi mumkin edi.
/// </summary>
public sealed class TimetablePageViewModelTests
{
    private sealed class SilentDialogService : IDialogService
    {
        public List<string> Errors { get; } = new();

        public bool ConfirmResult { get; set; } = true;

        public Task InfoAsync(string message, string title = "Ma'lumot") => Task.CompletedTask;

        public Task ErrorAsync(string message, string title = "Xato")
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string message, string title = "Tasdiqlang")
            => Task.FromResult(ConfirmResult);

        public Task ShowValidationAsync(ValidationResult result) => Task.CompletedTask;

        public Task<bool> ConfirmWarningsAsync(ValidationResult result) => Task.FromResult(true);

        public Task CopyToClipboardAsync(string text) => Task.CompletedTask;

        public Task<string?> SaveFileAsync(
            string suggestedFileName, string filterName = "PDF hujjat", string extension = "pdf")
            => Task.FromResult<string?>(null);

        public Task<string?> OpenFileAsync(
            string title = "Faylni tanlang", string filterName = "XML fayl", string extension = "xml")
            => Task.FromResult<string?>(null);
    }

    private sealed class StubNavigationService : INavigationService
    {
        public ViewModelBase? Current => null;

        public event EventHandler<ViewModelBase>? Navigated;

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
            => Navigated?.Invoke(this, null!);

        public ViewModelBase NavigateToType(Type viewModelType)
            => throw new NotSupportedException();
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

    private static async Task<(TestDbFactory Db, TimetableViewModel Vm, SilentDialogService Dialogs)>
        BuildAsync()
    {
        var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 6);

        var teacher = db.AddTeacher("Voxidjonov Abduxalil");
        var math = db.AddSubject("Matematika");
        var a = db.AddClassGroup("5-A", room: "201");

        db.AddAssignment(teacher, math, a, weeklyHours: 3);
        db.EnsureActiveSchedule();
        db.AddEntry(a, math, teacher, WeekDay.Dushanba, 1, room: "201");

        await new LegacyToV2Backfill(db.Context, db.Get<ICardOccurrenceProjector>()).RunAsync();

        var dialogs = new SilentDialogService();

        var vm = new TimetableViewModel(
            db.Get<ICardBoardService>(),
            db.Get<IScheduleSnapshotProvider>(),
            db.Get<ISchedulingStore>(),
            db.Get<IScheduleSetService>(),
            db.Get<ITeacherService>(),
            db.Get<ISubjectService>(),
            db.Get<IAvailabilityService>(),
            new StubPdfExporter(),
            dialogs,
            new MainViewModel(
                new StubNavigationService(),
                dialogs,
                db.Get<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>()));

        await vm.LoadAsync();

        return (db, vm, dialogs);
    }

    [Fact]
    public async Task Tor_kartochkalardan_quriladi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        Assert.Empty(dialogs.Errors);
        Assert.NotEmpty(vm.Cells);
        Assert.Single(vm.ClassGroups);

        // Dushanba 1-soatda dars bor.
        var busy = vm.Cells.Where(c => c.IsLessonCell && c.HasEntry).ToList();
        Assert.Single(busy);
        Assert.Equal("Matematika", busy[0].SubjectName);
        Assert.Equal(WeekDay.Dushanba, busy[0].Day);
        Assert.Equal(1, busy[0].LessonNumber);
    }

    [Fact]
    public async Task Joylashtirilmagan_darslar_qoyish_royxatida_korinadi()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        // Me'yor 3, qo'yilgan 1 → 2 soat qoldi (bitta dars ta'rifi).
        var option = Assert.Single(vm.VisiblePlaceLessons);
        Assert.Contains("Matematika", option.Name, StringComparison.Ordinal);
        Assert.Contains("2 soat", option.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dars_qoyish_kartochka_yaratadi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        var target = vm.Cells.First(c => c.IsLessonCell && !c.HasEntry && c.LessonNumber == 3);
        vm.SelectCell(target);
        vm.PlaceLesson = vm.VisiblePlaceLessons.First();

        await vm.PlaceCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.Equal("Dars muvaffaqiyatli qo'yildi.", vm.PlacementSummary);

        var cards = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync();
        Assert.Equal(2, cards.Count);
        Assert.Contains(cards, c => c.PeriodNo == 3 && c.DayNo == DayNumbering.ToDayNo(target.Day));
    }

    [Fact]
    public async Task Band_katakka_qoyilsa_rad_etiladi()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        var busy = vm.Cells.First(c => c.IsLessonCell && c.HasEntry);
        vm.SelectCell(busy);
        vm.PlaceLesson = vm.VisiblePlaceLessons.First();

        await vm.PlaceCommand.ExecuteAsync(null);

        Assert.Equal("Dars qo'yilmadi — to'siqlar mavjud.", vm.PlacementSummary);
        Assert.NotEmpty(vm.PlacementConflicts);

        // Bazada hech narsa qo'shilmadi.
        var cards = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync();
        Assert.Single(cards);
    }

    [Fact]
    public async Task Darsni_ochirish_kartochkani_bazadan_olib_tashlaydi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        var busy = vm.Cells.First(c => c.IsLessonCell && c.HasEntry);
        vm.SelectCell(busy);

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.Empty(await db.GetFromNewScope<ICardBoardService>().GetCardsAsync());

        // Ro'yxatda endi 3 soat qoldi.
        Assert.Contains("3 soat", vm.VisiblePlaceLessons.First().Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sinf_jadvalini_tozalash_kartochkalarni_ochiradi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        await vm.ClearScheduleCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.Empty(await db.GetFromNewScope<ICardBoardService>().GetCardsAsync());
    }
}
