using DarsJadvali.Application.Import;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.ViewModels;
using DarsJadvali.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// «aSc importi» sahifasining mantiqi: qadamlar, tugmalarning yonishi, tasdiqlash,
/// ogohlantirishlarni kod bo'yicha guruhlash va bekor qilish.
/// </summary>
/// <remarks>
/// Importer soxta: bu yerda XML tahlili emas, EKRAN xatti-harakati sinaladi
/// (haqiqiy import <c>Import</c> papkasidagi sinovlarda tekshirilgan).
/// </remarks>
public sealed class AscImportViewModelTests : IDisposable
{
    private readonly TestDbFactory _db = new();
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Sinov tozalashi — muhim emas.
            }
        }

        _db.Dispose();
    }

    // =====================================================================
    // Soxta bog'liqliklar
    // =====================================================================

    private sealed class FakeImporter : IAscXmlImporter
    {
        /// <summary>Har bir <c>PreviewAsync</c> chaqiruvining parametrlari.</summary>
        public List<ImportOptions> PreviewCalls { get; } = new();

        /// <summary>Har bir <c>ImportAsync</c> chaqiruvining parametrlari.</summary>
        public List<ImportOptions> ImportCalls { get; } = new();

        public ImportResult PreviewResult { get; set; } = Success();

        public ImportResult ImportResult { get; set; } = Success();

        public Exception? Throws { get; set; }

        /// <summary>Amal boshlanganini bildiradi (bekor qilish sinovi uchun).</summary>
        public TaskCompletionSource Entered { get; } = new();

        /// <summary>To'ldirilsa, amal shu vazifa tugagunicha kutadi.</summary>
        public TaskCompletionSource? Gate { get; set; }

        public async Task<ImportPreview> PreviewAsync(
            Stream xml, ImportOptions options, CancellationToken ct = default)
        {
            await ConsumeAsync(xml, ct).ConfigureAwait(false);
            PreviewCalls.Add(options);

            return new ImportPreview(PreviewResult with
            {
                DryRun = true,
                AcademicYearId = options.AcademicYearId,
            });
        }

        public async Task<ImportResult> ImportAsync(
            Stream xml, ImportOptions options, CancellationToken ct = default)
        {
            await ConsumeAsync(xml, ct).ConfigureAwait(false);
            ImportCalls.Add(options);

            return ImportResult with
            {
                DryRun = false,
                AcademicYearId = options.AcademicYearId,
            };
        }

        private async Task ConsumeAsync(Stream xml, CancellationToken ct)
        {
            using var reader = new StreamReader(xml);
            await reader.ReadToEndAsync(ct).ConfigureAwait(false);

            Entered.TrySetResult();

            if (Gate is not null)
            {
                await Gate.Task.WaitAsync(ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();

            if (Throws is not null)
            {
                throw Throws;
            }
        }

        public static ImportResult Success(params ImportMessage[] messages) => new()
        {
            Success = true,
            Stats = new[]
            {
                new ImportEntityStat(ImportEntityKind.Teacher, "O'qituvchilar", 3, 3, 0, 0),
                new ImportEntityStat(ImportEntityKind.Card, "Kartochkalar", 10, 8, 0, 2),
            },
            Messages = messages,
            ScheduleNames = new[] { "aSc import — 1-chorak" },
            Source = new AscSourceSummary("asctt2012", 6, 1, 4, 7, 5, 3, 4, 2, 6, 6, 12, 10, 0),
        };
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public List<string> Errors { get; } = new();

        public List<string> Confirmations { get; } = new();

        public string? Copied { get; private set; }

        /// <summary>«Faylni tanlash» oynasi shu yo'lni qaytaradi (null — bekor qilindi).</summary>
        public string? FileToPick { get; set; }

        public bool ConfirmResult { get; set; } = true;

        public Task InfoAsync(string message, string title = "Ma'lumot") => Task.CompletedTask;

        public Task ErrorAsync(string message, string title = "Xato")
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(string message, string title = "Tasdiqlang")
        {
            Confirmations.Add(message);
            return Task.FromResult(ConfirmResult);
        }

        public Task ShowValidationAsync(ValidationResult result) => Task.CompletedTask;

        public Task<bool> ConfirmWarningsAsync(ValidationResult result) => Task.FromResult(true);

        public Task CopyToClipboardAsync(string text)
        {
            Copied = text;
            return Task.CompletedTask;
        }

        public Task<string?> SaveFileAsync(
            string suggestedFileName, string filterName = "PDF hujjat", string extension = "pdf")
            => Task.FromResult<string?>(null);

        public Task<string?> OpenFileAsync(
            string title = "Faylni tanlang", string filterName = "XML fayl", string extension = "xml")
            => Task.FromResult(FileToPick);
    }

    private sealed class StubNavigationService : INavigationService
    {
        public ViewModelBase? Current => null;

        public event EventHandler<ViewModelBase>? Navigated;

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
            => Navigated?.Invoke(this, null!);

        public ViewModelBase NavigateToType(Type viewModelType) => throw new NotSupportedException();
    }

    // =====================================================================
    // Yordamchilar
    // =====================================================================

    private string WriteTempXml(string content = "<timetable />")
    {
        var path = Path.Combine(Path.GetTempPath(), $"asc-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private async Task<(AscImportViewModel Vm, FakeImporter Importer, RecordingDialogService Dialogs, MainViewModel Main)>
        BuildAsync()
    {
        _db.Context.AcademicYears.Add(new AcademicYear
        {
            Name = "2025–2026",
            StartYear = 2025,
            DaysPerWeek = 6,
            WeeksInCycle = 1,
            TermsCount = 4,
        });
        _db.Context.SaveChanges();

        var importer = new FakeImporter();
        var dialogs = new RecordingDialogService();
        var main = new MainViewModel(
            new StubNavigationService(),
            dialogs,
            _db.Get<IServiceScopeFactory>());

        var vm = new AscImportViewModel(
            importer,
            _db.Get<IAcademicYearService>(),
            dialogs,
            main);

        await vm.LoadAsync();

        return (vm, importer, dialogs, main);
    }

    /// <summary>Fayl tanlangan, oldindan ko'rishga tayyor holat.</summary>
    private async Task<(AscImportViewModel Vm, FakeImporter Importer, RecordingDialogService Dialogs, MainViewModel Main)>
        BuildWithFileAsync()
    {
        var built = await BuildAsync();
        built.Dialogs.FileToPick = WriteTempXml();

        await built.Vm.ChooseFileCommand.ExecuteAsync(null);

        return built;
    }

    // =====================================================================
    // Sinovlar
    // =====================================================================

    [Fact]
    public async Task Boshida_fayl_yoq_va_tugmalar_ochirilgan()
    {
        var (vm, importer, dialogs, _) = await BuildAsync();

        Assert.Equal(AscImportStep.ChooseFile, vm.Step);
        Assert.False(vm.HasFile);
        Assert.False(vm.CanPreview);
        Assert.False(vm.CanImport);
        Assert.False(vm.PreviewCommand.CanExecute(null));
        Assert.False(vm.ImportCommand.CanExecute(null));

        // O'quv yili tanlagichi bazadan to'ldi.
        Assert.Single(vm.AcademicYears);
        Assert.NotNull(vm.SelectedAcademicYear);

        Assert.Empty(importer.PreviewCalls);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task Fayl_tanlangach_oldindan_korish_yonadi_lekin_import_yonmaydi()
    {
        var (vm, _, dialogs, _) = await BuildWithFileAsync();

        Assert.True(vm.HasFile);
        Assert.Equal(AscImportStep.Configure, vm.Step);
        Assert.True(vm.CanPreview);
        Assert.False(vm.CanImport);
        Assert.Contains(".xml", vm.FileSummary, StringComparison.Ordinal);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task Oldindan_korish_quruq_rejimda_va_tanlangan_yil_bilan_chaqiriladi()
    {
        var (vm, importer, dialogs, _) = await BuildWithFileAsync();

        vm.MergeMode = ImportMergeMode.Replace;
        vm.ImportCards = false;
        vm.SchedulePrefix = "  Kirill importi  ";
        vm.ActivateFirstSchedule = true;

        await vm.PreviewCommand.ExecuteAsync(null);

        var options = Assert.Single(importer.PreviewCalls);
        Assert.True(options.DryRun);
        Assert.Equal(vm.SelectedAcademicYear!.Id, options.AcademicYearId);
        Assert.Equal(ImportMergeMode.Replace, options.MergeMode);
        Assert.False(options.ImportCards);
        Assert.True(options.ActivateFirstSchedule);
        Assert.Equal("Kirill importi", options.SchedulePrefix);

        // Hisobot ko'rindi, import tugmasi endi yonadi, bazaga hech narsa yozilmadi.
        Assert.Equal(AscImportStep.Preview, vm.Step);
        Assert.True(vm.HasReport);
        Assert.True(vm.CanImport);
        Assert.Empty(importer.ImportCalls);
        Assert.Contains("Bazaga hech narsa yozilmadi", vm.ResultSummary, StringComparison.Ordinal);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task Statistika_jadvali_bolimlar_boyicha_toladi()
    {
        var (vm, _, _, _) = await BuildWithFileAsync();

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Stats.Count);
        var teachers = vm.Stats.First(s => s.Title == "O'qituvchilar");
        Assert.Equal(3, teachers.Found);
        Assert.Equal(3, teachers.Created);

        var cards = vm.Stats.First(s => s.Title == "Kartochkalar");
        Assert.Equal(8, cards.Created);
        Assert.Equal(2, cards.Skipped);
    }

    [Fact]
    public async Task Sozlama_ozgarsa_oldindan_korish_bekor_qilinadi()
    {
        var (vm, _, _, _) = await BuildWithFileAsync();

        await vm.PreviewCommand.ExecuteAsync(null);
        Assert.True(vm.CanImport);

        vm.ImportCards = false;

        Assert.Equal(AscImportStep.Configure, vm.Step);
        Assert.False(vm.CanImport);
        Assert.False(vm.HasReport);
    }

    [Fact]
    public async Task Import_tasdiq_berilmasa_bajarilmaydi()
    {
        var (vm, importer, dialogs, _) = await BuildWithFileAsync();
        await vm.PreviewCommand.ExecuteAsync(null);

        dialogs.ConfirmResult = false;
        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Confirmations);
        Assert.Empty(importer.ImportCalls);
        Assert.Equal(AscImportStep.Preview, vm.Step);
    }

    [Fact]
    public async Task Almashtirish_rejimida_tasdiq_matni_ogohlantiradi()
    {
        var (vm, _, dialogs, _) = await BuildWithFileAsync();
        vm.MergeMode = ImportMergeMode.Replace;
        await vm.PreviewCommand.ExecuteAsync(null);

        dialogs.ConfirmResult = false;
        await vm.ImportCommand.ExecuteAsync(null);

        var text = Assert.Single(dialogs.Confirmations);
        Assert.Contains("O'CHIRILADI", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tasdiqlangach_import_bajariladi_va_tanlagich_yangilanadi()
    {
        var (vm, importer, dialogs, main) = await BuildWithFileAsync();
        await vm.PreviewCommand.ExecuteAsync(null);

        await vm.ImportCommand.ExecuteAsync(null);

        var options = Assert.Single(importer.ImportCalls);
        Assert.False(options.DryRun);
        Assert.Equal(vm.SelectedAcademicYear!.Id, options.AcademicYearId);

        Assert.Equal(AscImportStep.Done, vm.Step);
        Assert.True(vm.HasSuccessfulImport);
        Assert.True(vm.GoToTimetableCommand.CanExecute(null));
        Assert.Contains("ta yaratildi", vm.ResultSummary, StringComparison.Ordinal);
        Assert.Empty(dialogs.Errors);

        // Import tugagach yuqoridagi tanlagich (va u orqali boshqa ekranlar) yangilandi.
        Assert.NotEmpty(main.AcademicYears);
        Assert.NotEmpty(main.Schedules);
    }

    [Fact]
    public async Task Xatoli_oldindan_korish_importga_yol_bermaydi()
    {
        var (vm, importer, _, _) = await BuildWithFileAsync();

        importer.PreviewResult = FakeImporter.Success(
            new ImportMessage(ImportSeverity.Error, "ASC-NO-YEAR", "Maqsad o'quv yili topilmadi.")) with
        {
            Success = false,
        };

        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.False(vm.CanImport);
        Assert.False(vm.ImportCommand.CanExecute(null));
        Assert.True(vm.HasReport);
        Assert.Contains(vm.MessageGroups, g => g.Severity == ImportSeverity.Error);
    }

    [Fact]
    public async Task Buzuq_XML_dialogda_korsatiladi_dastur_yiqilmaydi()
    {
        var (vm, importer, dialogs, _) = await BuildWithFileAsync();
        importer.Throws = new AscImportException("Bu aSc TimeTables XML eksporti emas.");

        await vm.PreviewCommand.ExecuteAsync(null);

        var error = Assert.Single(dialogs.Errors);
        Assert.Contains("aSc TimeTables XML eksporti emas", error, StringComparison.Ordinal);
        Assert.False(vm.HasReport);
        Assert.False(vm.CanImport);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Ogohlantirishlar_kod_boyicha_guruhlanadi_royxat_komilmaydi()
    {
        var (vm, importer, _, _) = await BuildWithFileAsync();

        var messages = new List<ImportMessage>();
        for (var i = 1; i <= 500; i++)
        {
            messages.Add(new ImportMessage(
                ImportSeverity.Warning, "ASC-UNKNOWN-TEACHER",
                $"«{i}» raqamli o'qituvchi topilmadi.", $"T{i}"));
        }

        messages.Add(new ImportMessage(
            ImportSeverity.Warning, "ASC-CARD-CONFLICT", "Kartochka o'rni band edi.", "C1"));
        messages.Add(new ImportMessage(
            ImportSeverity.Warning, "ASC-CARD-CONFLICT", "Kartochka o'rni band edi.", "C2"));
        messages.Add(new ImportMessage(
            ImportSeverity.Info, "ASC-FRACTIONAL-PERIODS", "1,5 soat 2 ga butunlashtirildi."));
        messages.Add(new ImportMessage(
            ImportSeverity.Error, "ASC-DB-CONSTRAINT", "Baza cheklovi buzildi."));

        importer.PreviewResult = FakeImporter.Success(messages.ToArray());

        await vm.PreviewCommand.ExecuteAsync(null);

        // 504 ta xabar → 4 ta guruh.
        Assert.Equal(4, vm.MessageGroups.Count);
        Assert.True(vm.HasMessages);
        Assert.Contains("504", vm.MessagesTitle, StringComparison.Ordinal);

        // Avval xato, keyin ogohlantirishlar (soni bo'yicha), oxirida ma'lumot.
        Assert.Equal("ASC-DB-CONSTRAINT", vm.MessageGroups[0].Code);
        Assert.Equal("ASC-UNKNOWN-TEACHER", vm.MessageGroups[1].Code);
        Assert.Equal("ASC-CARD-CONFLICT", vm.MessageGroups[2].Code);
        Assert.Equal("ASC-FRACTIONAL-PERIODS", vm.MessageGroups[3].Code);

        // 500 ta bir xil xabar ro'yxatni ko'mib tashlamaydi: 5 ta namuna + "yana N ta".
        var teachers = vm.MessageGroups[1];
        Assert.Equal(500, teachers.Count);
        Assert.Equal(5, teachers.Samples.Count);
        Assert.True(teachers.HasMore);
        Assert.Equal("…va yana 495 ta shunday xabar", teachers.MoreText);
        Assert.Contains("(T1)", teachers.Samples[0], StringComparison.Ordinal);

        // Kod o'zbekchaga o'giriladi, semantik daraja saqlanadi (rang — konverterning ishi).
        Assert.Equal("Noma'lum o'qituvchiga havola", teachers.Title);
        Assert.Equal(ImportSeverity.Warning, teachers.Severity);
        Assert.Equal("Noma'lum o'qituvchiga havola — 500 ta", teachers.Header);

        var conflicts = vm.MessageGroups[2];
        Assert.Equal(2, conflicts.Count);
        Assert.False(conflicts.HasMore);
        Assert.Equal(string.Empty, conflicts.MoreText);
    }

    [Fact]
    public async Task Notanish_kod_ham_royxatda_korinadi()
    {
        var (vm, importer, _, _) = await BuildWithFileAsync();
        importer.PreviewResult = FakeImporter.Success(
            new ImportMessage(ImportSeverity.Warning, "ASC-YANGI-KOD", "Kelajakdagi kod."));

        await vm.PreviewCommand.ExecuteAsync(null);

        var group = Assert.Single(vm.MessageGroups);
        Assert.Equal("ASC-YANGI-KOD", group.Title);
        Assert.Contains("Ogohlantirish", group.CodeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uzoq_amalni_toxtatib_bolinadi()
    {
        var (vm, importer, dialogs, _) = await BuildWithFileAsync();
        importer.Gate = new TaskCompletionSource();

        var preview = vm.PreviewCommand.ExecuteAsync(null);

        // Amal haqiqatan boshlanganini kutamiz — shundan keyin bekor qilish ma'noli.
        await importer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(vm.IsBusy);
        Assert.True(vm.CancelRunCommand.CanExecute(null));

        await vm.CancelRunCommand.ExecuteAsync(null);
        await preview;

        Assert.False(vm.IsBusy);
        Assert.Empty(importer.PreviewCalls);
        Assert.Empty(dialogs.Errors);
        Assert.Equal("Oldindan ko'rish bekor qilindi.", vm.StatusMessage);
        Assert.False(vm.CanImport);
    }

    [Fact]
    public async Task Hisobotni_nusxalash_matnni_buferga_yozadi()
    {
        var (vm, _, dialogs, _) = await BuildWithFileAsync();
        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.True(vm.CopyReportCommand.CanExecute(null));
        await vm.CopyReportCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.Copied);
        Assert.Contains("aSc XML importi", dialogs.Copied!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fayl_tanlash_bekor_qilinsa_holat_ozgarmaydi()
    {
        var (vm, _, dialogs, _) = await BuildAsync();
        dialogs.FileToPick = null;

        await vm.ChooseFileCommand.ExecuteAsync(null);

        Assert.False(vm.HasFile);
        Assert.Equal(AscImportStep.ChooseFile, vm.Step);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task Bosh_fayl_rad_etiladi()
    {
        var (vm, _, dialogs, _) = await BuildAsync();
        dialogs.FileToPick = WriteTempXml(string.Empty);

        await vm.ChooseFileCommand.ExecuteAsync(null);

        Assert.False(vm.HasFile);
        Assert.Contains("bo'sh", Assert.Single(dialogs.Errors), StringComparison.Ordinal);
    }
}
