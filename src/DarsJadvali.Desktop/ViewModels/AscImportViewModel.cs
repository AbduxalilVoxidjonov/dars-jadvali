using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Import;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Import sehrgarining qadamlari.</summary>
public enum AscImportStep
{
    /// <summary>Fayl hali tanlanmagan.</summary>
    ChooseFile = 0,

    /// <summary>Fayl tanlangan — sozlamalar kiritilmoqda.</summary>
    Configure = 1,

    /// <summary>Oldindan ko'rish tayyor — tasdiqlash kutilmoqda.</summary>
    Preview = 2,

    /// <summary>Import bajarildi — natija hisoboti ko'rsatilyapti.</summary>
    Done = 3
}

/// <summary>
/// «aSc importi» sahifasi: XML fayl tanlash → sozlamalar → oldindan ko'rish → import.
/// </summary>
/// <remarks>
/// <para><b>Nima uchun ikki bosqich.</b> <see cref="IAscXmlImporter.PreviewAsync"/>
/// haqiqiy importni tranzaksiya ichida bajarib, oxirida uni qaytaradi. Shu sababli
/// "oldindan ko'rish nima deb aytgan bo'lsa, import ham shuni qiladi" — foydalanuvchi
/// bazaga tegmasdan turib natijani ko'radi.</para>
/// <para><b>Uzoq ish.</b> Import EF Core bilan ishlaydi va katta faylda soniyalab davom
/// etadi. Shu sababli u <see cref="Task.Run(Func{Task},CancellationToken)"/> orqali fon
/// oqimiga chiqariladi — UI qotib qolmaydi. Bu xavfsiz: sahifaning barcha amallari
/// <see cref="ViewModelBase.RunExclusiveAsync"/> navbatidan o'tadi, ya'ni bitta
/// <c>DbContext</c> ga bir vaqtda faqat bitta amal tegadi (M-01).</para>
/// <para><b>Fayl bir marta o'qiladi.</b> Tanlangan faylning baytlari xotirada saqlanadi:
/// oldindan ko'rish va import AYNI baytlar ustida ishlaydi, fayl orada o'zgarsa ham
/// hisobot bilan natija farq qilmaydi.</para>
/// </remarks>
public sealed partial class AscImportViewModel : ViewModelBase
{
    /// <summary>Bitta kod bo'yicha ko'rsatiladigan namuna xabarlar soni.</summary>
    private const int SampleLimit = 5;

    /// <summary>Ruxsat etilgan eng katta fayl (aSc eksporti bundan kichik bo'ladi).</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    private readonly IAscXmlImporter _importer;
    private readonly IAcademicYearService _years;
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;

    /// <summary>Tanlangan faylning xom baytlari (kodirovka o'quvchining ishi).</summary>
    private byte[]? _xml;

    [ObservableProperty]
    private AscImportStep _step = AscImportStep.ChooseFile;

    [ObservableProperty]
    private AcademicYear? _selectedAcademicYear;

    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string _fileSummary = string.Empty;

    [ObservableProperty]
    private ImportMergeMode _mergeMode = ImportMergeMode.Merge;

    [ObservableProperty]
    private bool _importCards = true;

    [ObservableProperty]
    private bool _skipStudents = true;

    [ObservableProperty]
    private string _schedulePrefix = "aSc import";

    [ObservableProperty]
    private bool _activateFirstSchedule;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _reportText = string.Empty;

    [ObservableProperty]
    private string _sourceSummary = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    /// <summary>Oxirgi hisobot muvaffaqiyatlimi (xato darajasidagi xabar yo'qmi).</summary>
    [ObservableProperty]
    private bool _lastRunSucceeded;

    /// <summary>Yangi ViewModel yaratadi.</summary>
    public AscImportViewModel(
        IAscXmlImporter importer,
        IAcademicYearService years,
        IDialogService dialogs,
        MainViewModel main)
    {
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _years = years ?? throw new ArgumentNullException(nameof(years));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _main = main ?? throw new ArgumentNullException(nameof(main));
    }

