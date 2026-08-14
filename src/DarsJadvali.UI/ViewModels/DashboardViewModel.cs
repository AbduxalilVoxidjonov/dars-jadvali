using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.UI.Models;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Bosh sahifa: umumiy ko'rsatkichlar, maktab jadvali, avtomatik tuzish va tekshiruv.</summary>
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
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly MainViewModel _main;

    private readonly List<ClassTimetableViewModel> _allTimetables = new();

    private CancellationTokenSource? _generationCts;
    private bool _isRefreshingTimetables;

    [ObservableProperty]
    private int teacherCount;

    [ObservableProperty]
    private int subjectCount;

    [ObservableProperty]
    private int classGroupCount;

    [ObservableProperty]
    private int placedLessonCount;

    [ObservableProperty]
    private int assignmentCount;

    [ObservableProperty]
    private int weeklyHoursTotal;

    [ObservableProperty]
    private bool isGenerating;

    [ObservableProperty]
    private double generationProgressValue;

    [ObservableProperty]
    private double generationProgressMax = 100;

    [ObservableProperty]
    private string generationMessage = string.Empty;

    [ObservableProperty]
    private string validationSummary = string.Empty;

    [ObservableProperty]
    private bool hasValidationResult;

    [ObservableProperty]
    private ClassFilterOption? selectedClassFilter;

    [ObservableProperty]
    private bool isTimetableEmpty = true;

    [ObservableProperty]
    private string timetableEmptyMessage = NoLessonsMessage;

    [ObservableProperty]
    private int dayCount = 1;

    public DashboardViewModel(
        ITeacherService teachers,
        ISubjectService subjects,
        IClassGroupService classGroups,
        IAssignmentService assignments,
        IScheduleService schedule,
        IWorkDayService workDays,
        IScheduleValidator validator,
        IScheduleGenerator generator,
        IDialogService dialogs,
        INavigationService navigation,
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
        _dialogs = dialogs;
        _navigation = navigation;
        _main = main;
    }

    /// <summary>Tekshiruvda topilgan konfliktlar.</summary>
    public ObservableCollection<Conflict> ValidationConflicts { get; } = new();

    /// <summary>Sinf filtri bandlari ("Barcha sinflar" + har bir sinf).</summary>
    public ObservableCollection<ClassFilterOption> ClassFilters { get; } = new();

    /// <summary>Ekranda ko'rsatilayotgan sinf guruhlari.</summary>
    public ObservableCollection<ClassTimetableViewModel> ClassTimetables { get; } = new();

    /// <summary>Jadval sarlavhasidagi faol kun nomlari.</summary>
    public ObservableCollection<string> DayHeaders { get; } = new();

    /// <summary>Generator nomi.</summary>
    public string GeneratorName => _generator.Name;

    /// <summary>Generator tavsifi.</summary>
    public string GeneratorDescription => _generator.Description;

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Ma'lumotlar yuklanmoqda...";

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
            _dialogs.Error("Ma'lumotlarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
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
            slotTexts[slot.LessonNumber] =
                TimeTextHelper.ToText(slot.StartTime) + "-" + TimeTextHelper.ToText(slot.EndTime);
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

        DayHeaders.Clear();
        foreach (var day in days)
        {
            DayHeaders.Add(day.ToUzbek());
        }

        DayCount = days.Count;

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
                    DayCount = days.Count,
                };

                foreach (var day in days)
                {
                    ScheduleEntry? entry = null;

                    if (lookup is not null)
                    {
                        lookup.TryGetValue((day, lesson), out entry);
                    }

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

        _isRefreshingTimetables = true;

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
            _isRefreshingTimetables = false;
        }
    }

    /// <summary>Tanlangan filtrga mos sinf guruhlarini ekranga chiqaradi.</summary>
    private void ApplyClassFilter()
    {
        ClassTimetables.Clear();

        var filterId = SelectedClassFilter?.Id ?? 0;

        foreach (var block in _allTimetables)
        {
            if (filterId != 0 && block.ClassGroupId != filterId)
            {
                continue;
            }

            block.IsAlternate = ClassTimetables.Count % 2 == 1;
            ClassTimetables.Add(block);
        }
    }

    partial void OnSelectedClassFilterChanged(ClassFilterOption? value)
    {
        if (_isRefreshingTimetables)
        {
            return;
        }

        ApplyClassFilter();
    }

    /// <summary>"Dars jadvali" sahifasiga o'tib, shu sinfni tanlab beradi.</summary>
    [RelayCommand]
    private async Task EditClassAsync(ClassTimetableViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var menuItem = _main.MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(TimetableViewModel));

            if (menuItem is null)
            {
                _dialogs.Info("«Dars jadvali» sahifasi topilmadi.");
                return;
            }

            // Chap menyu ham belgilanadi va standart navigatsiya ishga tushadi.
            _main.SelectedMenuItem = menuItem;

            if (_navigation.Current is not TimetableViewModel timetable)
            {
                return;
            }

            // Sahifa ma'lumotlarini yuklab bo'lgunicha kutamiz, so'ng sinfni tanlaymiz.
            await WaitUntilIdleAsync(timetable).ConfigureAwait(true);

            var target = timetable.ClassGroups.FirstOrDefault(c => c.Id == item.ClassGroupId);

            if (target is null)
            {
                return;
            }

            timetable.IsClassMode = true;

            if (!ReferenceEquals(timetable.FilterClassGroup, target))
            {
                timetable.FilterClassGroup = target;
            }
        }
        catch (Exception ex)
        {
            _dialogs.Error("«Dars jadvali» sahifasini ochishda xatolik yuz berdi.\n\n" + ex.Message);
        }
    }

    /// <summary>ViewModel yuklashni tugatishini kutadi (eng ko'pi bilan 10 soniya).</summary>
    private static async Task WaitUntilIdleAsync(ViewModelBase viewModel)
    {
        if (!viewModel.IsBusy)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName is null or nameof(ViewModelBase.IsBusy)) && !viewModel.IsBusy)
            {
                completion.TrySetResult();
            }
        }

        viewModel.PropertyChanged += Handler;

        try
        {
            if (!viewModel.IsBusy)
            {
                completion.TrySetResult();
            }

            await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
        }
        finally
        {
            viewModel.PropertyChanged -= Handler;
        }
    }

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
            .Select(p => char.ToUpper(p[0], System.Globalization.CultureInfo.CurrentCulture) + ".");

        return parts[0] + " " + string.Join(" ", initials);
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (!_dialogs.Confirm(
                "Jadval avtomatik tuziladi. Mavjud jadval o'chirilib, qaytadan tuziladi.\n\nDavom etilsinmi?",
                "Jadvalni avtomatik tuzish"))
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

            _dialogs.Info(text, "Jadval tuzish natijasi");
        }
        catch (OperationCanceledException)
        {
            GenerationMessage = "Jadval tuzish bekor qilindi.";
            StatusMessage = "Bekor qilindi.";
        }
        catch (Exception ex)
        {
            GenerationMessage = "Xatolik yuz berdi.";
            _dialogs.Error("Jadvalni tuzishda xatolik yuz berdi.\n\n" + ex.Message);
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
                ValidationConflicts.Add(conflict);
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
                ValidationSummary = $"Jami {result.Conflicts.Count} ta muammo: {errors} ta xato, {warnings} ta ogohlantirish.";
            }

            StatusMessage = "Tekshiruv yakunlandi.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bekor qilindi.";
        }
        catch (Exception ex)
        {
            _dialogs.Error("Tekshirishda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
