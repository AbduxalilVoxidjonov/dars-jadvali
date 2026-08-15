using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Infrastructure.Export;
using DarsJadvali.Scheduling.Pipeline;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Bosh sahifa: umumiy ko'rsatkichlar, jadval yadrosi, avtomatik tuzish, tekshiruv va PDF.</summary>
/// <remarks>
/// <para>
/// M-04: maktab jadvalini chizish endi bu ViewModel'ning ishi emas — u
/// <see cref="TimetableBoardViewModel"/> ga topshirilgan (virtualizatsiyalangan, tahrirlanadigan to'r).
/// </para>
/// <para>
/// Generatsiya <b>yangi</b> <see cref="IScheduleGenerationService"/> ga ulangan
/// (<c>Lesson</c> + <c>Card</c> modeli). Eski <c>IScheduleGenerator</c> (<c>[Obsolete]</c>)
/// Desktop'dan butunlay olib tashlandi.
/// </para>
/// </remarks>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly ITeacherService _teachers;
    private readonly ISubjectService _subjects;
    private readonly IClassGroupService _classGroups;
    private readonly IAssignmentService _assignments;
    private readonly IScheduleSetService _schedules;
    private readonly ICardBoardService _cards;
    private readonly IScheduleGenerationService _generation;
    private readonly IPlanCapacityService _capacity;
    private readonly IBoardCardRewriter _rewriter;
    private readonly IScopedTimetablePdfExporter _pdfExporter;
    private readonly IDialogService _dialogs;

    private CancellationTokenSource? _generationCts;

    [ObservableProperty]
    private int _teacherCount;

    [ObservableProperty]
    private int _subjectCount;

    [ObservableProperty]
    private int _classGroupCount;

    [ObservableProperty]
    private int _placedLessonCount;

    [ObservableProperty]
    private int _assignmentCount;

    [ObservableProperty]
    private int _weeklyHoursTotal;

    [ObservableProperty]
    private string _weeklyHoursText = "/ 0";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private double _generationProgressValue;

    [ObservableProperty]
    private double _generationProgressMax = 100;

    /// <summary>Joriy fazaning o'zbekcha nomi ("Optimallashtirish").</summary>
    [ObservableProperty]
    private string _generationPhase = string.Empty;

    /// <summary>Foiz matni ("64%").</summary>
    [ObservableProperty]
    private string _generationPercentText = string.Empty;

    [ObservableProperty]
    private string _generationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasGenerationMessage;

    /// <summary>Determinizm urug'i — bir xil urug' + bir xil ma'lumot → bir xil jadval.</summary>
    [ObservableProperty]
    private int _seed = 12345;

    /// <summary>Qidiruv byudjeti (aSc "Complexity of generation").</summary>
    [ObservableProperty]
    private ComplexityOption? _selectedComplexity;

    /// <summary>Qulflangan kartochkalar joyida qolsinmi.</summary>
    [ObservableProperty]
    private bool _keepLocked = true;

    /// <summary>To'liq bo'lmagan yechim ham saqlansinmi.</summary>
    [ObservableProperty]
    private bool _savePartial = true;

    /// <summary>Generatsiya hisoboti bormi.</summary>
    [ObservableProperty]
    private bool _hasGenerationReport;

    /// <summary>"Joylashgan / jami" matni.</summary>
    [ObservableProperty]
    private string _generationSummary = string.Empty;

    /// <summary>Yakuniy soft jarima matni.</summary>
    [ObservableProperty]
    private string _softCostText = string.Empty;

    /// <summary>Joylashtirilmagan darslar ro'yxati bo'sh emasmi.</summary>
    [ObservableProperty]
    private bool _hasUnplacedLessons;

    /// <summary>Buzilgan qat'iy cheklovlar bormi.</summary>
    [ObservableProperty]
    private bool _hasHardViolations;

    /// <summary>Generatsiyadan oldingi tekshiruv xatolari bormi.</summary>
    [ObservableProperty]
    private bool _hasVerificationFaults;

    /// <summary>Yumshatish tavsiyalari bormi.</summary>
    [ObservableProperty]
    private bool _hasRelaxationSuggestions;

    /// <summary>Jarima taqsimoti bormi.</summary>
    [ObservableProperty]
    private bool _hasPenaltyBreakdown;

    [ObservableProperty]
    private string _validationSummary = string.Empty;

    [ObservableProperty]
    private bool _hasValidationResult;

    /// <summary>Sig'im ogohlantirishlari bormi ("5-A: 47 soat, 35 ta slot").</summary>
    [ObservableProperty]
    private bool _hasCapacityWarnings;

    /// <summary>Sig'im tekshiruvining qisqacha xulosasi.</summary>
    [ObservableProperty]
    private string _capacitySummary = string.Empty;

    /// <summary>Sig'im tekshiruvi hech bo'lmasa bir marta bajarildimi.</summary>
    [ObservableProperty]
    private bool _hasCapacityResult;

    /// <summary>Yangi bosh sahifa ViewModel'i yaratadi.</summary>
    public DashboardViewModel(
        ITeacherService teachers,
        ISubjectService subjects,
        IClassGroupService classGroups,
        IAssignmentService assignments,
        IScheduleSetService schedules,
        ICardBoardService cards,
        IScheduleGenerationService generation,
        IPlanCapacityService capacity,
        IBoardCardRewriter rewriter,
        IScopedTimetablePdfExporter pdfExporter,
        IDialogService dialogs,
        TimetableBoardViewModel board)
    {
        _teachers = teachers;
        _subjects = subjects;
        _classGroups = classGroups;
        _assignments = assignments;
        _schedules = schedules;
        _cards = cards;
        _generation = generation;
        _capacity = capacity;
        _rewriter = rewriter;
        _pdfExporter = pdfExporter;
        _dialogs = dialogs;
        Board = board;

        foreach (var option in ComplexityOption.All)
        {
            Complexities.Add(option);
        }

        SelectedComplexity = Complexities.FirstOrDefault(c => c.Value == Complexity.Normal);

        // Ikkalasi bitta DI qamrovidagi bitta DbContext ustida ishlaydi — navbat ham bitta (M-01).
        ShareOperationQueueWith(board);
    }

    /// <summary>aSc uslubidagi jadval tahrirlash yadrosi (to'r, drag-drop, undo/redo).</summary>
    public TimetableBoardViewModel Board { get; }

    /// <summary>Tekshiruvda topilgan konfliktlar.</summary>
    public ObservableCollection<ConflictRowViewModel> ValidationConflicts { get; } = new();

    /// <summary>Qidiruv byudjeti variantlari.</summary>
    public ObservableCollection<ComplexityOption> Complexities { get; } = new();

    /// <summary>Joylashtirilmagan darslar ro'yxati (aniq ro'yxat, taxmin emas).</summary>
    public ObservableCollection<UnplacedLessonRowViewModel> UnplacedLessons { get; } = new();

    /// <summary>Buzilgan hard cheklovlar.</summary>
    public ObservableCollection<string> HardViolations { get; } = new();

    /// <summary>Generatsiyadan oldingi tekshiruv xatolari ("Xona yetishmaydi").</summary>
    public ObservableCollection<string> VerificationFaults { get; } = new();

    /// <summary>
    /// Sig'im ogohlantirishlari: rejadagi soat mavjud slotlardan oshib ketgan
    /// sinf / guruh / o'qituvchilar.
    /// </summary>
    public ObservableCollection<CapacityWarningRowViewModel> CapacityWarnings { get; } = new();

    /// <summary>Qaysi cheklovni yumshatish kerakligi haqidagi tavsiyalar.</summary>
    public ObservableCollection<string> RelaxationSuggestions { get; } = new();

    /// <summary>Soft jarimaning cheklovlar bo'yicha taqsimoti.</summary>
    public ObservableCollection<PenaltyRowViewModel> PenaltyBreakdown { get; } = new();

    /// <summary>Generator nomi.</summary>
    public string GeneratorName => "Algoritm: " + _generation.Name;

    /// <summary>Generator tavsifi.</summary>
    public string GeneratorDescription => _generation.Description;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken ct = default)
        => RunExclusiveAsync(RefreshCoreAsync, ct);

    /// <summary>Bosh sahifa ma'lumotlarini bazadan qayta o'qiydi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task RefreshAsync(CancellationToken ct = default)
        => RunExclusiveAsync(RefreshCoreAsync, ct);

    private async Task RefreshCoreAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Ma'lumotlar yuklanmoqda...";

            var teachers = await _teachers.GetAllAsync(ct).ConfigureAwait(true);
            var subjects = await _subjects.GetAllAsync(ct).ConfigureAwait(true);
            var classGroups = await _classGroups.GetAllAsync(ct).ConfigureAwait(true);
            var assignments = await _assignments.GetAllAsync(ct).ConfigureAwait(true);
            var cards = await _cards.GetCardsAsync(null, ct).ConfigureAwait(true);

            TeacherCount = teachers.Count;
            SubjectCount = subjects.Count;
            ClassGroupCount = classGroups.Count;
            AssignmentCount = assignments.Count;
            WeeklyHoursTotal = assignments.Sum(a => a.WeeklyHoursCount);
            WeeklyHoursText = "/ " + WeeklyHoursTotal;

            // Qo'yilgan soatlar = kartochkalar uzunliklari yig'indisi (juft dars 2 soat).
            PlacedLessonCount = cards.Sum(c => Math.Max(1, c.Length));

            StatusMessage = "Tayyor.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bekor qilindi.";
            return;
        }
        catch (Exception ex)
        {
            StatusMessage = "Yuklashda xatolik.";
            await _dialogs.ErrorAsync("Ma'lumotlarni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        // Jadval yadrosi o'z navbatida ma'lumotni bir marta o'qib, baholash keshini quradi.
        await Board.LoadAsync(ct).ConfigureAwait(true);
    }

    partial void OnGenerationMessageChanged(string value)
        => HasGenerationMessage = !string.IsNullOrWhiteSpace(value);

    /// <summary>Jadvalni avtomatik tuzadi (yangi <c>Card</c> asosidagi yadro).</summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private Task GenerateAsync(CancellationToken ct = default)
        => RunExclusiveAsync(GenerateCoreAsync, ct);

    private async Task GenerateCoreAsync(CancellationToken ct)
    {
        // aSc "Verify specification" fazasi kabi: reja sig'imdan oshgan bo'lsa
        // foydalanuvchi buni GENERATSIYADAN OLDIN biladi. Aks holda darslarning bir
        // qismi jimgina joylashmay qolardi va sabab ko'rinmasdi.
        var capacity = await CheckCapacityCoreAsync(ct, showDialogWhenClean: false).ConfigureAwait(true);

        var warningText = capacity.HasWarnings
            ? "\n\nDIQQAT — reja sig'imdan oshgan:\n" +
              string.Join("\n", capacity.Warnings.Take(8).Select(w => "• " + w.Message)) +
              (capacity.Warnings.Count > 8
                  ? $"\n… va yana {capacity.Warnings.Count - 8} ta.\n"
                  : "\n") +
              "Bu soatlar jadvalga sig'maydi — generator ularni joylashtira olmaydi."
            : string.Empty;

        var confirmed = await _dialogs.ConfirmAsync(
                "Jadval avtomatik tuziladi. Mavjud kartochkalar qaytadan joylashtiriladi" +
                (KeepLocked ? " (qulflanganlari joyida qoladi)" : string.Empty) +
                "." + warningText +
                "\n\nDavom etilsinmi?",
                "Jadvalni avtomatik tuzish")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        // Navbatning tokeni bilan bog'lanadi: sahifadan chiqilsa generatsiya ham to'xtaydi.
        _generationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            IsGenerating = true;
            GenerateCommand.NotifyCanExecuteChanged();
            CancelGenerationCommand.NotifyCanExecuteChanged();

            ClearReport();

            GenerationProgressValue = 0;
            GenerationProgressMax = 100;
            GenerationPhase = "Boshlanmoqda";
            GenerationPercentText = "0%";
            GenerationMessage = "Boshlanmoqda...";
            StatusMessage = "Jadval tuzilmoqda...";

            var progress = new Progress<ScheduleGenerationProgress>(p =>
            {
                GenerationProgressMax = 100;
                GenerationProgressValue = Math.Clamp(p.Percent, 0, 100);
                GenerationPhase = p.PhaseName;
                GenerationPercentText = p.Percent.ToString("F0", CultureInfo.CurrentCulture) + "%";
                GenerationMessage =
                    $"{p.PhaseName}: {p.PlacedCards}/{p.TotalCards} kartochka, jarima {p.SoftCost}";
            });

            var options = new ScheduleGenerationOptions
            {
                Seed = Seed,
                Complexity = SelectedComplexity?.Value ?? Complexity.Normal,
                KeepLocked = KeepLocked,
                SavePartial = SavePartial,
                AllowRelaxation = true,
            };

            // GenerateAsync HECH QACHON OperationCanceledException tashlamaydi —
            // bekor qilinsa Cancelled = true bo'lgan hisobot qaytadi.
            var report = await _generation
                .GenerateAsync(options, progress, _generationCts.Token)
                .ConfigureAwait(true);

            ShowReport(report);

            if (report.Cancelled)
            {
                GenerationMessage = "Jadval tuzish bekor qilindi — jadval o'zgarmadi.";
                StatusMessage = "Bekor qilindi.";
                return;
            }

            // Generatsiyadan keyin jadval qayta yuklanadi (navbat ichida — shu tokendan foydalanadi).
            await RefreshCoreAsync(ct).ConfigureAwait(true);

            GenerationMessage = report.Success
                ? $"Tayyor: {report.PlacedCards} ta kartochka joylashtirildi."
                : $"{report.PlacedCards} ta joylashtirildi, {report.UnplacedCards} tasi joylashmadi.";

            StatusMessage = GenerationMessage;
        }
        catch (Exception ex)
        {
            GenerationMessage = "Xatolik yuz berdi.";
            await _dialogs.ErrorAsync("Jadvalni tuzishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsGenerating = false;
            _generationCts?.Dispose();
            _generationCts = null;
            GenerateCommand.NotifyCanExecuteChanged();
            CancelGenerationCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Hisobotni ekranga chiqaradi.</summary>
    private void ShowReport(ScheduleGenerationReport report)
    {
        ClearReport();

        GenerationSummary = report.Cancelled
            ? "Bekor qilindi — hech narsa yozilmadi."
            : $"Joylashgan: {report.PlacedCards} / {report.TotalCards} kartochka" +
              (report.Applied ? string.Empty : "  (natija SAQLANMADI)") +
              $"  •  {report.Elapsed.TotalSeconds.ToString("F1", CultureInfo.CurrentCulture)} soniya";

        SoftCostText = $"Yumshoq jarima: {report.SoftCost}";

        foreach (var lesson in report.UnplacedLessons)
        {
            UnplacedLessons.Add(new UnplacedLessonRowViewModel(lesson));
        }

        foreach (var violation in report.HardViolations)
        {
            HardViolations.Add(violation);
        }

        foreach (var fault in report.VerificationFaults)
        {
            VerificationFaults.Add(fault);
        }

        foreach (var suggestion in report.RelaxationSuggestions)
        {
            RelaxationSuggestions.Add(suggestion);
        }

        foreach (var share in report.PenaltyBreakdown.OrderByDescending(p => p.Penalty))
        {
            PenaltyBreakdown.Add(new PenaltyRowViewModel(share));
        }

        ValidationConflicts.Clear();
        foreach (var conflict in report.Conflicts)
        {
            ValidationConflicts.Add(new ConflictRowViewModel(conflict));
        }

        HasUnplacedLessons = UnplacedLessons.Count > 0;
        HasHardViolations = HardViolations.Count > 0;
        HasVerificationFaults = VerificationFaults.Count > 0;
        HasRelaxationSuggestions = RelaxationSuggestions.Count > 0;
        HasPenaltyBreakdown = PenaltyBreakdown.Count > 0;

        HasValidationResult = ValidationConflicts.Count > 0;
        HasGenerationReport = true;
    }

    private void ClearReport()
    {
        UnplacedLessons.Clear();
        HardViolations.Clear();
        VerificationFaults.Clear();
        RelaxationSuggestions.Clear();
        PenaltyBreakdown.Clear();
        GenerationSummary = string.Empty;
        SoftCostText = string.Empty;
        HasUnplacedLessons = false;
        HasHardViolations = false;
        HasVerificationFaults = false;
        HasRelaxationSuggestions = false;
        HasPenaltyBreakdown = false;
        HasGenerationReport = false;
    }

    private bool CanGenerate() => !IsGenerating;

    /// <summary>Generatsiyani bekor qiladi — jadval o'zgarishsiz qoladi.</summary>
    [RelayCommand(CanExecute = nameof(CanCancelGeneration))]
    private void CancelGeneration()
    {
        _generationCts?.Cancel();
        GenerationMessage = "Bekor qilinmoqda...";
    }

    private bool CanCancelGeneration() => IsGenerating;

    /// <summary>Butun jadvalni tekshiradi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ValidateAllAsync(CancellationToken ct = default)
        => RunExclusiveAsync(ValidateAllCoreAsync, ct);

    private async Task ValidateAllCoreAsync(CancellationToken ct)
    {
        // "Tekshirish" amali ikki qismdan iborat: sig'im (reja) va joylashtirish (fakt).
        await CheckCapacityCoreAsync(ct, showDialogWhenClean: false).ConfigureAwait(true);

        try
        {
            IsBusy = true;
            StatusMessage = "Jadval tekshirilmoqda...";

            var conflicts = await _generation.ValidateAsync(null, ct).ConfigureAwait(true);

            ValidationConflicts.Clear();
            foreach (var conflict in conflicts)
            {
                ValidationConflicts.Add(new ConflictRowViewModel(conflict));
            }

            HasValidationResult = true;

            if (conflicts.Count == 0)
            {
                ValidationSummary = HasCapacityWarnings
                    ? "Jadvalda to'qnashuv yo'q, lekin reja sig'imdan oshgan (pastga qarang)."
                    : "Jadvalda muammo topilmadi.";
            }
            else
            {
                var errors = conflicts.Count(c => c.Severity == ConflictSeverity.Error);
                var warnings = conflicts.Count - errors;
                ValidationSummary =
                    $"Jami {conflicts.Count} ta muammo: {errors} ta xato, {warnings} ta ogohlantirish.";
            }

            StatusMessage = "Tekshiruv yakunlandi.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bekor qilindi.";
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Tekshirishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Rejani sig'im bo'yicha tekshiradi: har sinf/guruh/o'qituvchi uchun
    /// "rejalashtirilgan soat" va "mavjud slot" solishtiriladi.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task CheckCapacityAsync(CancellationToken ct = default)
        => RunExclusiveAsync(token => CheckCapacityCoreAsync(token, showDialogWhenClean: true), ct);

    /// <summary>
    /// Sig'im tekshiruvini bajaradi va natijani ekranga chiqaradi.
    /// </summary>
    /// <param name="ct">Bekor qilish tokeni.</param>
    /// <param name="showDialogWhenClean">
    /// Muammo topilmasa ham xabar oynasi ko'rsatilsinmi (alohida "Tekshirish" tugmasi uchun).
    /// </param>
    private async Task<PlanCapacityReport> CheckCapacityCoreAsync(
        CancellationToken ct, bool showDialogWhenClean)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Reja sig'imi tekshirilmoqda...";

            var report = await _capacity.CheckAsync(null, ct).ConfigureAwait(true);

            CapacityWarnings.Clear();
            foreach (var warning in report.Warnings)
            {
                CapacityWarnings.Add(new CapacityWarningRowViewModel(warning));
            }

            // Yadroning Verify fazasi xatolari — generatsiya hisobotidagi bilan AYNI manba.
            VerificationFaults.Clear();
            foreach (var fault in report.VerificationFaults)
            {
                VerificationFaults.Add(fault);
            }

            HasVerificationFaults = VerificationFaults.Count > 0;
            HasCapacityWarnings = CapacityWarnings.Count > 0;
            HasCapacityResult = true;
            CapacitySummary = report.Summary;
            StatusMessage = report.Summary;

            if (showDialogWhenClean && !report.HasWarnings)
            {
                await _dialogs.InfoAsync(report.Summary, "Sig'im tekshiruvi").ConfigureAwait(true);
            }

            return report;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bekor qilindi.";
            return PlanCapacityReport.Empty;
        }
        catch (Exception ex)
        {
            CapacitySummary = "Sig'imni tekshirib bo'lmadi.";
            HasCapacityResult = true;
            await _dialogs.ErrorAsync("Reja sig'imini tekshirishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
            return PlanCapacityReport.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Butun jadvalni tozalaydi (tasdiqdan keyin).</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ClearScheduleAsync(CancellationToken ct = default)
        => RunExclusiveAsync(ClearScheduleCoreAsync, ct);

    private async Task ClearScheduleCoreAsync(CancellationToken ct)
    {
        var confirmed = await _dialogs.ConfirmAsync(
                "Barcha sinflarning jadvali to'liq o'chirilsinmi?\n\nBu amalni qaytarib bo'lmaydi.",
                "Jadvalni tozalash")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var scheduleId = await _schedules.GetActiveIdAsync(ct).ConfigureAwait(true);
            await _rewriter.RewriteAsync(scheduleId, Array.Empty<CardWrite>(), ct).ConfigureAwait(true);

            ValidationConflicts.Clear();
            ValidationSummary = string.Empty;
            HasValidationResult = false;
            ClearReport();
            StatusMessage = "Jadval tozalandi.";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Jadvalni tozalashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        // Tozalashdan keyin jadval qayta yuklanadi.
        await RefreshCoreAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Joriy ko'rinishga mos jadvalni PDF ga yuklab oladi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ExportPdfAsync(CancellationToken ct = default)
        => RunExclusiveAsync(ExportPdfCoreAsync, ct);

    private async Task ExportPdfCoreAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "PDF tayyorlanmoqda...";

            // E-01: qamrov metod nomida aniq ko'rsatiladi — "barcha sinflar" tasodifan chiqmaydi.
            var filterId = Board.ViewKind == Models.TimetableViewKind.Class
                ? Board.SelectedScope?.Id ?? 0
                : 0;

            var options = new PdfExportOptions { SchoolName = null };

            var document = filterId == 0
                ? await _pdfExporter.ExportSchoolScheduleAsync(options, ct).ConfigureAwait(true)
                : await _pdfExporter.ExportClassScheduleAsync(filterId, options, ct).ConfigureAwait(true);

            var path = await _dialogs.SaveFileAsync(document.FileName).ConfigureAwait(true);

            if (path is null)
            {
                StatusMessage = "PDF saqlash bekor qilindi.";
                return;
            }

            await File.WriteAllBytesAsync(path, document.Content, ct).ConfigureAwait(true);

            StatusMessage = "PDF saqlandi.";
            await _dialogs.InfoAsync($"PDF saqlandi:\n{path}", "PDF yuklab olindi").ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bekor qilindi.";
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("PDF yaratishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>Qidiruv byudjeti (aSc "Complexity of generation") tanlagichidagi band.</summary>
/// <param name="Value">Yadro qiymati.</param>
/// <param name="Name">O'zbekcha nom.</param>
public sealed record ComplexityOption(Complexity Value, string Name)
{
    /// <summary>Barcha variantlar.</summary>
    public static IReadOnlyList<ComplexityOption> All { get; } = new[]
    {
        new ComplexityOption(Complexity.Small, "Kichik (tez)"),
        new ComplexityOption(Complexity.Normal, "Oddiy"),
        new ComplexityOption(Complexity.Large, "Katta (sekinroq)"),
        new ComplexityOption(Complexity.Huge, "Juda katta (eng sekin)"),
    };
}

/// <summary>Hisobotdagi bitta joylashtirilmagan dars qatori.</summary>
public sealed class UnplacedLessonRowViewModel
{
    /// <summary>Qatorni yaratadi.</summary>
    public UnplacedLessonRowViewModel(UnplacedLessonView source)
    {
        ArgumentNullException.ThrowIfNull(source);

        SubjectName = source.SubjectName;
        Scope = string.IsNullOrWhiteSpace(source.GroupName)
            ? source.ClassName
            : source.ClassName + " / " + source.GroupName;
        Teachers = source.TeacherNames.Count == 0
            ? "(o'qituvchi biriktirilmagan)"
            : string.Join(", ", source.TeacherNames);
        HoursText = $"{source.RemainingPeriods} soat qoldi ({source.PlacedPeriods}/{source.PeriodsPerWeek})";
    }

    /// <summary>Fan nomi.</summary>
    public string SubjectName { get; }

    /// <summary>Sinf va guruh.</summary>
    public string Scope { get; }

    /// <summary>O'qituvchilar.</summary>
    public string Teachers { get; }

    /// <summary>Qolgan soat matni.</summary>
    public string HoursText { get; }
}

/// <summary>Sig'im ogohlantirishlaridagi bitta qator.</summary>
/// <remarks>
/// Rang/qalinlik qaytarilmaydi — <see cref="Scope"/> semantik enum sifatida beriladi,
/// ko'rinish esa XAML tomonda hal qilinadi (M-05 qoidasi).
/// </remarks>
public sealed class CapacityWarningRowViewModel
{
    /// <summary>Qatorni yaratadi.</summary>
    public CapacityWarningRowViewModel(CapacityWarning source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Scope = source.Scope;
        Name = source.Name;
        Message = source.Message;
        OverflowText = $"{source.Overflow} soat sig'maydi";
        ScopeText = source.Scope switch
        {
            CapacityScope.Class => "Sinf",
            CapacityScope.Group => "Guruh",
            _ => "O'qituvchi",
        };
    }

    /// <summary>Ogohlantirish kimga tegishli (semantik enum).</summary>
    public CapacityScope Scope { get; }

    /// <summary>Sinf / guruh / o'qituvchi nomi.</summary>
    public string Name { get; }

    /// <summary>To'liq o'zbekcha xabar.</summary>
    public string Message { get; }

    /// <summary>"Sinf" / "Guruh" / "O'qituvchi".</summary>
    public string ScopeText { get; }

    /// <summary>Sig'maydigan soat matni.</summary>
    public string OverflowText { get; }
}

/// <summary>Jarima taqsimotidagi bitta qator.</summary>
public sealed class PenaltyRowViewModel
{
    /// <summary>Qatorni yaratadi.</summary>
    public PenaltyRowViewModel(PenaltyShare source)
    {
        ArgumentNullException.ThrowIfNull(source);

        ConstraintId = source.ConstraintId;
        Name = source.Name;
        PenaltyText = source.Penalty.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>Cheklov kodi.</summary>
    public string ConstraintId { get; }

    /// <summary>Cheklov nomi.</summary>
    public string Name { get; }

    /// <summary>Jarima qiymati.</summary>
    public string PenaltyText { get; }
}
