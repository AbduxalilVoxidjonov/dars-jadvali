using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Bosh sahifa: umumiy ko'rsatkichlar, maktab jadvali, avtomatik tuzish, tekshiruv va PDF.</summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private const string NoLessonsMessage =
        "Hali birorta dars qo'yilmagan. «Dars jadvali» sahifasidan qo'lda qo'shing " +
        "yoki yuqoridagi «Avtomatik tuzish» tugmasini bosing.";

    private const string NoClassGroupsMessage =
        "Hali birorta sinf qo'shilmagan. Avval «Sinflar» sahifasidan sinf qo'shing.";

    private readonly ITeacherService _teachers;
    private readonly ISubjectService _subjects;
    private readonly IClassGroupService _classGroups;
    private readonly IAssignmentService _assignments;
    private readonly IScheduleService _schedule;
    private readonly IWorkDayService _workDays;
    private readonly IScheduleValidator _validator;
    private readonly IScheduleGenerator _generator;
    private readonly ISchoolTimetablePdfExporter _pdfExporter;
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;

    private readonly List<ClassTimetableViewModel> _allTimetables = new();
    private readonly List<string> _dayHeaders = new();

    private CancellationTokenSource? _generationCts;
    private bool _isRefreshingFilters;

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

    [ObservableProperty]
    private string _generationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasGenerationMessage;

    [ObservableProperty]
    private string _validationSummary = string.Empty;

    [ObservableProperty]
    private bool _hasValidationResult;

    [ObservableProperty]
    private ClassFilterOption? _selectedClassFilter;

    [ObservableProperty]
    private bool _isTimetableEmpty = true;

    [ObservableProperty]
    private string _timetableEmptyMessage = NoLessonsMessage;

    /// <summary>Maktab jadvalining joriy holati — View shu asosda to'rni quradi.</summary>
    [ObservableProperty]
    private SchoolTimetableSnapshot? _timetable;

    /// <summary>Yangi bosh sahifa ViewModel'i yaratadi.</summary>
    public DashboardViewModel(
        ITeacherService teachers,
        ISubjectService subjects,
        IClassGroupService classGroups,
        IAssignmentService assignments,
        IScheduleService schedule,
        IWorkDayService workDays,
        IScheduleValidator validator,
        IScheduleGenerator generator,
        ISchoolTimetablePdfExporter pdfExporter,
        IDialogService dialogs,
        MainViewModel main)
    {
        _teachers = teachers;
        _subjects = subjects;
        _classGroups = classGroups;
        _assignments = assignments;
        _schedule = schedule;
        _workDays = workDays;
        _validator = validator;
        _generator = generator;
        _pdfExporter = pdfExporter;
        _dialogs = dialogs;
        _main = main;
    }

    /// <summary>Tekshiruvda topilgan konfliktlar.</summary>
    public ObservableCollection<ConflictRowViewModel> ValidationConflicts { get; } = new();

    /// <summary>Sinf filtri bandlari ("Barcha sinflar" + har bir sinf).</summary>
    public ObservableCollection<ClassFilterOption> ClassFilters { get; } = new();

    /// <summary>Generator nomi.</summary>
    public string GeneratorName => "Algoritm: " + _generator.Name;

    /// <summary>Generator tavsifi.</summary>
    public string GeneratorDescription => _generator.Description;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken ct = default) => RefreshAsync(ct);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Ma'lumotlar yuklanmoqda...";

            // Ma'lumot bir marta o'qiladi — quyidagi sikllar faqat xotirada ishlaydi.
            var teachers = await _teachers.GetAllAsync(ct).ConfigureAwait(true);
            var subjects = await _subjects.GetAllAsync(ct).ConfigureAwait(true);
            var classGroups = await _classGroups.GetAllAsync(ct).ConfigureAwait(true);
            var assignments = await _assignments.GetAllAsync(ct).ConfigureAwait(true);
            var entries = await _schedule.GetAllAsync(ct).ConfigureAwait(true);
            var activeDays = await _workDays.GetActiveAsync(ct).ConfigureAwait(true);
            var maxLesson = await _workDays.GetMaxLessonNumberAsync(ct).ConfigureAwait(true);
            var slots = await _workDays.GetLessonSlotsAsync(ct).ConfigureAwait(true);

            TeacherCount = teachers.Count;
            SubjectCount = subjects.Count;
            ClassGroupCount = classGroups.Count;
            AssignmentCount = assignments.Count;
            WeeklyHoursTotal = assignments.Sum(a => a.WeeklyHoursCount);
            WeeklyHoursText = "/ " + WeeklyHoursTotal;
            PlacedLessonCount = entries.Count;

            BuildClassTimetables(classGroups, entries, activeDays, maxLesson, slots);

            StatusMessage = "Tayyor.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bekor qilindi.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Yuklashda xatolik.";
            await _dialogs.ErrorAsync("Ma'lumotlarni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Umumiy maktab jadvalini bir martalik ma'lumot asosida quradi.</summary>
    private void BuildClassTimetables(
        IReadOnlyList<ClassGroup> classGroups,
        IReadOnlyList<ScheduleEntry> entries,
        IReadOnlyList<WorkDay> activeDays,
        int maxLessonNumber,
        IReadOnlyList<LessonSlot> slots)
    {
        var days = activeDays
            .OrderBy(w => w.DayOfWeek)
            .Select(w => w.DayOfWeek)
            .ToList();

        if (days.Count == 0)
        {
            days = WeekDayExtensions.All.Take(6).ToList();
        }

        var lastLesson = maxLessonNumber > 0 ? maxLessonNumber : 7;

        var slotTexts = new Dictionary<int, string>();
        foreach (var slot in slots)
        {
            slotTexts[slot.LessonNumber] = ToTimeText(slot.StartTime) + "-" + ToTimeText(slot.EndTime);
        }

        // Yozuvlarni sinf bo'yicha bir marta guruhlaymiz — sinflar bo'ylab DB ga qayta bormaymiz.
        var byClass = new Dictionary<int, Dictionary<(WeekDay Day, int Lesson), ScheduleEntry>>();
        foreach (var entry in entries)
        {
            if (!byClass.TryGetValue(entry.ClassGroupId, out var map))
            {
                map = new Dictionary<(WeekDay, int), ScheduleEntry>();
                byClass[entry.ClassGroupId] = map;
            }

            map[(entry.DayOfWeek, entry.LessonNumber)] = entry;
        }

        _dayHeaders.Clear();
        foreach (var day in days)
        {
            _dayHeaders.Add(day.ToUzbek());
        }

        _allTimetables.Clear();

        foreach (var classGroup in classGroups.OrderBy(c => c.Name, StringComparer.CurrentCulture))
        {
            byClass.TryGetValue(classGroup.Id, out var lookup);

            var block = new ClassTimetableViewModel
            {
                ClassGroupId = classGroup.Id,
                ClassName = classGroup.Name,
                RoomText = string.IsNullOrWhiteSpace(classGroup.RoomNumber)
                    ? string.Empty
                    : classGroup.RoomNumber!.Trim() + "-xona",
                LessonCount = lookup?.Count ?? 0,
            };

            for (var lesson = 1; lesson <= lastLesson; lesson++)
            {
                slotTexts.TryGetValue(lesson, out var time);

                var row = new ClassTimetableRowViewModel
                {
                    LessonText = lesson + "-soat",
                    TimeText = time ?? string.Empty,
                };

                foreach (var day in days)
                {
                    ScheduleEntry? entry = null;
                    lookup?.TryGetValue((day, lesson), out entry);

                    if (entry is null)
                    {
                        row.Cells.Add(new DashboardCellViewModel());
                        continue;
                    }

                    row.Cells.Add(new DashboardCellViewModel
                    {
                        HasEntry = true,
                        SubjectName = entry.Subject?.Name ?? "(fan)",
                        TeacherName = ShortName(entry.Teacher?.FullName),
                        RoomText = string.IsNullOrWhiteSpace(entry.RoomNumber)
                            ? string.Empty
                            : entry.RoomNumber!,
                        ColorCode = entry.Teacher?.ColorCode ?? "#90A4AE",
                    });
                }

                block.Rows.Add(row);
            }

            _allTimetables.Add(block);
        }

        RebuildClassFilters();

        if (classGroups.Count == 0)
        {
            TimetableEmptyMessage = NoClassGroupsMessage;
            IsTimetableEmpty = true;
        }
        else if (entries.Count == 0)
        {
            TimetableEmptyMessage = NoLessonsMessage;
            IsTimetableEmpty = true;
        }
        else
        {
            IsTimetableEmpty = false;
        }

        ApplyClassFilter();
    }

    /// <summary>Filtr ro'yxatini qayta quradi va oldingi tanlovni saqlab qoladi.</summary>
    private void RebuildClassFilters()
    {
        var previousId = SelectedClassFilter?.Id ?? 0;

        _isRefreshingFilters = true;

        try
        {
            ClassFilters.Clear();
            ClassFilters.Add(new ClassFilterOption(0, "Barcha sinflar"));

            foreach (var block in _allTimetables)
            {
                ClassFilters.Add(new ClassFilterOption(block.ClassGroupId, block.ClassName));
            }

            SelectedClassFilter = ClassFilters.FirstOrDefault(f => f.Id == previousId) ?? ClassFilters[0];
        }
        finally
        {
            _isRefreshingFilters = false;
        }
    }

    /// <summary>Tanlangan filtrga mos sinf guruhlaridan yangi "surat" tayyorlaydi.</summary>
    private void ApplyClassFilter()
    {
        var filterId = SelectedClassFilter?.Id ?? 0;
        var blocks = new List<ClassTimetableViewModel>();

        foreach (var block in _allTimetables)
        {
            if (filterId != 0 && block.ClassGroupId != filterId)
            {
                continue;
            }

            block.IsAlternate = blocks.Count % 2 == 1;
            blocks.Add(block);
        }

        Timetable = new SchoolTimetableSnapshot
        {
            DayHeaders = _dayHeaders.ToArray(),
            Blocks = blocks,
        };
    }

    partial void OnSelectedClassFilterChanged(ClassFilterOption? value)
    {
        if (_isRefreshingFilters)
        {
            return;
        }

        ApplyClassFilter();
    }

    partial void OnGenerationMessageChanged(string value)
        => HasGenerationMessage = !string.IsNullOrWhiteSpace(value);

    /// <summary>"Dars jadvali" sahifasiga o'tib, shu sinfni tanlab beradi.</summary>
    [RelayCommand]
    private void EditClass(ClassTimetableViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _main.GoToTimetable(item.ClassGroupId);
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
                "Jadval avtomatik tuziladi. Mavjud jadval o'chirilib, qaytadan tuziladi.\n\nDavom etilsinmi?",
                "Jadvalni avtomatik tuzish")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        _generationCts = new CancellationTokenSource();

        try
        {
            IsGenerating = true;
            GenerateCommand.NotifyCanExecuteChanged();
            CancelGenerationCommand.NotifyCanExecuteChanged();

            GenerationProgressValue = 0;
            GenerationProgressMax = 100;
            GenerationMessage = "Boshlanmoqda...";
            StatusMessage = "Jadval tuzilmoqda...";

            var progress = new Progress<GenerationProgress>(p =>
            {
                GenerationProgressMax = p.Total > 0 ? p.Total : 100;
                GenerationProgressValue = p.Current;
                GenerationMessage = string.IsNullOrWhiteSpace(p.Message)
                    ? $"{p.Current} / {p.Total}"
                    : $"{p.Current} / {p.Total} — {p.Message}";
            });

            var options = new GenerationOptions { ClearExisting = true };
            var result = await _generator
                .GenerateAsync(options, progress, _generationCts.Token)
                .ConfigureAwait(true);

            // Generatsiyadan keyin jadval qayta yuklanadi.
            await RefreshAsync(CancellationToken.None).ConfigureAwait(true);

            var text =
                (result.Success ? "Jadval muvaffaqiyatli tuzildi." : "Jadval to'liq tuzilmadi.") +
                Environment.NewLine + Environment.NewLine +
                $"Qo'yilgan darslar: {result.PlacedCount}" + Environment.NewLine +
                $"Qo'yilmagan darslar: {result.UnplacedCount}" + Environment.NewLine +
                $"Sarflangan vaqt: {result.Elapsed.TotalSeconds:0.0} soniya";

            if (result.Messages.Count > 0)
            {
                var shown = result.Messages.Take(15);
                text += Environment.NewLine + Environment.NewLine +
                        "Izohlar:" + Environment.NewLine +
                        string.Join(Environment.NewLine, shown.Select(m => "• " + m));

                if (result.Messages.Count > 15)
                {
                    text += Environment.NewLine + $"... va yana {result.Messages.Count - 15} ta izoh.";
                }
            }

            GenerationMessage = result.Success
                ? $"Tayyor: {result.PlacedCount} ta dars qo'yildi."
                : $"{result.PlacedCount} ta qo'yildi, {result.UnplacedCount} ta qo'yilmadi.";

            await _dialogs.InfoAsync(text, "Jadval tuzish natijasi").ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            GenerationMessage = "Jadval tuzish bekor qilindi.";
            StatusMessage = "Bekor qilindi.";
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

    private bool CanGenerate() => !IsGenerating;

    [RelayCommand(CanExecute = nameof(CanCancelGeneration))]
    private void CancelGeneration()
    {
        _generationCts?.Cancel();
        GenerationMessage = "Bekor qilinmoqda...";
    }

    private bool CanCancelGeneration() => IsGenerating;

    [RelayCommand]
    private async Task ValidateAllAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Jadval tekshirilmoqda...";

            var result = await _validator.ValidateAllAsync(ct).ConfigureAwait(true);

            ValidationConflicts.Clear();
            foreach (var conflict in result.Conflicts)
            {
                ValidationConflicts.Add(new ConflictRowViewModel(conflict));
            }

            HasValidationResult = true;

            if (result.Conflicts.Count == 0)
            {
                ValidationSummary = "Jadvalda muammo topilmadi.";
            }
            else
            {
                var errors = result.Conflicts.Count(c => c.Severity == ConflictSeverity.Error);
                var warnings = result.Conflicts.Count - errors;
                ValidationSummary =
                    $"Jami {result.Conflicts.Count} ta muammo: {errors} ta xato, {warnings} ta ogohlantirish.";
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

    /// <summary>Butun jadvalni tozalaydi (tasdiqdan keyin).</summary>
    [RelayCommand]
    private async Task ClearScheduleAsync(CancellationToken ct = default)
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
            await _schedule.ClearAsync(null, ct).ConfigureAwait(true);
            ValidationConflicts.Clear();
            ValidationSummary = string.Empty;
            HasValidationResult = false;
            StatusMessage = "Jadval tozalandi.";
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
        await RefreshAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Joriy sinf filtriga mos jadvalni PDF ga yuklab oladi.</summary>
    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "PDF tayyorlanmoqda...";

            var filterId = SelectedClassFilter?.Id ?? 0;

            var options = new PdfExportOptions
            {
                ClassGroupId = filterId == 0 ? null : filterId,
                SchoolName = null,
            };

            var pdf = await _pdfExporter.ExportAsync(options, ct).ConfigureAwait(true);
            var suggested = _pdfExporter.SuggestFileName(options, DateTime.Now);

            var path = await _dialogs.SaveFileAsync(suggested).ConfigureAwait(true);

            if (path is null)
            {
                StatusMessage = "PDF saqlash bekor qilindi.";
                return;
            }

            await File.WriteAllBytesAsync(path, pdf, ct).ConfigureAwait(true);

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

    /// <summary>TimeSpan ni "HH:mm" ko'rinishiga keltiradi.</summary>
    private static string ToTimeText(TimeSpan value)
        => value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>"Voxidjonov Abduxalil" → "Voxidjonov A." ko'rinishidagi qisqartma.</summary>
    private static string ShortName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "(o'qituvchi)";
        }

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
        {
            return parts.Length == 1 ? parts[0] : fullName.Trim();
        }

        var initials = parts
            .Skip(1)
            .Take(2)
            .Select(p => char.ToUpper(p[0], CultureInfo.CurrentCulture) + ".");

        return parts[0] + " " + string.Join(" ", initials);
    }
}
