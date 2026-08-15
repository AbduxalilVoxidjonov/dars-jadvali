using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Export;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Dars jadvali ekrani — ko'rish, qo'yish va o'chirish.</summary>
/// <remarks>
/// <para>
/// <b>Yagona manba.</b> Bu sahifa ham bosh sahifadagi jadval yadrosi ham AYNI
/// <c>Card</c>/<c>Lesson</c> modeli ustida ishlaydi. Ilgari bu ekran eski
/// <c>ScheduleEntry</c> ga yozardi va natijada ikkita ekran ikki xil jadvalni
/// ko'rsatib qolishi mumkin edi.
/// </para>
/// <para>
/// Baholash <see cref="TimetableBoard"/> orqali xotirada bajariladi (qoidalar
/// <c>ScheduleSnapshot</c> dan), yozish esa <see cref="TimetableBoardWriter"/>
/// orqali bitta tranzaksiyada ketadi.
/// </para>
/// </remarks>
public sealed partial class TimetableViewModel : ViewModelBase
{
    private readonly ICardBoardService _cards;
    private readonly IScheduleSnapshotProvider _snapshots;
    private readonly ISchedulingStore _store;
    private readonly IScheduleSetService _schedules;
    private readonly ITeacherService _teachers;
    private readonly ISubjectService _subjects;
    private readonly IAvailabilityService _availability;
    private readonly IScopedTimetablePdfExporter _pdfExporter;
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;

    private readonly TimetableBoard _board = new();
    private readonly TimetableBoardWriter _writer;

    private readonly List<WeekDay> _activeDays = new();
    private readonly Dictionary<int, string> _slotTimes = new();
    private readonly Dictionary<int, int> _periodIdByNumber = new();

    private IReadOnlyList<int> _periodNumbers = Array.Empty<int>();
    private int _scheduleId;
    private bool _isInitializing;

    [ObservableProperty]
    private bool _isClassMode = true;

    [ObservableProperty]
    private bool _isTeacherMode;

    [ObservableProperty]
    private SchoolClass? _filterClassGroup;

    [ObservableProperty]
    private Teacher? _filterTeacher;

    [ObservableProperty]
    private TimetableCellViewModel? _selectedCell;

    [ObservableProperty]
    private string _selectedCellText = "Katak tanlanmagan.";

    [ObservableProperty]
    private SchoolClass? _placeClassGroup;