    /// <summary>Maqsad o'quv yili tanlagichi.</summary>
    public ObservableCollection<AcademicYear> AcademicYears { get; } = new();

    /// <summary>Bo'limlar bo'yicha statistika (topildi / yaratiladi / yangilanadi / o'tkaziladi).</summary>
    public ObservableCollection<ImportStatRowViewModel> Stats { get; } = new();

    /// <summary>Xabarlar KOD bo'yicha guruhlangan ro'yxati.</summary>
    public ObservableCollection<ImportMessageGroupViewModel> MessageGroups { get; } = new();

    /// <summary>Fayl tanlanganmi.</summary>
    public bool HasFile => _xml is not null;

    /// <summary>Radio tugma uchun: «birlashtirish» rejimi tanlanganmi.</summary>
    public bool IsMergeMode
    {
        get => MergeMode == ImportMergeMode.Merge;
        set
        {
            if (value)
            {
                MergeMode = ImportMergeMode.Merge;
            }
        }
    }

    /// <summary>Radio tugma uchun: «almashtirish» rejimi tanlanganmi.</summary>
    public bool IsReplaceMode
    {
        get => MergeMode == ImportMergeMode.Replace;
        set
        {
            if (value)
            {
                MergeMode = ImportMergeMode.Replace;
            }
        }
    }

    /// <summary>Oldindan ko'rish yoki import natijasi bormi.</summary>
    public bool HasReport => ReportText.Length > 0;

    /// <summary>Oldindan ko'rish tayyor va importga ruxsat berilganmi.</summary>
    public bool HasValidPreview => Step == AscImportStep.Preview && LastRunSucceeded;

    /// <summary>Import muvaffaqiyatli tugadimi (jadvalga o'tish tugmasi shunda yonadi).</summary>
    public bool HasSuccessfulImport => Step == AscImportStep.Done && LastRunSucceeded;

    /// <summary>Xabarlar umuman bormi.</summary>
    public bool HasMessages => MessageGroups.Count > 0;

    /// <summary>Ogohlantirish va xatolar haqidagi qisqa sarlavha.</summary>
    public string MessagesTitle => MessageGroups.Count == 0
        ? "Ogohlantirish yo'q"
        : $"Xabarlar — {MessageGroups.Count} xil kod, jami {MessageGroups.Sum(g => g.Count)} ta";

    /// <summary>«Oldindan ko'rish» tugmasi yoqilganmi.</summary>
    public bool CanPreview => IsNotBusy && HasFile && SelectedAcademicYear is not null;

