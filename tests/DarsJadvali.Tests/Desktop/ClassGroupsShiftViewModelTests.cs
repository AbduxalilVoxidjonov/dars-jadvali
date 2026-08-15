using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.ViewModels;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Sinflar ekrani: smena ustuni ko'rinadi, tanlangan smena bazaga yoziladi va
/// noto'g'ri smena tushunarli o'zbekcha xabar bilan rad etiladi.
/// </summary>
public sealed class ClassGroupsShiftViewModelTests
{
    private sealed class RecordingDialogService : IDialogService
    {
        public List<string> Errors { get; } = new();

        public List<string> Infos { get; } = new();

        public Task InfoAsync(string message, string title = "Ma'lumot")
        {
            Infos.Add(message);
            return Task.CompletedTask;
        }

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

    /// <summary>Eski modelda ikkita sinf + v2 ga ko'chirish (dastur ham shu yo'ldan boradi).</summary>
    private static async Task<(TestDbFactory Db, ClassGroupsViewModel Vm, RecordingDialogService Dialogs)>
        BuildAsync()
    {
        var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 7);

        var teacher = db.AddTeacher("Voxidjonov Abduxalil");
        var math = db.AddSubject("Matematika");
        var a = db.AddClassGroup("5-A", room: "201");
        db.AddClassGroup("5-B");

        db.AddAssignment(teacher, math, a, weeklyHours: 4);
        db.EnsureActiveSchedule();

        await new LegacyToV2Backfill(db.Context, db.Get<ICardOccurrenceProjector>()).RunAsync();

        var dialogs = new RecordingDialogService();
        var vm = new ClassGroupsViewModel(
            db.Get<IClassGroupService>(),
            new ClassShiftService(db.Get<IUnitOfWork>(), db.Get<ISchedulingStore>()),
            dialogs);

        await vm.LoadAsync();
        return (db, vm, dialogs);
    }

    [Fact]
    public async Task Sinflar_royxatida_smena_ustuni_toldiriladi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var _ = db;

        Assert.Empty(dialogs.Errors);
        Assert.Equal(2, vm.ClassGroups.Count);

        var row = vm.ClassGroups.Single(c => c.Name == "5-A");

        // Backfill sinfni v2 modelga ko'chirgan — ikkala Id ham mavjud.
        Assert.True(row.SchoolClassId > 0);
        Assert.False(string.IsNullOrWhiteSpace(row.ShiftName));
    }

    [Fact]
    public async Task Tanlangan_smena_bazaga_yoziladi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var _ = db;

        var row = vm.ClassGroups.Single(c => c.Name == "5-A");

        // Ikkinchi smena kerak — backfill yaratmagan bo'lsa qo'shamiz.
        var yearId = db.Context.SchoolClasses.AsNoTracking().Single(c => c.Id == row.SchoolClassId).AcademicYearId;
        var second = db.Context.Shifts.FirstOrDefault(s => s.AcademicYearId == yearId && s.ShiftNo == 2);

        if (second is null)
        {
            second = new Shift
            {
                AcademicYearId = yearId,
                ShiftNo = 2,
                Name = "2-smena",
                ShortName = "II",
            };

            db.Context.Shifts.Add(second);
            await db.Context.SaveChangesAsync();
        }

        await vm.LoadAsync();

        vm.SelectedClassGroup = vm.ClassGroups.Single(c => c.Name == "5-A");
        await vm.EditCommand.ExecuteAsync(vm.SelectedClassGroup);

        Assert.True(vm.HasShifts);
        Assert.True(vm.CanEditShift);

        vm.EditShift = vm.Shifts.Single(s => s.ShiftId == second.Id);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);

        var saved = db.Context.SchoolClasses.AsNoTracking().Single(c => c.Id == row.SchoolClassId);

        Assert.Equal(second.Id, saved.ShiftId);
        Assert.Equal("2-smena", vm.ClassGroups.Single(c => c.Name == "5-A").ShiftName);
    }

    [Fact]
    public async Task Begona_oquv_yili_smenasi_ozbekcha_xabar_beradi()
    {
        var (db, vm, dialogs) = await BuildAsync();
        using var _ = db;

        var row = vm.ClassGroups.Single(c => c.Name == "5-A");

        // Boshqa o'quv yilining smenasi — bunday tanlov rad etilishi kerak.
        var otherYear = new AcademicYear
        {
            Name = "2030–2031",
            StartYear = 2030,
            DaysPerWeek = 6,
            WeeksInCycle = 1,
            TermsCount = 4,
        };

        db.Context.AcademicYears.Add(otherYear);
        await db.Context.SaveChangesAsync();

        var foreign = new Shift
        {
            AcademicYearId = otherYear.Id,
            ShiftNo = 2,
            Name = "Begona smena",
            ShortName = "X",
        };

        db.Context.Shifts.Add(foreign);
        await db.Context.SaveChangesAsync();

        await vm.LoadAsync();

        vm.SelectedClassGroup = vm.ClassGroups.Single(c => c.Name == "5-A");
        await vm.EditCommand.ExecuteAsync(vm.SelectedClassGroup);

        var foreignOption = vm.Shifts.FirstOrDefault(s => s.ShiftId == foreign.Id);
        if (foreignOption is null)
        {
            // Servis begona yil smenasini ro'yxatga umuman qo'shmasa — bu ham to'g'ri xatti-harakat.
            return;
        }

        vm.EditShift = foreignOption;
        await vm.SaveCommand.ExecuteAsync(null);

        var message = Assert.Single(dialogs.Errors);
        Assert.Contains("o'quv yiliga tegishli emas", message, StringComparison.Ordinal);

        // Baza tegilmagan.
        var saved = db.Context.SchoolClasses.AsNoTracking().Single(c => c.Id == row.SchoolClassId);
        Assert.NotEqual(foreign.Id, saved.ShiftId);
    }
}