    /// <summary>Qo'yiladigan dars — joylashtirilmagan (rejada bor) darslar ro'yxatidan.</summary>
    [ObservableProperty]
    private UnplacedLessonOption? _placeLesson;

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
        ICardBoardService cards,
        IScheduleSnapshotProvider snapshots,
        ISchedulingStore store,
        IScheduleSetService schedules,
        ITeacherService teachers,
        ISubjectService subjects,
        IAvailabilityService availability,
        IScopedTimetablePdfExporter pdfExporter,
        IDialogService dialogs,
        MainViewModel main)
    {
        _cards = cards;
        _snapshots = snapshots;
        _store = store;
        _schedules = schedules;
        _teachers = teachers;
        _subjects = subjects;
        _availability = availability;
        _pdfExporter = pdfExporter;
        _dialogs = dialogs;
        _main = main;

        _writer = new TimetableBoardWriter(cards);
    }

    /// <summary>Jadval to'ridagi barcha kataklar (sarlavhalar bilan birga, qator-qator).</summary>
    public ObservableCollection<TimetableCellViewModel> Cells { get; } = new();

    /// <summary>Oxirgi joylashtirish urinishidagi konfliktlar.</summary>
    public ObservableCollection<ConflictRowViewModel> PlacementConflicts { get; } = new();

    /// <summary>Barcha sinflar.</summary>
    public ObservableCollection<SchoolClass> ClassGroups { get; } = new();

    /// <summary>Barcha o'qituvchilar.</summary>
    public ObservableCollection<Teacher> Teachers { get; } = new();

    /// <summary>Tanlangan sinf uchun joylashtirilmagan darslar.</summary>
    public ObservableCollection<UnplacedLessonOption> PlaceLessons { get; } = new();

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken ct = default)
        => RunExclusiveAsync(LoadCoreAsync, ct);

    private async Task LoadCoreAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            _isInitializing = true;

            _scheduleId = await _schedules.GetActiveIdAsync(ct).ConfigureAwait(true);

            // Ma'lumot bir marta o'qiladi.
            var teachers = await _teachers.GetAllAsync(ct).ConfigureAwait(true);
            var subjects = await _subjects.GetAllAsync(ct).ConfigureAwait(true);
            var snapshot = await _snapshots.LoadAsync(_scheduleId, ct).ConfigureAwait(true);
            var input = await _store.LoadAsync(_scheduleId, ct).ConfigureAwait(true);
            var cardViews = await _cards.GetCardsAsync(_scheduleId, ct).ConfigureAwait(true);
            var unplaced = await _cards.GetUnplacedAsync(_scheduleId, ct).ConfigureAwait(true);
            var availability = await _availability.GetLessonAvailabilityForAllAsync(ct).ConfigureAwait(true);

            _slotTimes.Clear();
            _periodIdByNumber.Clear();

            foreach (var period in input.Periods.Where(p => !p.IsBreak).OrderBy(p => p.PeriodNo))
            {
                _periodIdByNumber[period.PeriodNo] = period.Id;
                _slotTimes[period.PeriodNo] = ToTimeText(period.StartTime) + " - " + ToTimeText(period.EndTime);
            }

            _periodNumbers = _periodIdByNumber.Keys.OrderBy(n => n).ToList();

            var classes = input.Classes.Where(c => !c.IsDeleted).ToList();

            ClassGroups.Clear();
            foreach (var item in classes.OrderBy(c => c.Name, StringComparer.CurrentCulture))
            {
                ClassGroups.Add(item);
            }

            Teachers.Clear();
            foreach (var item in teachers.OrderBy(t => t.FullName, StringComparer.CurrentCulture))
            {
                Teachers.Add(item);
            }

            _activeDays.Clear();
            _activeDays.AddRange(snapshot.ActiveWorkDays.Select(w => w.DayOfWeek));

            var blocked = ToBlockedSlots(availability, snapshot);
            var rules = CardViewAdapter.ToRuleSet(snapshot, _periodNumbers, blocked);

            var classIdByName = new Dictionary<string, int>(StringComparer.CurrentCulture);
            foreach (var item in classes)
            {
                classIdByName[item.Name] = item.Id;
            }

            var placed = CardViewAdapter.ToCards(cardViews, teachers, subjects);
            var pending = CardViewAdapter.ToUnplacedCards(
                unplaced,
                teachers,
                subjects,
                cardViews.Count == 0 ? 1_000_000 : Math.Max(1_000_000, cardViews.Max(v => v.CardId) + 1),
                classIdByName);

            _board.Load(placed.Concat(pending), rules);
            _board.ClearDirty();

            RebuildPlaceLessons(unplaced, classIdByName);

            // Sinf ro'yxati yangilangani uchun avvalgi tanlovni yangi obyektlarga bog'laymiz.
            FilterClassGroup = FindClassGroup(FilterClassGroup?.Id) ?? ClassGroups.FirstOrDefault();
            FilterTeacher = FindTeacher(FilterTeacher?.Id) ?? Teachers.FirstOrDefault();

            // Bosh sahifadan "Tahrirlash" bosilgan bo'lsa — o'sha sinfga o'tamiz.
            if (_main.PendingClassGroupId is int pendingId)
            {
                var pendingClass = FindClassGroup(pendingId) ??
                                   ClassGroups.FirstOrDefault(c => c.LegacyClassGroupId == pendingId);

                if (pendingClass is not null)
                {
                    IsTeacherMode = false;
                    IsClassMode = true;
                    FilterClassGroup = pendingClass;
                }

                _main.PendingClassGroupId = null;
            }

            PlaceClassGroup = FilterClassGroup;

            _isInitializing = false;

            BuildGrid();
            RefreshPlaceOptions();

            StatusMessage = IsTeacherMode
                ? $"{FilterTeacher?.FullName ?? "O'qituvchi tanlanmagan"} jadvali."
                : $"{FilterClassGroup?.Name ?? "Sinf tanlanmagan"} jadvali.";
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

    /// <summary>
    /// Ommaviy bandlik natijasini (o'qituvchi, kun, soat) uchliklariga aylantiradi.
    /// </summary>
    private List<(int TeacherId, WeekDay Day, int Period)> ToBlockedSlots(
        IReadOnlyDictionary<int, IReadOnlyList<TeacherDayAvailability>> availability,
        ScheduleSnapshot snapshot)
    {
        var result = new List<(int, WeekDay, int)>();

        foreach (var (teacherId, days) in availability)
        {
            foreach (var day in days)
            {
                if (!day.HasRestriction)
                {
                    continue;
                }

                var allowed = new HashSet<int>(day.AllowedLessonNumbers);
                var max = snapshot.MaxLessonNumberOf(day.Day);

                foreach (var period in _periodNumbers)
                {
                    if (max > 0 && period > max)
                    {
                        continue;
                    }

                    if (!allowed.Contains(period))
                    {
                        result.Add((teacherId, day.Day, period));
                    }
                }
            }
        }

        return result;
    }

    private void RebuildPlaceLessons(
        IReadOnlyList<UnplacedLessonView> unplaced, IReadOnlyDictionary<string, int> classIdByName)
    {
        PlaceLessons.Clear();

        foreach (var lesson in unplaced.Where(l => l.RemainingPeriods > 0))
        {
            classIdByName.TryGetValue(lesson.ClassName, out var classId);
            PlaceLessons.Add(new UnplacedLessonOption(lesson, classId));
        }
    }

    private SchoolClass? FindClassGroup(int? id)
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
        BuildGrid();
    }

    partial void OnIsTeacherModeChanged(bool value)
    {
        if (!value || _isInitializing)
        {
            return;
        }

        IsClassMode = false;
        BuildGrid();
    }

    partial void OnFilterClassGroupChanged(SchoolClass? value)
    {
        if (_isInitializing || !IsClassMode)
        {
            return;
        }

        PlaceClassGroup = value;
        BuildGrid();
    }

    partial void OnFilterTeacherChanged(Teacher? value)
    {
        if (_isInitializing || !IsTeacherMode)
        {
            return;
        }

        BuildGrid();
    }

    partial void OnPlaceClassGroupChanged(SchoolClass? value)
    {
        if (_isInitializing)
        {
            return;
        }

        RefreshPlaceOptions();
    }

    partial void OnPlacementSummaryChanged(string value)
        => HasPlacementSummary = !string.IsNullOrWhiteSpace(value);

    /// <summary>Qo'yish paneli uchun tanlangan sinfning joylashtirilmagan darslarini ko'rsatadi.</summary>
    private void RefreshPlaceOptions()
    {
        foreach (var option in PlaceLessons)
        {
            option.IsVisibleForClass = PlaceClassGroup is null || option.ClassId == PlaceClassGroup.Id;
        }

        if (PlaceLesson is not null && !PlaceLesson.IsVisibleForClass)
        {
            PlaceLesson = null;
        }

        OnPropertyChanged(nameof(VisiblePlaceLessons));
    }

    /// <summary>Tanlangan sinfga tegishli joylashtirilmagan darslar.</summary>
    public IEnumerable<UnplacedLessonOption> VisiblePlaceLessons
        => PlaceLessons.Where(o => o.IsVisibleForClass);

    /// <summary>To'rni bazadan qayta o'qiydi (navbat orqali).</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task RefreshGridAsync(CancellationToken ct = default)
        => RunExclusiveAsync(LoadCoreAsync, ct);

    private void BuildGrid()
    {
        SelectedCell = null;
        SelectedCellText = "Katak tanlanmagan.";
        Cells.Clear();

        var days = _activeDays.Count > 0 ? _activeDays : WeekDayExtensions.All.Take(6).ToList();
        var periods = _periodNumbers.Count > 0 ? _periodNumbers : Enumerable.Range(1, 7).ToList();

        GridColumnCount = days.Count + 1;

        var lookup = new Dictionary<(WeekDay Day, int Period), TimetableCard>();

        foreach (var card in _board.Cards.Where(c => c.IsPlaced))
        {
            if (!Matches(card))
            {
                continue;
            }

            foreach (var period in card.OccupiedPeriods)
            {
                lookup[(card.Day!.Value, period)] = card;
            }
        }

        // Birinchi qator — kun nomlari
        Cells.Add(new TimetableCellViewModel(this) { IsHeader = true, HeaderText = "Dars" });
        foreach (var day in days)
        {
            Cells.Add(new TimetableCellViewModel(this) { IsHeader = true, HeaderText = day.ToUzbek() });
        }

        // Qolgan qatorlar
        foreach (var period in periods)
        {
            _slotTimes.TryGetValue(period, out var time);

            Cells.Add(new TimetableCellViewModel(this)
            {
                IsHeader = true,
                HeaderText = period + "-dars",
                HeaderSubText = time ?? string.Empty,
            });

            foreach (var day in days)
            {
                var cell = new TimetableCellViewModel(this)
                {
                    Day = day,
                    LessonNumber = period,
                };

                if (lookup.TryGetValue((day, period), out var card))
                {
                    cell.CardId = card.Id;
                    cell.SubjectName = card.SubjectName;
                    cell.PersonName = IsTeacherMode ? card.ScopeText : card.TeacherText;
                    cell.RoomText = card.RoomNumber ?? string.Empty;
                    cell.ColorCode = card.ColorCode;
                }

                Cells.Add(cell);
            }
        }

        StatusMessage = IsTeacherMode
            ? $"{FilterTeacher?.FullName ?? "O'qituvchi tanlanmagan"} — {lookup.Values.Distinct().Count()} ta dars."
            : $"{FilterClassGroup?.Name ?? "Sinf tanlanmagan"} — {lookup.Values.Distinct().Count()} ta dars.";
    }

    /// <summary>Karta joriy filtrga tushadimi.</summary>
    private bool Matches(TimetableCard card)
    {
        if (IsTeacherMode)
        {
            return FilterTeacher is not null && card.TeacherIds.Contains(FilterTeacher.Id);
        }

        if (FilterClassGroup is null)
        {
            return false;
        }

        return card.ClassIds.Count > 0
            ? card.ClassIds.Contains(FilterClassGroup.Id)
            : card.ClassGroupId == FilterClassGroup.Id;
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

        if (IsClassMode && FilterClassGroup is not null)
        {
            PlaceClassGroup = FilterClassGroup;
        }
    }

    /// <summary>Katakdagi darsni o'chiradi.</summary>
    public Task DeleteEntryAsync(TimetableCellViewModel cell, CancellationToken ct = default)
    {
        if (cell is null || !cell.HasEntry)
        {
            return Task.CompletedTask;
        }

        return RunExclusiveAsync(token => DeleteEntryCoreAsync(cell, token), ct);
    }

    private async Task DeleteEntryCoreAsync(TimetableCellViewModel cell, CancellationToken ct)
    {
        var card = cell.CardId is { } id ? _board.FindById(id) : null;

        if (card is null)
        {
            return;
        }

        if (card.IsLocked)
        {
            await _dialogs.InfoAsync("Bu dars qulflangan — avval bosh sahifadagi jadvaldan qulfni oching.")
                .ConfigureAwait(true);
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

            // Kartani joylashtirilmaganlar ro'yxatiga qaytaramiz — bu bazada kartochkani o'chiradi.
            _board.MoveCard(card, null);
            await PersistAsync(ct).ConfigureAwait(true);

            StatusMessage = "Dars o'chirildi.";
        }
        catch (OperationCanceledException)
        {
            return;
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

        await LoadCoreAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Tanlangan katakka dars qo'yadi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task PlaceAsync(CancellationToken ct = default)
        => RunExclusiveAsync(PlaceCoreAsync, ct);

    private async Task PlaceCoreAsync(CancellationToken ct)
    {
        if (SelectedCell is null || SelectedCell.IsHeader)
        {
            await _dialogs.InfoAsync("Avval jadvaldan bo'sh katakni tanlang.").ConfigureAwait(true);
            return;
        }

        if (PlaceLesson is null)
        {
            await _dialogs.ErrorAsync(
                    "Qo'yiladigan darsni tanlang. Ro'yxatda faqat rejada bor, lekin hali " +
                    "joylashtirilmagan darslar ko'rinadi.")
                .ConfigureAwait(true);
            return;
        }

        // Shu dars uchun hali kartochkasi yo'q kartani topamiz.
        var card = _board.Cards.FirstOrDefault(c =>
            !c.IsPlaced && c.EntityId is null && c.LessonId == PlaceLesson.LessonId);

        if (card is null)
        {
            await _dialogs.InfoAsync("Bu dars uchun qo'yiladigan soat qolmadi.").ConfigureAwait(true);
            return;
        }

        var day = SelectedCell.Day;
        var period = SelectedCell.LessonNumber;

        var evaluation = _board.Evaluate(card, day, period);
        ShowEvaluation(evaluation);

        if (!evaluation.IsAllowed)
        {
            PlacementSummary = "Dars qo'yilmadi — to'siqlar mavjud.";
            await _dialogs.InfoAsync(evaluation.ReasonText, "Dars qo'yilmadi").ConfigureAwait(true);
            return;
        }

        if (evaluation.Reasons.Count > 0)
        {
            var accepted = await _dialogs.ConfirmAsync(
                    evaluation.ReasonText + "\n\nBaribir qo'yilsinmi?", "Ogohlantirish")
                .ConfigureAwait(true);

            if (!accepted)
            {
                PlacementSummary = "Dars qo'yilmadi.";
                return;
            }
        }

        try
        {
            IsBusy = true;

            _board.MoveCard(card, new SlotPosition(day, period));
            await PersistAsync(ct).ConfigureAwait(true);

            PlacementSummary = "Dars muvaffaqiyatli qo'yildi.";
            StatusMessage = PlacementSummary;
        }
        catch (OperationCanceledException)
        {
            PlacementSummary = string.Empty;
            return;
        }
        catch (Exception ex)
        {
            PlacementSummary = "Xatolik yuz berdi.";
            await _dialogs.ErrorAsync("Darsni qo'yishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        await LoadCoreAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Taxtadagi o'zgarishlarni bazaga yozadi.</summary>
    private async Task PersistAsync(CancellationToken ct)
    {
        var context = new BoardWriteContext(_scheduleId, _periodIdByNumber);
        var result = await _writer.SaveAsync(_board, context, ct).ConfigureAwait(true);

        if (result.HasRejections)
        {
            throw new InvalidOperationException(
                string.Join("\n", result.Rejections.Select(r => r.Message)));
        }

        _board.ClearDirty();
    }

    private void ShowEvaluation(PlacementEvaluation evaluation)
    {
        PlacementConflicts.Clear();

        var severity = evaluation.IsAllowed ? ConflictSeverity.Warning : ConflictSeverity.Error;
        var code = evaluation.IsAllowed ? ConflictCodes.SubjectRepeatedInDay : ConflictCodes.ClassBusy;

        foreach (var reason in evaluation.Reasons)
        {
            PlacementConflicts.Add(new ConflictRowViewModel(new Conflict(severity, code, reason)));
        }

        HasPlacementResult = true;
    }

    /// <summary>Tanlangan katakdagi darsni o'chiradi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DeleteSelectedAsync(CancellationToken ct = default)
    {
        if (SelectedCell is null || !SelectedCell.HasEntry)
        {
            await _dialogs.InfoAsync("Avval darsi bor katakni tanlang.").ConfigureAwait(true);
            return;
        }

        await DeleteEntryAsync(SelectedCell, ct).ConfigureAwait(true);
    }

    /// <summary>Sinf yoki butun maktab jadvalini tozalaydi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ClearScheduleAsync(CancellationToken ct = default)
        => RunExclusiveAsync(ClearScheduleCoreAsync, ct);

    private async Task ClearScheduleCoreAsync(CancellationToken ct)
    {
        SchoolClass? target = null;
        string question;

        if (IsClassMode && FilterClassGroup is not null)
        {
            target = FilterClassGroup;
            question = $"\"{target.Name}\" sinfining butun jadvali o'chirilsinmi?" +
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

            foreach (var card in _board.Cards.Where(c => c.IsPlaced && !c.IsLocked).ToList())
            {
                if (target is null || card.ClassIds.Contains(target.Id) || card.ClassGroupId == target.Id)
                {
                    _board.MoveCard(card, null);
                }
            }

            await PersistAsync(ct).ConfigureAwait(true);

            StatusMessage = "Jadval tozalandi.";
            PlacementConflicts.Clear();
            PlacementSummary = string.Empty;
            HasPlacementResult = false;
        }
        catch (OperationCanceledException)
        {
            return;
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

        await LoadCoreAsync(ct).ConfigureAwait(true);
    }

    /// <summary>Joriy tanlovga mos jadvalni PDF ga yuklab oladi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ExportPdfAsync(CancellationToken ct = default)
        => RunExclusiveAsync(ExportPdfCoreAsync, ct);

    private async Task ExportPdfCoreAsync(CancellationToken ct)
    {
        // E-01: qamrov ANIQ tanlanadi — "butun maktab" tasodifan chiqmaydi.
        if (IsTeacherMode && FilterTeacher is null)
        {
            await _dialogs.InfoAsync("Avval o'qituvchini tanlang.").ConfigureAwait(true);
            return;
        }

        if (!IsTeacherMode && FilterClassGroup is null)
        {
            await _dialogs.InfoAsync("Avval sinfni tanlang.").ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "PDF tayyorlanmoqda...";

            var options = new PdfExportOptions { SchoolName = null };

            // Eksport hali eski sinf identifikatorini kutadi (LegacyClassGroupId).
            var legacyClassId = FilterClassGroup?.LegacyClassGroupId ?? FilterClassGroup?.Id ?? 0;

            var document = IsTeacherMode
                ? await _pdfExporter
                    .ExportTeacherScheduleAsync(FilterTeacher!.Id, options, ct).ConfigureAwait(true)
                : await _pdfExporter
                    .ExportClassScheduleAsync(legacyClassId, options, ct).ConfigureAwait(true);

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

    /// <summary>TimeOnly ni "HH:mm" ko'rinishiga keltiradi.</summary>
    private static string ToTimeText(TimeOnly value)
        => value.ToString("HH\\:mm", CultureInfo.InvariantCulture);
}

/// <summary>Qo'yish panelidagi bitta joylashtirilmagan dars.</summary>
public sealed partial class UnplacedLessonOption : ObservableObject
{
    /// <summary>Tanlangan sinf filtriga tushadimi.</summary>
    [ObservableProperty]
    private bool _isVisibleForClass = true;

    /// <summary>Yangi band yaratadi.</summary>
    public UnplacedLessonOption(UnplacedLessonView source, int classId)
    {
        ArgumentNullException.ThrowIfNull(source);

        LessonId = source.LessonId;
        ClassId = classId;

        var scope = string.IsNullOrWhiteSpace(source.GroupName)
            ? source.ClassName
            : source.ClassName + " / " + source.GroupName;

        var teachers = source.TeacherNames.Count == 0
            ? string.Empty
            : " — " + string.Join(", ", source.TeacherNames);

        Name = $"{scope}: {source.SubjectName}{teachers} ({source.RemainingPeriods} soat)";
    }

    /// <summary>Dars ta'rifi Id.</summary>
    public int LessonId { get; }

    /// <summary>Sinf Id (topilmasa 0).</summary>
    public int ClassId { get; }

    /// <summary>Ko'rinadigan nom.</summary>
    public string Name { get; }
}

/// <summary>Jadval to'rining bitta katagi (sarlavha yoki dars katagi).</summary>
public sealed partial class TimetableCellViewModel : ObservableObject
{
    private readonly TimetableViewModel _owner;

    /// <summary>Katakdagi kartaning UI identifikatori.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntry))]
    [NotifyPropertyChangedFor(nameof(State))]
    private int? _cardId;

    [ObservableProperty]
    private string _subjectName = string.Empty;

    [ObservableProperty]
    private string _personName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoom))]
    [NotifyPropertyChangedFor(nameof(RoomDisplayText))]
    private string _roomText = string.Empty;

    [ObservableProperty]
    private string _colorCode = "#FFFFFF";

    [ObservableProperty]
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
    public bool HasEntry => CardId.HasValue;

    /// <summary>Xona ko'rsatilsinmi.</summary>
    public bool HasRoom => !string.IsNullOrWhiteSpace(RoomText);

    /// <summary>Xona matni ("Xona: 12").</summary>
    public string RoomDisplayText => HasRoom ? "Xona: " + RoomText : string.Empty;

    /// <summary>
    /// Katakning semantik holati. Rang bu yerda emas — uni XAML uslublari va
    /// konverterlar hal qiladi (M-06).
    /// </summary>
    public TimetableCellState State => IsHeader
        ? TimetableCellState.Header
        : (HasEntry ? TimetableCellState.Occupied : TimetableCellState.Empty);

    /// <summary>Katakni tanlash.</summary>
    [RelayCommand]
    private void Select() => _owner.SelectCell(this);

    /// <summary>Katakdagi darsni o'chirish.</summary>
    [RelayCommand]
    private Task DeleteAsync(CancellationToken ct = default) => _owner.DeleteEntryAsync(this, ct);
}