    /// <summary>«Import qilish» tugmasi yoqilganmi.</summary>
    public bool CanImport => IsNotBusy && HasFile && SelectedAcademicYear is not null && HasValidPreview;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken ct = default)
        => RunExclusiveAsync(LoadYearsAsync, ct);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(IsBusy) or nameof(Step) or nameof(LastRunSucceeded)
            or nameof(HasFile) or nameof(HasReport))
        {
            OnPropertyChanged(nameof(CanPreview));
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(HasValidPreview));
            OnPropertyChanged(nameof(HasSuccessfulImport));
            NotifyCommandsCanExecuteChanged();
        }
    }

    // -------------------------------------------------------------------------
    // 1-qadam: fayl tanlash
    // -------------------------------------------------------------------------

    /// <summary>aSc XML faylini tanlaydi va uni xotiraga o'qiydi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task ChooseFileAsync(CancellationToken ct = default)
    {
        string? path;

        try
        {
            path = await _dialogs
                .OpenFileAsync("aSc XML eksportini tanlang", "aSc TimeTables XML", "xml")
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Fayl tanlash oynasini ochib bo'lmadi.\n\n" + ex.Message)
                .ConfigureAwait(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ProgressText = "Fayl o'qilmoqda...";

            var info = new FileInfo(path);
            if (!info.Exists)
            {
                await _dialogs.ErrorAsync("Tanlangan fayl topilmadi:\n" + path).ConfigureAwait(true);
                return;
            }

            if (info.Length == 0)
            {
                await _dialogs.ErrorAsync("Tanlangan fayl bo'sh.").ConfigureAwait(true);
                return;
            }

            if (info.Length > MaxFileBytes)
            {
                await _dialogs.ErrorAsync(
                        $"Fayl juda katta ({FormatSize(info.Length)}). " +
                        $"Ruxsat etilgan eng katta hajm — {FormatSize(MaxFileBytes)}.")
                    .ConfigureAwait(true);
                return;
            }

            _xml = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(true);

            FilePath = path;
            FileName = info.Name;
            FileSummary = $"{info.Name} — {FormatSize(info.Length)}";

            ClearReport();
            Step = AscImportStep.Configure;
            StatusMessage = "Fayl tanlandi. O'quv yilini tekshirib, «Oldindan ko'rish» tugmasini bosing.";
            OnPropertyChanged(nameof(HasFile));
        }
        catch (OperationCanceledException)
        {
            // Sahifadan chiqildi — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _xml = null;
            OnPropertyChanged(nameof(HasFile));
            await _dialogs.ErrorAsync("Faylni o'qishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    // -------------------------------------------------------------------------
    // 2-qadam: oldindan ko'rish (bazaga yozilmaydi)
    // -------------------------------------------------------------------------

    /// <summary>Bazaga yozmasdan nima bo'lishini hisoblaydi.</summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private Task PreviewAsync(CancellationToken ct = default)
        => RunExclusiveAsync(PreviewCoreAsync, ct);

    private async Task PreviewCoreAsync(CancellationToken ct)
    {
        var xml = _xml;
        var year = SelectedAcademicYear;
        if (xml is null || year is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ProgressText = "Fayl tahlil qilinmoqda, oldindan ko'rish tayyorlanmoqda...";
            StatusMessage = "Oldindan ko'rish...";

            var options = BuildOptions(year.Id) with { DryRun = true };

            var preview = await Task.Run(async () =>
            {
                await using var stream = new MemoryStream(xml, writable: false);
                return await _importer.PreviewAsync(stream, options, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(true);

            ApplyResult(preview.Result);
            Step = AscImportStep.Preview;

            StatusMessage = preview.IsValid
                ? "Oldindan ko'rish tayyor — bazaga hech narsa yozilmadi."
                : "Oldindan ko'rishda xatolar topildi — import qilib bo'lmaydi.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Oldindan ko'rish bekor qilindi.";
        }
        catch (AscImportException ex)
        {
            await ShowReadErrorAsync(ex).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Oldindan ko'rish bajarilmadi.";
            await _dialogs.ErrorAsync("Oldindan ko'rishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    // -------------------------------------------------------------------------
    // 3-qadam: haqiqiy import
    // -------------------------------------------------------------------------

    /// <summary>Tasdiqlashdan so'ng XML'ni bazaga yuklaydi.</summary>
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync(CancellationToken ct = default)
    {
        var year = SelectedAcademicYear;
        if (year is null)
        {
            return;
        }

        var warning = MergeMode == ImportMergeMode.Replace
            ? "\n\nDIQQAT: «Almashtirish» rejimi tanlangan — «" + year.Name +
              "» o'quv yilining barcha mavjud darslari va kartochkalari avval O'CHIRILADI. " +
              "Buni ortga qaytarib bo'lmaydi."
            : string.Empty;

        var confirmed = await _dialogs.ConfirmAsync(
                $"«{FileName}» fayli «{year.Name}» o'quv yiliga import qilinsinmi?" + warning,
                "aSc importini tasdiqlang")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        await RunExclusiveAsync(token => ImportCoreAsync(year, token), ct).ConfigureAwait(true);
    }

    private async Task ImportCoreAsync(AcademicYear year, CancellationToken ct)
    {
        var xml = _xml;
        if (xml is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ProgressText = "Import bajarilmoqda — bu bir necha soniya olishi mumkin...";
            StatusMessage = "Import...";

            var options = BuildOptions(year.Id) with { DryRun = false };

            var result = await Task.Run(async () =>
            {
                await using var stream = new MemoryStream(xml, writable: false);
                return await _importer.ImportAsync(stream, options, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(true);

            ApplyResult(result);
            Step = AscImportStep.Done;

            if (result.Success)
            {
                ProgressText = "Jadval tanlagichi yangilanmoqda...";

                // Import yangi jadval variantlari yaratgan bo'lishi mumkin — yuqoridagi
                // tanlagich va uning orqasidagi faol jadval qaytadan o'qiladi. Boshqa
                // sahifalar har navigatsiyada yangi qamrovda qurilgani uchun o'zi yangilanadi.
                await _main.RefreshSelectorsAsync(CancellationToken.None).ConfigureAwait(true);

                StatusMessage =
                    $"Import tugadi: {result.TotalCreated} ta yaratildi, " +
                    $"{result.TotalUpdated} ta yangilandi.";
            }
            else
            {
                StatusMessage = "Import bajarilmadi — hisobotdagi xatolarni ko'ring.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Import bekor qilindi — bazaga hech narsa yozilmadi.";
        }
        catch (AscImportException ex)
        {
            await ShowReadErrorAsync(ex).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Import bajarilmadi.";
            await _dialogs.ErrorAsync("Import paytida xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    // -------------------------------------------------------------------------
    // Yordamchi buyruqlar
    // -------------------------------------------------------------------------

    /// <summary>Bajarilayotgan uzoq amalni to'xtatadi.</summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private Task CancelRunAsync()
    {
        ProgressText = "To'xtatilmoqda...";
        return CancelPendingWorkAsync();
    }

    /// <summary>Hisobot matnini almashish buferiga nusxalaydi.</summary>
    [RelayCommand(CanExecute = nameof(HasReport))]
    private async Task CopyReportAsync()
    {
        await _dialogs.CopyToClipboardAsync(ReportText).ConfigureAwait(true);
        StatusMessage = "Hisobot nusxalandi.";
    }

    /// <summary>Import tugagach dars jadvali sahifasiga o'tadi.</summary>
    [RelayCommand(CanExecute = nameof(HasSuccessfulImport))]
    private void GoToTimetable() => _main.GoToTimetable();

    // -------------------------------------------------------------------------
    // Ichki mantiq
    // -------------------------------------------------------------------------

    private async Task LoadYearsAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            var items = await _years.GetAllAsync(ct).ConfigureAwait(true);

            var keepId = SelectedAcademicYear?.Id ?? _main.SelectedAcademicYear?.Id;

            AcademicYears.Clear();
            foreach (var item in items)
            {
                AcademicYears.Add(item);
            }

            SelectedAcademicYear =
                AcademicYears.FirstOrDefault(y => y.Id == keepId) ?? AcademicYears.FirstOrDefault();

            StatusMessage = AcademicYears.Count == 0
                ? "O'quv yili topilmadi — avval «O'quv yillari» sahifasida bittasini yarating."
                : "aSc XML faylini tanlang.";
        }
        catch (OperationCanceledException)
        {
            // Sahifadan chiqildi — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("O'quv yillarini yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ImportOptions BuildOptions(int academicYearId) => new()
    {
        AcademicYearId = academicYearId,
        MergeMode = MergeMode,
        ImportCards = ImportCards,
        SkipStudents = SkipStudents,
        SchedulePrefix = string.IsNullOrWhiteSpace(SchedulePrefix) ? "aSc import" : SchedulePrefix.Trim(),
        ActivateFirstSchedule = ActivateFirstSchedule,
    };

    /// <summary>Natijani ekranga yoyadi: statistika, guruhlangan xabarlar, matnli hisobot.</summary>
    private void ApplyResult(ImportResult result)
    {
        Stats.Clear();
        foreach (var stat in result.Stats.Where(s => s.HasAny))
        {
            Stats.Add(new ImportStatRowViewModel(stat));
        }

        MessageGroups.Clear();
        foreach (var group in GroupMessages(result.Messages))
        {
            MessageGroups.Add(group);
        }

        ReportText = result.ToReport();
        LastRunSucceeded = result.Success;

        SourceSummary = result.Source is { } src
            ? $"Format: {src.FormatName} • kunlar: {src.DaysPerWeek} • haftalar: {src.WeeksInCycle} • " +
              $"choraklar: {src.TermsCount} • darslar: {src.LessonCount} • kartochkalar: {src.CardCount}"
            : string.Empty;

        ResultSummary = result.DryRun
            ? $"Oldindan ko'rish: {result.TotalCreated} ta yaratiladi, " +
              $"{result.TotalUpdated} ta yangilanadi, {result.TotalSkipped} ta o'tkaziladi. " +
              "Bazaga hech narsa yozilmadi."
            : $"Natija: {result.TotalCreated} ta yaratildi, {result.TotalUpdated} ta yangilandi, " +
              $"{result.TotalSkipped} ta o'tkazib yuborildi.";

        if (result.ScheduleNames.Count > 0)
        {
            ResultSummary += " Jadval variantlari: " + string.Join(", ", result.ScheduleNames) + ".";
        }

        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(MessagesTitle));
    }

    /// <summary>
    /// Xabarlarni KOD bo'yicha guruhlaydi.
    /// </summary>
    /// <remarks>
    /// Buzuq eksportda bitta kod (masalan <c>ASC-UNKNOWN-TEACHER</c>) yuzlab marta
    /// takrorlanishi mumkin. Ro'yxatga hammasini to'kish hisobotni o'qib bo'lmas qiladi,
    /// shuning uchun har kod bitta qator bo'lib chiqadi: soni va bir nechta namunasi bilan.
    /// </remarks>
    private static IReadOnlyList<ImportMessageGroupViewModel> GroupMessages(
        IReadOnlyList<ImportMessage> messages)
    {
        if (messages.Count == 0)
        {
            return Array.Empty<ImportMessageGroupViewModel>();
        }

        return messages
            .GroupBy(m => m.Code, StringComparer.Ordinal)
            .Select(g => new ImportMessageGroupViewModel(
                g.Key,
                g.Max(m => m.Severity),
                g.Count(),
                g.Take(SampleLimit).Select(FormatSample).ToList()))
            .OrderByDescending(g => g.Severity)
            .ThenByDescending(g => g.Count)
            .ThenBy(g => g.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static string FormatSample(ImportMessage message) =>
        message.Reference is null ? message.Text : $"{message.Text} ({message.Reference})";

    /// <summary>Sozlama o'zgardi — eski oldindan ko'rish endi haqiqatga mos emas.</summary>
    private void InvalidatePreview()
    {
        if (Step is AscImportStep.ChooseFile)
        {
            return;
        }

        ClearReport();
        Step = AscImportStep.Configure;
        StatusMessage = "Sozlama o'zgardi — «Oldindan ko'rish» tugmasini qaytadan bosing.";
    }

    private void ClearReport()
    {
        Stats.Clear();
        MessageGroups.Clear();
        ReportText = string.Empty;
        SourceSummary = string.Empty;
        ResultSummary = string.Empty;
        LastRunSucceeded = false;

        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(MessagesTitle));
    }

    private Task ShowReadErrorAsync(AscImportException ex)
    {
        StatusMessage = "aSc faylini o'qib bo'lmadi.";
        return _dialogs.ErrorAsync(
            "aSc XML faylini o'qib bo'lmadi.\n\n" + ex.Message,
            "Import bajarilmadi");
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bayt",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.##} MB"),
    };

    partial void OnSelectedAcademicYearChanged(AcademicYear? value) => InvalidatePreview();

    partial void OnMergeModeChanged(ImportMergeMode value)
    {
        OnPropertyChanged(nameof(IsMergeMode));
        OnPropertyChanged(nameof(IsReplaceMode));
        InvalidatePreview();
    }

    partial void OnImportCardsChanged(bool value) => InvalidatePreview();

    partial void OnSkipStudentsChanged(bool value) => InvalidatePreview();

    partial void OnActivateFirstScheduleChanged(bool value) => InvalidatePreview();

    partial void OnSchedulePrefixChanged(string value) => InvalidatePreview();
}

/// <summary>Hisobot jadvalining bitta qatori: bo'lim × topildi / yaratildi / yangilandi / o'tkazildi.</summary>
public sealed class ImportStatRowViewModel
{
    /// <summary>Statistikadan qator yasaydi.</summary>
    public ImportStatRowViewModel(ImportEntityStat stat)
    {
        ArgumentNullException.ThrowIfNull(stat);

        Title = stat.Title;
        Found = stat.Found;
        Created = stat.Created;
        Updated = stat.Updated;
        Skipped = stat.Skipped;
    }

    /// <summary>Bo'lim nomi (o'zbekcha).</summary>
    public string Title { get; }

    /// <summary>Manba XML'da topilgan.</summary>
    public int Found { get; }

    /// <summary>Yaratiladigan/yaratilgan.</summary>
    public int Created { get; }

    /// <summary>Yangilanadigan/yangilangan.</summary>
    public int Updated { get; }

    /// <summary>O'tkazib yuboriladigan/yuborilgan.</summary>
    public int Skipped { get; }
}

/// <summary>
/// Bitta xabar KODI bo'yicha guruhlangan qator: soni va bir nechta namunasi.
/// </summary>
public sealed class ImportMessageGroupViewModel
{
    /// <summary>Barqaror kodlarning o'zbekcha tushuntirishlari.</summary>
    /// <remarks>
    /// Ro'yxatda yo'q kod chiqsa kodning o'zi ko'rsatiladi — yangi kod qo'shilganda
    /// ekran buzilmaydi, faqat matni texnikroq bo'ladi.
    /// </remarks>
    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["ASC-ACTIVATED"] = "Jadval varianti faollashtirildi",
        ["ASC-CARD-CONFLICT"] = "Kartochka to'qnashuvi — o'rin band edi",
        ["ASC-CARD-DUPLICATE"] = "Takrorlangan kartochka",
        ["ASC-CARD-MULTIDAY"] = "Kartochka bir nechta kunga tegishli",
        ["ASC-CARD-NO-DAY"] = "Kartochkada kun ko'rsatilmagan",
        ["ASC-CARD-OVERFLOW"] = "Kartochka jadval chegarasidan chiqib ketdi",
        ["ASC-CARDS-OFF"] = "Kartochkalar import qilinmadi (sozlama)",
        ["ASC-DB-CONSTRAINT"] = "Baza cheklovi buzildi — import qaytarildi",
        ["ASC-DIM-DAYS"] = "Hafta kunlari soni moslashtirildi",
        ["ASC-DIM-TERMS"] = "Choraklar soni moslashtirildi",
        ["ASC-DIM-WEEKS"] = "Hafta sikli moslashtirildi",
        ["ASC-ENTIRECLASS-ADDED"] = "Sinfning «butun sinf» guruhi qo'shildi",
        ["ASC-FRACTIONAL-PERIODS"] = "Kasrli haftalik soat butunlashtirildi",
        ["ASC-INVALID-VALUE"] = "Noto'g'ri qiymat — o'tkazib yuborildi",
        ["ASC-LESSON-NO-PERIODS"] = "Darsda haftalik soat ko'rsatilmagan",
        ["ASC-LESSON-NO-TEACHER"] = "Darsga o'qituvchi biriktirilmagan",
        ["ASC-MULTI-ENTIRECLASS"] = "Sinfda bir nechta «butun sinf» guruhi",
        ["ASC-MULTI-HOMEROOM"] = "Sinfga bir nechta xona biriktirilgan",
        ["ASC-NO-GROUPS"] = "Darsda guruh ko'rsatilmagan",
        ["ASC-NO-SCHEDULE"] = "Jadval varianti yaratilmadi",
        ["ASC-NO-YEAR"] = "Maqsad o'quv yili topilmadi",
        ["ASC-REPLACE"] = "Almashtirish rejimi — eski darslar o'chirildi",
        ["ASC-STUDENTS-SKIPPED"] = "O'quvchilar o'tkazib yuborildi",
        ["ASC-TRUNCATED"] = "Matn juda uzun — qisqartirildi",
        ["ASC-UNKNOWN-CLASS"] = "Noma'lum sinfga havola",
        ["ASC-UNKNOWN-CLASSROOM"] = "Noma'lum xonaga havola",
        ["ASC-UNKNOWN-GRADE"] = "Noma'lum parallelga havola",
        ["ASC-UNKNOWN-GROUP"] = "Noma'lum guruhga havola",
        ["ASC-UNKNOWN-LESSON"] = "Noma'lum darsga havola",
        ["ASC-UNKNOWN-PERIOD"] = "Noma'lum dars soatiga havola",
        ["ASC-UNKNOWN-SUBJECT"] = "Noma'lum fanga havola",
        ["ASC-UNKNOWN-TEACHER"] = "Noma'lum o'qituvchiga havola",
        ["ASC-UNKNOWN-TERM"] = "Noma'lum chorakka havola",
        ["ASC-UNSUPPORTED"] = "Qo'llab-quvvatlanmaydigan bo'lim",
    };

    /// <summary>Guruhni yaratadi.</summary>
    public ImportMessageGroupViewModel(
        string code, ImportSeverity severity, int count, IReadOnlyList<string> samples)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Severity = severity;
        Count = count;
        Samples = samples ?? Array.Empty<string>();
    }

    /// <summary>Barqaror kod (masalan <c>ASC-UNKNOWN-TEACHER</c>).</summary>
    public string Code { get; }

    /// <summary>
    /// Guruhdagi eng yuqori daraja. <b>Semantik</b> qiymat — rangni XAML konverteri beradi.
    /// </summary>
    public ImportSeverity Severity { get; }

    /// <summary>Shu kod bilan kelgan xabarlar soni.</summary>
    public int Count { get; }

    /// <summary>Namuna xabarlar (ko'pi bilan beshta).</summary>
    public IReadOnlyList<string> Samples { get; }

    /// <summary>O'zbekcha sarlavha; kod tanish bo'lmasa kodning o'zi.</summary>
    public string Title => Titles.TryGetValue(Code, out var title) ? title : Code;

    /// <summary>Daraja nomi.</summary>
    public string SeverityText => Severity switch
    {
        ImportSeverity.Error => "Xato",
        ImportSeverity.Warning => "Ogohlantirish",
        _ => "Ma'lumot",
    };

    /// <summary>Ro'yxatdagi sarlavha qatori.</summary>
    public string Header => $"{Title} — {Count} ta";

    /// <summary>Kod va daraja ko'rsatilgan izoh qatori.</summary>
    public string CodeText => $"{Code} • {SeverityText}";

    /// <summary>Namunalar ro'yxati to'liq emasmi.</summary>
    public bool HasMore => Count > Samples.Count;

    /// <summary>"…va yana N ta" qatori.</summary>
    public string MoreText => HasMore
        ? $"…va yana {Count - Samples.Count} ta shunday xabar"
        : string.Empty;
}
