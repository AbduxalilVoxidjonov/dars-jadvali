using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.UI.Models;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Dars jadvali ekrani — ko'rish, qo'yish va o'chirish.</summary>
public sealed partial class TimetableViewModel : ViewModelBase
{
    private readonly IScheduleService _schedule;
    private readonly IWorkDayService _workDays;
    private readonly ITeacherService _teachers;
    private readonly ISubjectService _subjects;
    private readonly IClassGroupService _classGroups;
    private readonly IDialogService _dialogs;

    private readonly List<WeekDay> _activeDays = new();
    private readonly Dictionary<int, string> _slotTimes = new();

    private int _maxLessonNumber = 7;
    private bool _isInitializing;

    [ObservableProperty]
    private bool isClassMode = true;

    [ObservableProperty]
    private bool isTeacherMode;

    [ObservableProperty]
    private ClassGroup? filterClassGroup;

    [ObservableProperty]
    private Teacher? filterTeacher;

    [ObservableProperty]
    private TimetableCellViewModel? selectedCell;

    [ObservableProperty]
    private string selectedCellText = "Katak tanlanmagan.";

    [ObservableProperty]
    private ClassGroup? placeClassGroup;

    [ObservableProperty]
    private Subject? placeSubject;

    [ObservableProperty]
    private Teacher? placeTeacher;

    [ObservableProperty]
    private string placeRoom = string.Empty;

    [ObservableProperty]
    private int gridColumnCount = 1;

    [ObservableProperty]
    private string placementSummary = string.Empty;

    [ObservableProperty]
    private bool hasPlacementResult;

    public TimetableViewModel(
        IScheduleService schedule,
        IWorkDayService workDays,
        ITeacherService teachers,
        ISubjectService subjects,
        IClassGroupService classGroups,
        IDialogService dialogs)
    {
        _schedule = schedule;
        _workDays = workDays;
        _teachers = teachers;
        _subjects = subjects;
        _classGroups = classGroups;
        _dialogs = dialogs;
    }

    /// <summary>Jadval to'ridagi barcha kataklar (sarlavhalar bilan birga, qator-qator).</summary>
    public ObservableCollection<TimetableCellViewModel> Cells { get; } = new();

    /// <summary>Oxirgi joylashtirish urinishidagi konfliktlar.</summary>
    public ObservableCollection<Conflict> PlacementConflicts { get; } = new();

    public ObservableCollection<ClassGroup> ClassGroups { get; } = new();

    public ObservableCollection<Subject> Subjects { get; } = new();

    public ObservableCollection<Teacher> Teachers { get; } = new();

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            _isInitializing = true;

            var classGroups = await _classGroups.GetAllAsync(ct).ConfigureAwait(true);
            var subjects = await _subjects.GetAllAsync(ct).ConfigureAwait(true);
            var teachers = await _teachers.GetAllAsync(ct).ConfigureAwait(true);
            var activeDays = await _workDays.GetActiveAsync(ct).ConfigureAwait(true);
            var maxLesson = await _workDays.GetMaxLessonNumberAsync(ct).ConfigureAwait(true);
            var slots = await _workDays.GetLessonSlotsAsync(ct).ConfigureAwait(true);

            ClassGroups.Clear();
            foreach (var item in classGroups.OrderBy(c => c.Name, StringComparer.CurrentCulture))
            {
                ClassGroups.Add(item);
            }

            Subjects.Clear();
            foreach (var item in subjects.OrderBy(s => s.Name, StringComparer.CurrentCulture))
            {
                Subjects.Add(item);
            }

            Teachers.Clear();
            foreach (var item in teachers.OrderBy(t => t.FullName, StringComparer.CurrentCulture))
            {
                Teachers.Add(item);
            }

            _activeDays.Clear();
            _activeDays.AddRange(activeDays.OrderBy(w => w.DayOfWeek).Select(w => w.DayOfWeek));

            _maxLessonNumber = maxLesson > 0 ? maxLesson : 7;

            _slotTimes.Clear();
            foreach (var slot in slots)
            {
                _slotTimes[slot.LessonNumber] =
                    TimeTextHelper.ToText(slot.StartTime) + " - " + TimeTextHelper.ToText(slot.EndTime);
            }

            FilterClassGroup ??= ClassGroups.FirstOrDefault();
            FilterTeacher ??= Teachers.FirstOrDefault();
            PlaceClassGroup ??= FilterClassGroup;
            PlaceTeacher ??= FilterTeacher;

            _isInitializing = false;

            await RefreshGridAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error("Jadvalni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }
    }

    partial void OnIsClassModeChanged(bool value)
    {
        if (!value || _isInitializing)
        {
            return;
        }

        IsTeacherMode = false;
        _ = RefreshGridAsync();
    }

