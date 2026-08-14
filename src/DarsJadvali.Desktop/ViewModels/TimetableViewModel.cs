using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Dars jadvali ekrani — ko'rish, qo'yish va o'chirish.</summary>
public sealed partial class TimetableViewModel : ViewModelBase
{
    private readonly IScheduleService _schedule;
    private readonly IWorkDayService _workDays;
    private readonly ITeacherService _teachers;
    private readonly ISubjectService _subjects;
    private readonly IClassGroupService _classGroups;
    private readonly ISchoolTimetablePdfExporter _pdfExporter;
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;

    private readonly List<WeekDay> _activeDays = new();
    private readonly Dictionary<int, string> _slotTimes = new();

    private int _maxLessonNumber = 7;
    private bool _isInitializing;

    [ObservableProperty]
    private bool _isClassMode = true;

    [ObservableProperty]
    private bool _isTeacherMode;

    [ObservableProperty]
    private ClassGroup? _filterClassGroup;

    [ObservableProperty]
    private Teacher? _filterTeacher;

    [ObservableProperty]
    private TimetableCellViewModel? _selectedCell;

    [ObservableProperty]
    private string _selectedCellText = "Katak tanlanmagan.";

    [ObservableProperty]
    private ClassGroup? _placeClassGroup;

    [ObservableProperty]
    private Subject? _placeSubject;

    [ObservableProperty]
    private Teacher? _placeTeacher;

    [ObservableProperty]
    private string _placeRoom = string.Empty;

    [ObservableProperty]
    private int _gridColumnCount = 1;

    [ObservableProperty]
    private string _placementSummary = string.Empty;

    [ObservableProperty]
    private bool _hasPlacementSummary;

    [ObservableProperty]
    private bool _hasPlacementResult;

    /// <summary>Yangi dars jadvali ViewModel'i yaratadi.</summary>
    public TimetableViewModel(
        IScheduleService schedule,
        IWorkDayService workDays,
        ITeacherService teachers,
        ISubjectService subjects,
        IClassGroupService classGroups,
        ISchoolTimetablePdfExporter pdfExporter,
        IDialogService dialogs,
        MainViewModel main)
    {
        _schedule = schedule;
        _workDays = workDays;
        _teachers = teachers;
        _subjects = subjects;
        _classGroups = classGroups;
        _pdfExporter = pdfExporter;
        _dialogs = dialogs;
        _main = main;
    }

    /// <summary>Jadval to'ridagi barcha kataklar (sarlavhalar bilan birga, qator-qator).</summary>
    public ObservableCollection<TimetableCellViewModel> Cells { get; } = new();

    /// <summary>Oxirgi joylashtirish urinishidagi konfliktlar.</summary>
    public ObservableCollection<ConflictRowViewModel> PlacementConflicts { get; } = new();

    /// <summary>Barcha sinflar.</summary>
    public ObservableCollection<ClassGroup> ClassGroups { get; } = new();

    /// <summary>Barcha fanlar.</summary>
    public ObservableCollection<Subject> Subjects { get; } = new();

    /// <summary>Barcha o'qituvchilar.</summary>
    public ObservableCollection<Teacher> Teachers { get; } = new();

    /// <inheritdoc />
    public override async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            _isInitializing = true;

