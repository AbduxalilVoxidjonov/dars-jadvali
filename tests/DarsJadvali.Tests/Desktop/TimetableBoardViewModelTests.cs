using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Desktop.ViewModels;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Jadval yadrosi ViewModel'ining <b>haqiqiy baza</b> bilan tekshiruvi:
/// to'r quriladi, smena filtri ishlaydi, joylashtirilmagan panel aniq ro'yxatdan keladi.
/// </summary>
/// <remarks>
/// Avalonia ishga tushirilmaydi — ViewModel toza C#, shuning uchun to'liq sinovdan o'tadi.
/// </remarks>
public sealed class TimetableBoardViewModelTests
{
    /// <summary>Dialog oynalarini bosmaydigan soxta servis.</summary>
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

        public Task<string?> OpenFileAsync(
            string title = "Faylni tanlang", string filterName = "XML fayl", string extension = "xml")
            => Task.FromResult<string?>(null);
    }

    private static async Task<(TestDbFactory Db, TimetableBoardViewModel Vm, SilentDialogService Dialogs)>
        BuildAsync()
    {
        var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 6);

        var teacher = db.AddTeacher("Voxidjonov Abduxalil");
        var other = db.AddTeacher("Karimova Nodira");
        var math = db.AddSubject("Matematika");
        var physics = db.AddSubject("Fizika");
        var a = db.AddClassGroup("5-A", room: "201");
        var b = db.AddClassGroup("5-B", room: "202");

        db.AddAssignment(teacher, math, a, weeklyHours: 4);
        db.AddAssignment(other, physics, b, weeklyHours: 3);

        db.EnsureActiveSchedule();
        db.AddEntry(a, math, teacher, WeekDay.Dushanba, 1, room: "201");
        db.AddEntry(a, math, teacher, WeekDay.Seshanba, 2, room: "201");
        db.AddEntry(b, physics, other, WeekDay.Chorshanba, 3, room: "202");

        await new LegacyToV2Backfill(db.Context, db.Get<ICardOccurrenceProjector>()).RunAsync();

        var dialogs = new SilentDialogService();

        var vm = new TimetableBoardViewModel(
            db.Get<ICardBoardService>(),
            db.Get<IScheduleSnapshotProvider>(),
            db.Get<ISchedulingStore>(),
            db.Get<IScheduleSetService>(),
            db.Get<ITeacherService>(),
            db.Get<ISubjectService>(),
            db.Get<IAvailabilityService>(),
            dialogs);

        await vm.LoadAsync();

        return (db, vm, dialogs);
    }

    [Fact]
    public async Task Tor_haqiqiy_kartochkalardan_quriladi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var _ = db;

        Assert.Empty(dialogs.Errors);
        Assert.False(vm.IsGridEmpty);
        Assert.NotEmpty(vm.Rows);
        Assert.NotEmpty(vm.DayHeaders);

        // Uchta kartochka bazadan keldi.
        Assert.Equal(3, vm.Board.Cards.Count(c => c.IsPlaced));
        Assert.All(vm.Board.Cards.Where(c => c.IsPlaced), c => Assert.NotNull(c.EntityId));

        // Qatorlardagi soat raqamlari qo'ng'iroq jadvalidan keladi.
        var periods = vm.Rows.Select(r => r.Period).Distinct().OrderBy(p => p).ToList();
        Assert.Equal(Enumerable.Range(1, 6), periods);
    }

    [Fact]
    public async Task Joylashtirilmagan_panel_aniq_royxatdan_toladi()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        // Matematika 4 − 2 = 2, fizika 3 − 1 = 2 → jami 4 ta karta.
        Assert.Equal(4, vm.UnplacedCards.Count);
        Assert.Equal("4 ta", vm.UnplacedText);
        Assert.All(vm.UnplacedCards, c => Assert.Null(c.EntityId));
        Assert.All(vm.UnplacedCards, c => Assert.True(c.LessonId > 0));
    }

    [Fact]
    public async Task Smena_tanlagichi_bazadagi_smenalardan_quriladi()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        // "Barcha smenalar" + 1-smena + 2-smena.
        Assert.Equal(3, vm.Shifts.Count);
        Assert.True(vm.HasShifts);
        Assert.Equal(0, vm.Shifts[0].ShiftNo);
        Assert.Equal(new[] { 1, 2 }, vm.Shifts.Skip(1).Select(s => s.ShiftNo));
    }

    [Fact]
    public async Task Ikkinchi_smena_tanlansa_tor_filtrlanadi()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        var beforeRows = vm.Rows.Count;
        Assert.True(beforeRows > 0);

        // Backfill barcha sinf va soatlarni 1-smenaga qo'ygan — 2-smenada hech narsa yo'q.
        vm.SelectedShift = vm.Shifts.Single(s => s.ShiftNo == 2);
        Assert.True(vm.IsGridEmpty);

        // 1-smena tanlansa hammasi qaytadi.
        vm.SelectedShift = vm.Shifts.Single(s => s.ShiftNo == 1);
        Assert.False(vm.IsGridEmpty);
        Assert.Equal(beforeRows, vm.Rows.Count);
    }

    [Fact]
    public async Task Ikki_smenada_soat_raqamlari_uzluksiz_va_filtr_ishlaydi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 12);

        var teacher = db.AddTeacher("Voxidjonov Abduxalil");
        var math = db.AddSubject("Matematika");
        var first = db.AddClassGroup("5-A");
        var second = db.AddClassGroup("9-A");

        db.AddAssignment(teacher, math, first, weeklyHours: 2);
        db.AddAssignment(teacher, math, second, weeklyHours: 2);
        db.EnsureActiveSchedule();
        db.AddEntry(first, math, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(second, math, teacher, WeekDay.Dushanba, 7);

        await new LegacyToV2Backfill(db.Context, db.Get<ICardOccurrenceProjector>()).RunAsync();

        // Backfill hammani 1-smenaga qo'yadi. Ikkinchi smenani qo'lda tuzamiz:
        // soat raqamlari UZLUKSIZ qoladi — 1-smena 1..6, 2-smena 7..12.
        var shift2 = db.Context.Shifts.Single(s => s.ShiftNo == 2);
        foreach (var period in db.Context.Periods.Where(p => p.PeriodNo >= 7))
        {
            period.ShiftId = shift2.Id;
        }

        db.Context.SchoolClasses.Single(c => c.Name == "9-A").ShiftId = shift2.Id;
        db.Context.SaveChanges();

        var dialogs = new SilentDialogService();
        var vm = new TimetableBoardViewModel(
            db.Get<ICardBoardService>(),
            db.Get<IScheduleSnapshotProvider>(),
            db.Get<ISchedulingStore>(),
            db.Get<IScheduleSetService>(),
            db.Get<ITeacherService>(),
            db.Get<ISubjectService>(),
            db.Get<IAvailabilityService>(),
            dialogs);

        await vm.LoadAsync();
        Assert.Empty(dialogs.Errors);

        // 1-smena: 1..6 soat, faqat 5-A sinfi.
        vm.SelectedShift = vm.Shifts.Single(s => s.ShiftNo == 1);
        Assert.Equal(Enumerable.Range(1, 6), vm.Rows.Select(r => r.Period).Distinct().OrderBy(p => p));
        Assert.Equal(new[] { "5-A (1-smena)" }, vm.Rows.Select(r => r.ScopeName).Distinct());

        // 2-smena: 7..12 soat, faqat 9-A sinfi.
        vm.SelectedShift = vm.Shifts.Single(s => s.ShiftNo == 2);
        Assert.Equal(Enumerable.Range(7, 6), vm.Rows.Select(r => r.Period).Distinct().OrderBy(p => p));
        Assert.Equal(new[] { "9-A (2-smena)" }, vm.Rows.Select(r => r.ScopeName).Distinct());

        // Barcha smenalar: 1..12 soat, ikkala sinf.
        vm.SelectedShift = vm.Shifts.Single(s => s.ShiftNo == 0);
        Assert.Equal(Enumerable.Range(1, 12), vm.Rows.Select(r => r.Period).Distinct().OrderBy(p => p));
        Assert.Equal(2, vm.Rows.Select(r => r.ScopeId).Distinct().Count());
    }

    [Fact]
    public async Task Qulflash_bazaga_yoziladi_va_qayta_yuklanganda_qoladi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        var card = vm.Board.Cards.First(c => c.IsPlaced);
        Assert.False(card.IsLocked);

        vm.ToggleLockCommand.Execute(card);

        // Yozish navbat orqali ketadi — tugashini kutamiz.
        await vm.CancelPendingWorkAsync();

        Assert.Empty(dialogs.Errors);

        var reread = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync();
        Assert.True(reread.Single(c => c.CardId == card.EntityId).IsLocked);
    }

    [Fact]
    public async Task Kartani_kochirish_bazaga_yoziladi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var __ = db;

        var card = vm.Board.Cards.First(c => c.Day == WeekDay.Dushanba);

        Assert.True(vm.PickUp(card));
        vm.DropAt(WeekDay.Juma, 5);
        await vm.CancelPendingWorkAsync();

        Assert.Empty(dialogs.Errors);
        Assert.Equal(WeekDay.Juma, card.Day);
        Assert.Equal(5, card.Period);

        var reread = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync();
        var moved = reread.Single(c => c.CardId == card.EntityId);
        Assert.Equal(DayNumbering.ToDayNo(WeekDay.Juma), moved.DayNo);
        Assert.Equal(5, moved.PeriodNo);
    }

    [Fact]
    public async Task Qulflangan_karta_kochmaydi()
    {
        var (db, vm, _) = await BuildAsync();
        using var __ = db;

        var card = vm.Board.Cards.First(c => c.IsPlaced);
        vm.ToggleLockCommand.Execute(card);
        await vm.CancelPendingWorkAsync();

        Assert.True(card.IsLocked);
        Assert.False(vm.PickUp(card));
    }
}