    partial void OnIsTeacherModeChanged(bool value)
    {
        if (!value || _isInitializing)
        {
            return;
        }

        IsClassMode = false;
        _ = RefreshGridAsync();
    }

    partial void OnFilterClassGroupChanged(ClassGroup? value)
    {
        if (_isInitializing || !IsClassMode)
        {
            return;
        }

        PlaceClassGroup = value;
        _ = RefreshGridAsync();
    }

    partial void OnFilterTeacherChanged(Teacher? value)
    {
        if (_isInitializing || !IsTeacherMode)
        {
            return;
        }

        PlaceTeacher = value;
        _ = RefreshGridAsync();
    }

    [RelayCommand]
    private async Task RefreshGridAsync()
    {
        try
        {
            IsBusy = true;

            IReadOnlyList<ScheduleEntry> entries;

            if (IsTeacherMode)
            {
                entries = FilterTeacher is null
                    ? Array.Empty<ScheduleEntry>()
                    : await _schedule.GetByTeacherAsync(FilterTeacher.Id).ConfigureAwait(true);
            }
            else
            {
                entries = FilterClassGroup is null
                    ? Array.Empty<ScheduleEntry>()
                    : await _schedule.GetByClassGroupAsync(FilterClassGroup.Id).ConfigureAwait(true);
            }

            BuildGrid(entries);

            StatusMessage = IsTeacherMode
                ? $"{FilterTeacher?.FullName ?? "O'qituvchi tanlanmagan"} — {entries.Count} ta dars."
                : $"{FilterClassGroup?.Name ?? "Sinf tanlanmagan"} — {entries.Count} ta dars.";
        }
        catch (Exception ex)
        {
            _dialogs.Error("Jadvalni yangilashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildGrid(IReadOnlyList<ScheduleEntry> entries)
    {
        SelectedCell = null;
        SelectedCellText = "Katak tanlanmagan.";
        Cells.Clear();

        var days = _activeDays.Count > 0 ? _activeDays : WeekDayExtensions.All.Take(6).ToList();
        GridColumnCount = days.Count + 1;

        var lookup = new Dictionary<(WeekDay Day, int Lesson), ScheduleEntry>();
        foreach (var entry in entries)
        {
            lookup[(entry.DayOfWeek, entry.LessonNumber)] = entry;
        }

        // Birinchi qator — kun nomlari
        Cells.Add(new TimetableCellViewModel(this) { IsHeader = true, HeaderText = "Dars" });
        foreach (var day in days)
        {
            Cells.Add(new TimetableCellViewModel(this) { IsHeader = true, HeaderText = day.ToUzbek() });
        }

        // Qolgan qatorlar
        for (var lesson = 1; lesson <= _maxLessonNumber; lesson++)
        {
            _slotTimes.TryGetValue(lesson, out var time);

            Cells.Add(new TimetableCellViewModel(this)
            {
                IsHeader = true,
                HeaderText = lesson + "-dars",
                HeaderSubText = time ?? string.Empty,
            });

            foreach (var day in days)
            {
                var cell = new TimetableCellViewModel(this)
                {
                    Day = day,
                    LessonNumber = lesson,
                };

                if (lookup.TryGetValue((day, lesson), out var entry))
                {
                    cell.EntryId = entry.Id;
                    cell.SubjectName = entry.Subject?.Name ?? "(fan)";
                    cell.PersonName = IsTeacherMode
                        ? entry.ClassGroup?.Name ?? "(sinf)"
                        : entry.Teacher?.FullName ?? "(o'qituvchi)";
                    cell.RoomText = string.IsNullOrWhiteSpace(entry.RoomNumber)
                        ? string.Empty
                        : entry.RoomNumber!;
                    cell.ColorCode = entry.Teacher?.ColorCode ?? "#90A4AE";
                }
                else
                {
                    cell.ColorCode = "#FFFFFF";
                }

                Cells.Add(cell);
            }
        }
    }

    /// <summary>Katak bosilganda chaqiriladi.</summary>
    public void SelectCell(TimetableCellViewModel cell)
    {
        if (cell.IsHeader)
        {
            return;
        }

        if (SelectedCell is not null)
        {
            SelectedCell.IsSelected = false;
        }

        cell.IsSelected = true;
        SelectedCell = cell;

        _slotTimes.TryGetValue(cell.LessonNumber, out var time);
        SelectedCellText = $"{cell.Day.ToUzbek()}, {cell.LessonNumber}-dars" +
                           (string.IsNullOrEmpty(time) ? string.Empty : $" ({time})");

        // Rejimga qarab tanlovni oldindan to'ldiramiz
        if (IsClassMode && FilterClassGroup is not null)
        {
            PlaceClassGroup = FilterClassGroup;
        }

        if (IsTeacherMode && FilterTeacher is not null)
        {
            PlaceTeacher = FilterTeacher;
        }
    }

    /// <summary>Katakdagi darsni o'chiradi.</summary>
    public async Task DeleteEntryAsync(TimetableCellViewModel cell)
    {
        if (cell is null || !cell.HasEntry)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"{cell.Day.ToUzbek()}, {cell.LessonNumber}-dars ({cell.SubjectName}) o'chirilsinmi?",
                "Darsni o'chirish"))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _schedule.RemoveAsync(cell.EntryId!.Value).ConfigureAwait(true);
            StatusMessage = "Dars o'chirildi.";
            await RefreshGridAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _dialogs.Error("Darsni o'chirishda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PlaceAsync()
    {
        if (SelectedCell is null || SelectedCell.IsHeader)
        {
            _dialogs.Info("Avval jadvaldan bo'sh katakni tanlang.");
            return;
        }

        if (PlaceClassGroup is null)
        {
            _dialogs.Error("Sinfni tanlang.");
            return;
        }

        if (PlaceSubject is null)
        {
            _dialogs.Error("Fanni tanlang.");
            return;
        }

        if (PlaceTeacher is null)
        {
            _dialogs.Error("O'qituvchini tanlang.");
            return;
        }

        var room = string.IsNullOrWhiteSpace(PlaceRoom) ? null : PlaceRoom.Trim();

        var draft = new ScheduleEntryDraft(
            null,
            PlaceClassGroup.Id,
            PlaceSubject.Id,
            PlaceTeacher.Id,
            SelectedCell.Day,
            SelectedCell.LessonNumber,
            room);

        try
        {
            IsBusy = true;

            var result = await _schedule.PlaceAsync(draft, false).ConfigureAwait(true);
            ShowValidation(result.Validation);

            if (result.Placed)
            {
                PlacementSummary = "Dars muvaffaqiyatli qo'yildi.";
                StatusMessage = PlacementSummary;
                await RefreshGridAsync().ConfigureAwait(true);
                return;
            }

            if (!result.Validation.IsValid)
            {
                PlacementSummary = "Dars qo'yilmadi — to'siqlar mavjud.";
                _dialogs.ShowValidation(result.Validation, "Darsni qo'yib bo'lmadi");
                return;
            }

            if (result.Validation.HasWarnings)
            {
                var question = "Quyidagi ogohlantirishlar mavjud:\n\n" +
                               result.Validation.ToDisplayText() +
                               "\n\nBaribir qo'yilsinmi?";

                if (!_dialogs.Confirm(question, "Ogohlantirish"))
                {
                    PlacementSummary = "Dars qo'yilmadi.";
                    return;
                }

                var forced = await _schedule.PlaceAsync(draft, true).ConfigureAwait(true);
                ShowValidation(forced.Validation);

                if (forced.Placed)
                {
                    PlacementSummary = "Dars ogohlantirishga qaramay qo'yildi.";
                    StatusMessage = PlacementSummary;
                    await RefreshGridAsync().ConfigureAwait(true);
                }
                else
                {
                    PlacementSummary = "Dars qo'yilmadi.";
                    _dialogs.ShowValidation(forced.Validation, "Darsni qo'yib bo'lmadi");
                }

                return;
            }

            PlacementSummary = "Dars qo'yilmadi.";
        }
        catch (Exception ex)
        {
            PlacementSummary = "Xatolik yuz berdi.";
            _dialogs.Error("Darsni qo'yishda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowValidation(ValidationResult validation)
    {
        PlacementConflicts.Clear();

        foreach (var conflict in validation.Conflicts)
        {
            PlacementConflicts.Add(conflict);
        }

        HasPlacementResult = true;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCell is null || !SelectedCell.HasEntry)
        {
            _dialogs.Info("Avval darsi bor katakni tanlang.");
            return;
        }

        await DeleteEntryAsync(SelectedCell).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ClearScheduleAsync()
    {
        int? classGroupId = null;
        string question;

        if (IsClassMode && FilterClassGroup is not null)
        {
            classGroupId = FilterClassGroup.Id;
            question = $"\"{FilterClassGroup.Name}\" sinfining butun jadvali o'chirilsinmi?\n\nBu amalni qaytarib bo'lmaydi.";
        }
        else
        {
            question = "Barcha sinflarning jadvali to'liq o'chirilsinmi?\n\nBu amalni qaytarib bo'lmaydi.";
        }

        if (!_dialogs.Confirm(question, "Jadvalni tozalash"))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _schedule.ClearAsync(classGroupId).ConfigureAwait(true);
            StatusMessage = "Jadval tozalandi.";
            PlacementConflicts.Clear();
            PlacementSummary = string.Empty;
            HasPlacementResult = false;
            await RefreshGridAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _dialogs.Error("Jadvalni tozalashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