            // Ma'lumot bir marta o'qiladi.
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
                _slotTimes[slot.LessonNumber] = ToTimeText(slot.StartTime) + " - " + ToTimeText(slot.EndTime);
            }

            // Sinf ro'yxati yangilangani uchun avvalgi tanlovni yangi obyektlarga bog'laymiz.
            FilterClassGroup = FindClassGroup(FilterClassGroup?.Id) ?? ClassGroups.FirstOrDefault();
            FilterTeacher = FindTeacher(FilterTeacher?.Id) ?? Teachers.FirstOrDefault();

            // Bosh sahifadan "Tahrirlash" bosilgan bo'lsa — o'sha sinfga o'tamiz.
            if (_main.PendingClassGroupId is int pendingId)
            {
                var pending = FindClassGroup(pendingId);

                if (pending is not null)
                {
                    IsTeacherMode = false;
                    IsClassMode = true;
                    FilterClassGroup = pending;
                }

                _main.PendingClassGroupId = null;
            }

            PlaceClassGroup = FilterClassGroup;
            PlaceTeacher = FilterTeacher;

            _isInitializing = false;

            await RefreshGridAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Jadvalni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }
    }

    private ClassGroup? FindClassGroup(int? id)
        => id is null ? null : ClassGroups.FirstOrDefault(c => c.Id == id.Value);

    private Teacher? FindTeacher(int? id)
        => id is null ? null : Teachers.FirstOrDefault(t => t.Id == id.Value);

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

    partial void OnPlacementSummaryChanged(string value)
        => HasPlacementSummary = !string.IsNullOrWhiteSpace(value);

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
            await _dialogs.ErrorAsync("Jadvalni yangilashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
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

                Cells.Add(cell);
            }
        }
    }

    /// <summary>Katak bosilganda chaqiriladi.</summary>
    public void SelectCell(TimetableCellViewModel cell)
    {
        if (cell is null || cell.IsHeader)
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

        var confirmed = await _dialogs.ConfirmAsync(
                $"{cell.Day.ToUzbek()}, {cell.LessonNumber}-dars ({cell.SubjectName}) o'chirilsinmi?",
                "Darsni o'chirish")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _schedule.RemoveAsync(cell.EntryId!.Value).ConfigureAwait(true);
            StatusMessage = "Dars o'chirildi.";
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Darsni o'chirishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshGridAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PlaceAsync()
    {
        if (SelectedCell is null || SelectedCell.IsHeader)
        {
            await _dialogs.InfoAsync("Avval jadvaldan bo'sh katakni tanlang.").ConfigureAwait(true);
            return;
        }

        if (PlaceClassGroup is null)
        {
            await _dialogs.ErrorAsync("Sinfni tanlang.").ConfigureAwait(true);
            return;
        }

        if (PlaceSubject is null)
        {
            await _dialogs.ErrorAsync("Fanni tanlang.").ConfigureAwait(true);
            return;
        }

        if (PlaceTeacher is null)
        {
            await _dialogs.ErrorAsync("O'qituvchini tanlang.").ConfigureAwait(true);
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
            ShowConflicts(result.Validation);

            if (result.Placed)
            {
                PlacementSummary = "Dars muvaffaqiyatli qo'yildi.";
                StatusMessage = PlacementSummary;
                await RefreshGridAsync().ConfigureAwait(true);
                return;
            }

            // Error darajali to'siq — qo'yilmaydi.
            if (!result.Validation.IsValid)
            {
                PlacementSummary = "Dars qo'yilmadi — to'siqlar mavjud.";
                await _dialogs.ShowValidationAsync(result.Validation).ConfigureAwait(true);
                return;
            }

            // Faqat ogohlantirish — foydalanuvchidan so'raymiz.
            if (result.Validation.HasWarnings)
            {
                var accepted = await _dialogs.ConfirmWarningsAsync(result.Validation).ConfigureAwait(true);

                if (!accepted)
                {
                    PlacementSummary = "Dars qo'yilmadi.";
                    return;
                }

                var forced = await _schedule.PlaceAsync(draft, true).ConfigureAwait(true);
                ShowConflicts(forced.Validation);

                if (forced.Placed)
                {
                    PlacementSummary = "Dars ogohlantirishga qaramay qo'yildi.";
                    StatusMessage = PlacementSummary;
                    await RefreshGridAsync().ConfigureAwait(true);
                }
                else
                {
                    PlacementSummary = "Dars qo'yilmadi.";
                    await _dialogs.ShowValidationAsync(forced.Validation).ConfigureAwait(true);
                }

                return;
            }

            PlacementSummary = "Dars qo'yilmadi.";
        }
        catch (Exception ex)
        {
            PlacementSummary = "Xatolik yuz berdi.";
            await _dialogs.ErrorAsync("Darsni qo'yishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowConflicts(ValidationResult validation)
    {
        PlacementConflicts.Clear();

        foreach (var conflict in validation.Conflicts)
        {
            PlacementConflicts.Add(new ConflictRowViewModel(conflict));
        }

        HasPlacementResult = true;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCell is null || !SelectedCell.HasEntry)
        {
            await _dialogs.InfoAsync("Avval darsi bor katakni tanlang.").ConfigureAwait(true);
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
            question = $"\"{FilterClassGroup.Name}\" sinfining butun jadvali o'chirilsinmi?" +
                       "\n\nBu amalni qaytarib bo'lmaydi.";
        }
        else
        {
            question = "Barcha sinflarning jadvali to'liq o'chirilsinmi?\n\nBu amalni qaytarib bo'lmaydi.";
        }

        var confirmed = await _dialogs.ConfirmAsync(question, "Jadvalni tozalash").ConfigureAwait(true);

        if (!confirmed)
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
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Jadvalni tozalashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshGridAsync().ConfigureAwait(true);
    }

    /// <summary>Joriy tanlovga mos jadvalni PDF ga yuklab oladi.</summary>
    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "PDF tayyorlanmoqda...";

            var options = new PdfExportOptions
            {
                ClassGroupId = IsClassMode ? FilterClassGroup?.Id : null,
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
}

/// <summary>Jadval to'rining bitta katagi (sarlavha yoki dars katagi).</summary>
public sealed partial class TimetableCellViewModel : ObservableObject
{
    private readonly TimetableViewModel _owner;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntry))]
    [NotifyPropertyChangedFor(nameof(Background))]
    private int? _entryId;

    [ObservableProperty]
    private string _subjectName = string.Empty;

    [ObservableProperty]
    private string _personName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoom))]
    [NotifyPropertyChangedFor(nameof(RoomDisplayText))]
    private string _roomText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background))]
    private string _colorCode = "#FFFFFF";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderBrush))]
    [NotifyPropertyChangedFor(nameof(BorderThickness))]
    private bool _isSelected;

    /// <summary>Katakni egasi bilan bog'lab yaratadi.</summary>
    public TimetableCellViewModel(TimetableViewModel owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>Sarlavha katagi (kun nomi, dars raqami yoki burchak).</summary>
    public bool IsHeader { get; init; }

    /// <summary>Dars katagimi (sarlavha emas).</summary>
    public bool IsLessonCell => !IsHeader;

    /// <summary>Sarlavha matni.</summary>
    public string HeaderText { get; init; } = string.Empty;

    /// <summary>Sarlavha ostidagi qo'shimcha matn (dars vaqti).</summary>
    public string HeaderSubText { get; init; } = string.Empty;

    /// <summary>Sarlavha ostida matn bormi.</summary>
    public bool HasHeaderSubText => !string.IsNullOrWhiteSpace(HeaderSubText);

    /// <summary>Katakka tegishli kun.</summary>
    public WeekDay Day { get; init; }

    /// <summary>Katakka tegishli dars raqami.</summary>
    public int LessonNumber { get; init; }

    /// <summary>Katakda dars bormi.</summary>
    public bool HasEntry => EntryId.HasValue;

    /// <summary>Xona ko'rsatilsinmi.</summary>
    public bool HasRoom => !string.IsNullOrWhiteSpace(RoomText);

    /// <summary>Xona matni ("Xona: 12").</summary>
    public string RoomDisplayText => HasRoom ? "Xona: " + RoomText : string.Empty;

    /// <summary>Katak foni (sarlavha — kulrang, dars — o'qituvchi rangining ochiq toni).</summary>
    public IBrush Background => IsHeader
        ? HeaderBackground
        : (HasEntry ? ScheduleColors.Light(ColorCode) : Brushes.White);

    /// <summary>Ramka rangi — tanlangan katakda ko'k.</summary>
    public IBrush BorderBrush => IsSelected ? ScheduleColors.Selection : ScheduleColors.CellBorder;

    /// <summary>Ramka qalinligi — tanlangan katakda qalinroq.</summary>
    public Thickness BorderThickness => IsSelected ? new Thickness(3) : new Thickness(1);

    private static readonly IBrush HeaderBackground = new ImmutableSolidColorBrush(Color.Parse("#EDE7F6"));

    /// <summary>Katakni tanlash.</summary>
    [RelayCommand]
    private void Select() => _owner.SelectCell(this);

    /// <summary>Katakdagi darsni o'chirish.</summary>
    [RelayCommand]
    private Task DeleteAsync() => _owner.DeleteEntryAsync(this);
}
