using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>
/// aSc uslubidagi <b>jadval tahrirlash yadrosi</b>: virtualizatsiyalangan to'r,
/// "karta qo'lda" drag-drop, jonli to'qnashuv ko'rsatish va 100 qadamli undo/redo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Muhim:</b> bu ViewModel entity ko'rmaydi — faqat <see cref="TimetableCard"/> bilan ishlaydi.
/// Application bilan bog'lanish yagona <see cref="CardViewAdapter"/> faylida.
/// </para>
/// <para>
/// <b>Baholash keshi:</b> <c>IScheduleValidator</c> har chaqiruvda butun bazani qayta o'qiydi,
/// shuning uchun drag paytida u <b>chaqirilmaydi</b>. Jadval nusxasi
/// (<see cref="IScheduleSnapshotProvider.LoadAsync"/>) <b>bir marta</b> yuklanadi va
/// <see cref="TimetableRuleSet.FromSnapshot"/> bilan keshga aylantiriladi; keyingi barcha
/// baholash xotirada bajariladi. Bazaga faqat amal tasdiqlangandan keyin yoziladi.
/// </para>
/// </remarks>
public sealed partial class TimetableBoardViewModel : ViewModelBase
{
    private const string NoClassGroupsMessage =
        "Hali birorta sinf qo'shilmagan. Avval «Sinflar» sahifasidan sinf qo'shing.";

    private const string NoDaysMessage =
        "Faol ish kuni yo'q. «Hafta kunlari» sahifasidan kunlarni yoqing.";

    private const string NoCardsMessage =
        "Bu jadvalda kartochka yo'q. «Avtomatik tuzish» bilan jadval tuzing.";

    private readonly ICardBoardService _cards;
    private readonly IScheduleSnapshotProvider _snapshots;
    private readonly ISchedulingStore _store;
    private readonly IScheduleSetService _schedules;
    private readonly ITeacherService _teachers;
    private readonly ISubjectService _subjects;
    private readonly IAvailabilityService _availability;
    private readonly IDialogService _dialogs;
    private readonly TimetableBoardWriter _writer;

    private readonly TimetableBoard _board = new();
    private readonly CommandHistory _history = new(CommandHistory.DefaultLimit);
    private readonly DragSession _drag;

    private readonly Dictionary<(int ScopeId, WeekDay Day, int Period), TimetableSlotViewModel> _slotIndex = new();
    private readonly Dictionary<int, string> _slotTimes = new();
    private readonly Dictionary<int, int> _periodIdByNumber = new();
    private readonly Dictionary<int, int> _shiftOfPeriod = new();
    private readonly Dictionary<int, int> _shiftOfClass = new();
    private readonly Dictionary<string, int> _roomIds = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<Teacher> _teacherList = Array.Empty<Teacher>();
    private IReadOnlyList<Subject> _subjectList = Array.Empty<Subject>();
    private IReadOnlyList<SchoolClass> _classList = Array.Empty<SchoolClass>();

    private int _scheduleId;
    private TimetableSlotViewModel? _hoverSlot;
    private bool _isRebuilding;
    private bool _suppressScopeReload;

    /// <summary>Ko'rinish turi: sinf / o'qituvchi / xona / umumiy.</summary>
    [ObservableProperty]
    private TimetableViewKind _viewKind = TimetableViewKind.Class;

    /// <summary>Tanlangan resurs (0 — barchasi).</summary>
    [ObservableProperty]
    private TimetableScopeOption? _selectedScope;

    /// <summary>Tanlangan smena (0 — barcha smenalar).</summary>
    [ObservableProperty]
    private TimetableShiftOption? _selectedShift;

    /// <summary>To'rda ko'rsatadigan narsa yo'q.</summary>
    [ObservableProperty]
    private bool _isGridEmpty = true;

    /// <summary>Bo'sh to'r uchun izoh.</summary>
    [ObservableProperty]
    private string _emptyMessage = NoClassGroupsMessage;

    /// <summary>Qo'lda karta bormi (aSc card-in-hand).</summary>
    [ObservableProperty]
    private bool _hasCardInHand;

    /// <summary>Qo'ldagi karta haqidagi matn.</summary>
    [ObservableProperty]
    private string _handText = string.Empty;

    /// <summary>Kursor ostidagi pozitsiya bahosi (jonli fikr-mulohaza).</summary>
    [ObservableProperty]
    private PlacementRating? _hoverRating;

    /// <summary>Nega qo'yib bo'lmasligi (yoki ogohlantirish) matni.</summary>
    [ObservableProperty]
    private string _hoverReason = string.Empty;

    /// <summary>Joylashtirilmagan kartalar soni matni.</summary>
    [ObservableProperty]
    private string _unplacedText = "0 ta";

    /// <summary>Smena tanlagichi ko'rinadimi (kamida ikkita smena bo'lsa).</summary>
    [ObservableProperty]
    private bool _hasShifts;

    /// <summary>Yangi jadval yadrosi ViewModel'i yaratadi.</summary>
    public TimetableBoardViewModel(
        ICardBoardService cards,
        IScheduleSnapshotProvider snapshots,
        ISchedulingStore store,
        IScheduleSetService schedules,
        ITeacherService teachers,
        ISubjectService subjects,
        IAvailabilityService availability,
        IDialogService dialogs)
    {
        _cards = cards;
        _snapshots = snapshots;
        _store = store;
        _schedules = schedules;
        _teachers = teachers;
        _subjects = subjects;
        _availability = availability;
        _dialogs = dialogs;
        _writer = new TimetableBoardWriter(cards);

        _drag = new DragSession(_board);
        _drag.Changed += (_, _) => OnDragChanged();
        _history.Changed += (_, _) => OnHistoryChanged();
    }

    /// <summary>To'r o'lchamlari — sarlavha va tana kataklari shu bitta obyektga bog'lanadi.</summary>
    public TimetableMetrics Metrics { get; } = new();

    /// <summary>Kun sarlavhalari.</summary>
    public ObservableCollection<TimetableDayHeaderViewModel> DayHeaders { get; } = new();

    /// <summary>To'r qatorlari (virtualizatsiya shu ro'yxat ustida ishlaydi).</summary>
    public ObservableCollection<TimetableRowViewModel> Rows { get; } = new();

    /// <summary>Joylashtirilmagan kartalar paneli.</summary>
    public ObservableCollection<TimetableCard> UnplacedCards { get; } = new();

    /// <summary>Ko'rinish almashtirgichdagi bandlar.</summary>
    public ObservableCollection<TimetableScopeOption> Scopes { get; } = new();

    /// <summary>Smena tanlagichidagi bandlar.</summary>
    public ObservableCollection<TimetableShiftOption> Shifts { get; } = new();

    /// <summary>Xotiradagi jadval taxtasi (sinovlar va View uchun).</summary>
    public TimetableBoard Board => _board;

    /// <summary>Komandalar tarixi — 100 qadam.</summary>
    public ICommandHistory History => _history;

    /// <summary>Drag sessiyasi.</summary>
    public DragSession Drag => _drag;

    /// <summary>Bekor qilish mumkinmi.</summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>Qaytarish mumkinmi.</summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>Undo tugmasi uchun izoh.</summary>
    public string UndoTooltip => _history.NextUndoTitle is { } t ? $"Bekor qilish: {t}" : "Bekor qilish";

    /// <summary>Redo tugmasi uchun izoh.</summary>
    public string RedoTooltip => _history.NextRedoTitle is { } t ? $"Qaytarish: {t}" : "Qaytarish";

    /// <summary>Tarix holati ("12 / 100 qadam").</summary>
    public string HistoryText => $"{_history.UndoCount} / {_history.Limit} qadam";

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken ct = default)
        => RunExclusiveAsync(LoadCoreAsync, ct);

    /// <summary>Ma'lumotni bazadan qayta o'qiydi va to'rni quradi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task RefreshAsync(CancellationToken ct = default)
        => RunExclusiveAsync(LoadCoreAsync, ct);

    private async Task LoadCoreAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Jadval yuklanmoqda...";

            _scheduleId = await _schedules.GetActiveIdAsync(ct).ConfigureAwait(true);

            // Butun ma'lumot BIR MARTA o'qiladi — keyingi barcha baholash xotirada.
            _teacherList = await _teachers.GetAllAsync(ct).ConfigureAwait(true);
            _subjectList = await _subjects.GetAllAsync(ct).ConfigureAwait(true);

            // Baholash qoidalarining YAGONA manbasi (kun, chegara, ish vaqti, me'yor).
            var snapshot = await _snapshots.LoadAsync(_scheduleId, ct).ConfigureAwait(true);

            // Qo'ng'iroq jadvali, smenalar va sinflar — yangi (Card/Lesson) model.
            var input = await _store.LoadAsync(_scheduleId, ct).ConfigureAwait(true);

            var cardViews = await _cards.GetCardsAsync(_scheduleId, ct).ConfigureAwait(true);
            var unplacedLessons = await _cards.GetUnplacedAsync(_scheduleId, ct).ConfigureAwait(true);

            // O'qituvchilar ish vaqti — BITTA ommaviy so'rov (ilgari o'qituvchi boshiga bittadan).
            var availability = await _availability
                .GetLessonAvailabilityForAllAsync(ct)
                .ConfigureAwait(true);

            BuildTimeStructure(input);

            var blocked = ToBlockedSlots(availability, snapshot);

            var rules = CardViewAdapter.ToRuleSet(snapshot, AllPeriodNumbers(), blocked);

            var placed = CardViewAdapter.ToCards(cardViews, _teacherList, _subjectList, _shiftOfClass);
            var unplaced = CardViewAdapter.ToUnplacedCards(
                unplacedLessons,
                _teacherList,
                _subjectList,
                NextUiId(cardViews),
                ClassIdByName(),
                _shiftOfClass);

            _board.Load(placed.Concat(unplaced), rules);
            _board.ClearDirty();
            _history.Clear();
            _drag.Cancel();

            RebuildShifts();
            RebuildScopes();
            RebuildGrid();

            StatusMessage = "Tayyor.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bekor qilindi.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Yuklashda xatolik.";
            await _dialogs.ErrorAsync("Jadvalni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Qo'ng'iroq jadvali, smenalar va sinflar keshini to'ldiradi.</summary>
    private void BuildTimeStructure(DarsJadvali.Application.Scheduling.SchedulingInput input)
    {
        _slotTimes.Clear();
        _periodIdByNumber.Clear();
        _shiftOfPeriod.Clear();
        _shiftOfClass.Clear();

        var shiftNoById = input.Shifts.ToDictionary(s => s.Id, s => s.ShiftNo);

        foreach (var period in input.Periods.Where(p => !p.IsBreak).OrderBy(p => p.PeriodNo))
        {
            _periodIdByNumber[period.PeriodNo] = period.Id;
            _slotTimes[period.PeriodNo] = ToTimeText(period.StartTime) + "-" + ToTimeText(period.EndTime);

            if (period.ShiftId is { } shiftId && shiftNoById.TryGetValue(shiftId, out var shiftNo))
            {
                _shiftOfPeriod[period.PeriodNo] = shiftNo;
            }
        }

        _classList = input.Classes.Where(c => !c.IsDeleted).ToList();

        foreach (var schoolClass in _classList)
        {
            if (schoolClass.ShiftId is { } shiftId && shiftNoById.TryGetValue(shiftId, out var shiftNo))
            {
                _shiftOfClass[schoolClass.Id] = shiftNo;
            }
        }

        ShiftList = input.Shifts.OrderBy(s => s.ShiftNo).Select(s => (s.ShiftNo, s.Name)).ToList();
    }

    /// <summary>Bazadagi smenalar (raqam, nom).</summary>
    private IReadOnlyList<(int No, string Name)> ShiftList { get; set; } = Array.Empty<(int, string)>();

    /// <summary>To'rdagi barcha dars soati raqamlari.</summary>
    private IReadOnlyList<int> AllPeriodNumbers()
        => _periodIdByNumber.Keys.OrderBy(n => n).ToList();

    private Dictionary<string, int> ClassIdByName()
    {
        var map = new Dictionary<string, int>(StringComparer.CurrentCulture);

        foreach (var item in _classList)
        {
            map[item.Name] = item.Id;
        }

        return map;
    }

    private static int NextUiId(IReadOnlyList<CardView> views)
        => views.Count == 0 ? 1_000_000 : Math.Max(1_000_000, views.Max(v => v.CardId) + 1);

    /// <summary>
    /// Ommaviy bandlik natijasini (o'qituvchi, kun, soat) uchliklariga aylantiradi.
    /// </summary>
    /// <remarks>
    /// Qoidaning o'zi (vaqt oralig'i → ruxsat etilgan soat) Application'da,
    /// <c>LessonAvailabilityRules</c> da hisoblanadi — bu yerda faqat teskari to'plam olinadi.
    /// </remarks>
    private List<(int TeacherId, WeekDay Day, int Period)> ToBlockedSlots(
        IReadOnlyDictionary<int, IReadOnlyList<TeacherDayAvailability>> availability,
        ScheduleSnapshot snapshot)
    {
        var result = new List<(int, WeekDay, int)>();
        var numbers = AllPeriodNumbers();

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

                foreach (var period in numbers)
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

    // ================= Smena =================

    /// <summary>Smena tanlagichini quradi (bir smenali maktabda ko'rinmaydi).</summary>
    private void RebuildShifts()
    {
        var previous = SelectedShift?.ShiftNo ?? 0;

        _suppressScopeReload = true;
        try
        {
            Shifts.Clear();
            Shifts.Add(new TimetableShiftOption(0, "Barcha smenalar"));

            foreach (var (no, name) in ShiftList)
            {
                Shifts.Add(new TimetableShiftOption(
                    no, string.IsNullOrWhiteSpace(name) ? no + "-smena" : name));
            }

            HasShifts = ShiftList.Count > 1;
            SelectedShift = Shifts.FirstOrDefault(s => s.ShiftNo == previous) ?? Shifts[0];
        }
        finally
        {
            _suppressScopeReload = false;
        }
    }

    /// <summary>Joriy smena filtri (0 — barchasi).</summary>
    private int ShiftFilter => SelectedShift?.ShiftNo ?? 0;

    /// <summary>Dars soati joriy smena filtriga tushadimi.</summary>
    private bool IsPeriodInShift(int period)
    {
        var filter = ShiftFilter;

        if (filter == 0)
        {
            return true;
        }

        // Smenaga biriktirilmagan soat barcha smenalarda ko'rinadi.
        return !_shiftOfPeriod.TryGetValue(period, out var shift) || shift == filter;
    }

    private bool IsClassInShift(int classId)
    {
        var filter = ShiftFilter;

        if (filter == 0)
        {
            return true;
        }

        return !_shiftOfClass.TryGetValue(classId, out var shift) || shift == filter;
    }

    partial void OnSelectedShiftChanged(TimetableShiftOption? value)
    {
        if (_isRebuilding || _suppressScopeReload)
        {
            return;
        }

        _drag.Cancel();
        RebuildScopes();
        RebuildGrid();
    }

    // ================= To'r qurish =================

    /// <summary>Ko'rinish turiga qarab almashtirgich bandlarini yig'adi.</summary>
    private void RebuildScopes()
    {
        var previousId = SelectedScope?.Id ?? 0;

        _suppressScopeReload = true;
        try
        {
            Scopes.Clear();
            Scopes.Add(new TimetableScopeOption(0, ViewKind switch
            {
                TimetableViewKind.Teacher => "Barcha o'qituvchilar",
                TimetableViewKind.Room => "Barcha xonalar",
                _ => "Barcha sinflar",
            }));

            foreach (var option in AllScopes())
            {
                Scopes.Add(option);
            }

            SelectedScope = Scopes.FirstOrDefault(s => s.Id == previousId) ?? Scopes[0];
        }
        finally
        {
            _suppressScopeReload = false;
        }
    }

    /// <summary>Joriy ko'rinishdagi barcha resurslar.</summary>
    private IEnumerable<TimetableScopeOption> AllScopes()
    {
        switch (ViewKind)
        {
            case TimetableViewKind.Teacher:
                return _teacherList
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.FullName, StringComparer.CurrentCulture)
                    .Select(t => new TimetableScopeOption(t.Id, t.FullName));

            case TimetableViewKind.Room:
                _roomIds.Clear();
                var rooms = _board.Cards
                    .Select(c => c.RoomNumber)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r, StringComparer.CurrentCulture)
                    .ToList();

                var list = new List<TimetableScopeOption>(rooms.Count);
                for (var i = 0; i < rooms.Count; i++)
                {
                    _roomIds[rooms[i]] = i + 1;
                    list.Add(new TimetableScopeOption(i + 1, rooms[i] + "-xona"));
                }

                return list;

            default:
                return _classList
                    .Where(c => IsClassInShift(c.Id))
                    .OrderBy(c => c.Name, StringComparer.CurrentCulture)
                    .Select(c => new TimetableScopeOption(c.Id, ScopeName(c)));
        }
    }

    private string ScopeName(SchoolClass item)
        => _shiftOfClass.TryGetValue(item.Id, out var shift) && ShiftList.Count > 1
            ? $"{item.Name} ({shift}-smena)"
            : item.Name;

    /// <summary>To'rni qaytadan quradi (ko'rinish yoki ma'lumot o'zgarganda).</summary>
    private void RebuildGrid()
    {
        _isRebuilding = true;

        try
        {
            Rows.Clear();
            DayHeaders.Clear();
            _slotIndex.Clear();
            _hoverSlot = null;

            var days = _board.Rules.Days;

            if (days.Count == 0)
            {
                IsGridEmpty = true;
                EmptyMessage = NoDaysMessage;
                RefreshUnplaced();
                return;
            }

            foreach (var day in days)
            {
                DayHeaders.Add(new TimetableDayHeaderViewModel(day, Metrics));
            }

            var filterId = SelectedScope?.Id ?? 0;
            var scopes = AllScopes()
                .Where(s => filterId == 0 || s.Id == filterId)
                .ToList();

            if (scopes.Count == 0)
            {
                IsGridEmpty = true;
                EmptyMessage = ViewKind switch
                {
                    TimetableViewKind.Room => "Xona biriktirilgan dars yo'q.",
                    TimetableViewKind.Class when _classList.Count == 0 => NoClassGroupsMessage,
                    _ => NoCardsMessage,
                };
                RefreshUnplaced();
                return;
            }

            // Ikki smenada soat raqamlari uzluksiz (1-smena 1..6, 2-smena 7..12);
            // ro'yxat qo'ng'iroq jadvalidan keladi, 1..N deb taxmin qilinmaydi.
            var periods = _board.Rules.PeriodNumbers.Where(IsPeriodInShift).ToList();

            if (periods.Count == 0)
            {
                IsGridEmpty = true;
                EmptyMessage = "Bu smenada dars soatlari sozlanmagan.";
                RefreshUnplaced();
                return;
            }

            var alternate = false;

            foreach (var scope in scopes)
            {
                var first = true;

                foreach (var period in periods)
                {
                    _slotTimes.TryGetValue(period, out var time);

                    var row = new TimetableRowViewModel(
                        scope.Id, scope.Name, first, alternate, period, time ?? string.Empty, Metrics);

                    foreach (var day in days)
                    {
                        var slot = new TimetableSlotViewModel(this, scope.Id, day, period, Metrics);
                        row.Slots.Add(slot);
                        _slotIndex[(scope.Id, day, period)] = slot;
                    }

                    Rows.Add(row);
                    first = false;
                }

                alternate = !alternate;
            }

            IsGridEmpty = false;
        }
        finally
        {
            _isRebuilding = false;
        }

        RefreshCards();
        RefreshUnplaced();
    }

    /// <summary>Kartalarni kataklarga joylashtiradi (to'r qayta qurilmaydi).</summary>
    private void RefreshCards()
    {
        foreach (var slot in _slotIndex.Values)
        {
            slot.Card = null;
            slot.IsContinuation = false;
        }

        foreach (var card in _board.Cards)
        {
            if (!card.IsPlaced)
            {
                continue;
            }

            var day = card.Day!.Value;
            var start = card.Period!.Value;

            foreach (var scopeId in ScopesOf(card))
            {
                var offset = 0;

                // Juft dars barcha egallagan soatlarida ko'rinadi — yaxlit blok bo'lib.
                foreach (var period in card.PeriodsFrom(start))
                {
                    if (_slotIndex.TryGetValue((scopeId, day, period), out var slot))
                    {
                        slot.Card = card;
                        slot.IsContinuation = offset > 0;
                    }

                    offset++;
                }
            }
        }
    }

    /// <summary>Karta joriy ko'rinishda qaysi qatorlarga tegishli.</summary>
    private IEnumerable<int> ScopesOf(TimetableCard card)
    {
        switch (ViewKind)
        {
            case TimetableViewKind.Teacher:
                return card.TeacherIds;

            case TimetableViewKind.Room:
                return string.IsNullOrWhiteSpace(card.RoomNumber) ||
                       !_roomIds.TryGetValue(card.RoomNumber!.Trim(), out var roomId)
                    ? Array.Empty<int>()
                    : new[] { roomId };

            default:
                return card.ClassIds.Count > 0 ? card.ClassIds : new[] { card.ClassGroupId };
        }
    }

    /// <summary>
    /// Joylashtirilmagan kartalar panelini yangilaydi — <b>faqat farqni</b> qo'llab.
    /// </summary>
    /// <remarks>
    /// Ro'yxat katta bo'lishi mumkin (generatsiya to'liq bo'lmasa minglab karta).
    /// Har ko'chirishda <c>Clear()</c> + N ta <c>Add()</c> qilish M-05 dagi xatoni takrorlardi:
    /// bitta amal uchun minglab <c>CollectionChanged</c> hodisasi. Shuning uchun faqat
    /// qo'shilgan/olib tashlangan kartalar yangilanadi.
    /// </remarks>
    private void RefreshUnplaced()
    {
        var desired = _board.UnplacedCards.ToHashSet();

        // To'liq qayta qurish faqat boshlang'ich yuklashda yoki katta o'zgarishda.
        if (UnplacedCards.Count == 0 || Math.Abs(desired.Count - UnplacedCards.Count) > 32)
        {
            UnplacedCards.Clear();

            foreach (var card in desired
                         .OrderBy(c => c.ClassName, StringComparer.CurrentCulture)
                         .ThenBy(c => c.SubjectName, StringComparer.CurrentCulture))
            {
                UnplacedCards.Add(card);
            }

            UnplacedText = UnplacedCards.Count + " ta";
            return;
        }

        for (var i = UnplacedCards.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(UnplacedCards[i]))
            {
                UnplacedCards.RemoveAt(i);
            }
        }

        var existing = UnplacedCards.ToHashSet();

        foreach (var card in desired)
        {
            if (existing.Contains(card))
            {
                continue;
            }

            UnplacedCards.Insert(SortedIndexFor(card), card);
        }

        UnplacedText = UnplacedCards.Count + " ta";
    }

    /// <summary>Kartani sinf → fan tartibida qaysi o'ringa qo'yish kerakligini topadi.</summary>
    private int SortedIndexFor(TimetableCard card)
    {
        for (var i = 0; i < UnplacedCards.Count; i++)
        {
            var other = UnplacedCards[i];
            var byClass = string.Compare(card.ClassName, other.ClassName, StringComparison.CurrentCulture);

            if (byClass < 0 ||
                (byClass == 0 &&
                 string.Compare(card.SubjectName, other.SubjectName, StringComparison.CurrentCulture) < 0))
            {
                return i;
            }
        }

        return UnplacedCards.Count;
    }

    partial void OnViewKindChanged(TimetableViewKind value)
    {
        if (_isRebuilding)
        {
            return;
        }

        _drag.Cancel();
        RebuildScopes();
        RebuildGrid();
    }

    partial void OnSelectedScopeChanged(TimetableScopeOption? value)
    {
        if (_isRebuilding || _suppressScopeReload)
        {
            return;
        }

        _drag.Cancel();
        RebuildGrid();
    }

    // ================= Drag-drop ("karta qo'lda") =================

    /// <summary>Kartani "qo'lga oladi". <paramref name="groupMove"/> — CTRL bosilgan.</summary>
    public bool PickUp(TimetableCard? card, bool groupMove = false)
    {
        if (card is null)
        {
            return false;
        }

        if (card.IsLocked)
        {
            StatusMessage = "Karta qulflangan — avval qulfni oching.";
            return false;
        }

        var picked = _drag.TryPickUp(card, groupMove);

        if (picked)
        {
            ApplyHighlights();
        }

        return picked;
    }

    /// <summary>
    /// Kursor katak ustiga kelganda — jonli baholash.
    /// </summary>
    /// <remarks>
    /// Faqat <b>ikkita</b> katak yangilanadi (eskisi va yangisi) — 1800 katakni har piksel
    /// harakatida aylanib chiqish 60 fps ni buzardi. Baholash xotiradagi nusxa ustida
    /// bajariladi: bazaga murojaat yo'q.
    /// </remarks>
    public void HoverSlot(TimetableSlotViewModel? slot)
    {
        if (ReferenceEquals(_hoverSlot, slot))
        {
            return;
        }

        if (_hoverSlot is not null)
        {
            _hoverSlot.IsHoverTarget = false;
            _hoverSlot.Rating = null;
        }

        _hoverSlot = slot;

        if (slot is null)
        {
            _drag.ClearHover();
            return;
        }

        slot.IsHoverTarget = true;

        // SHIFT qo'lda karta bo'lmaganda kursor ostidagi karta uchun ishlaydi (aSc §4.2).
        if (!_drag.IsActive)
        {
            _drag.SetHighlightCard(slot.Card);
        }

        _drag.Hover(slot.Day, slot.Period);

        slot.Rating = _drag.IsActive ? _drag.HoverEvaluation?.Rating : null;
    }

    /// <summary>SHIFT holatini yangilaydi (yoritish to'plami qayta hisoblanadi).</summary>
    public void SetHighlighting(bool shiftHeld)
    {
        _drag.SetHighlighting(shiftHeld);
        ApplyHighlights();
    }

    /// <summary>Katakka bosildi: qo'lda karta bo'lsa — qo'yiladi, aks holda karta olinadi.</summary>
    public void ClickSlot(TimetableSlotViewModel slot, bool ctrlHeld)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (_drag.IsActive)
        {
            DropAt(slot.Day, slot.Period);
            return;
        }

        if (slot.Card is not null)
        {
            PickUp(slot.Card, ctrlHeld);
        }
    }

    /// <summary>Qo'ldagi kartani berilgan pozitsiyaga qo'yadi.</summary>
    public void DropAt(WeekDay day, int period)
    {
        var command = _drag.BuildDropCommand(day, period);

        if (command is null)
        {
            StatusMessage = _drag.HoverEvaluation?.ReasonText is { Length: > 0 } reason
                ? reason
                : "Bu pozitsiyaga qo'yib bo'lmaydi.";
            return;
        }

        _drag.Complete();
        Apply(command);
    }

    /// <summary>Qo'ldagi kartani joylashtirilmaganlar paneliga qaytaradi.</summary>
    [RelayCommand]
    private void ReturnToPanel()
    {
        var command = _drag.BuildReturnCommand();
        _drag.Complete();

        if (command is not null)
        {
            Apply(command);
        }
    }

    /// <summary><c>ESC</c> — kartani qo'ldan qo'yib yuboradi.</summary>
    [RelayCommand]
    private void CancelDrag()
    {
        _drag.Cancel();
        ApplyHighlights();
        StatusMessage = "Bekor qilindi.";
    }

    private void OnDragChanged()
    {
        HasCardInHand = _drag.IsActive;

        HandText = _drag.PrimaryCard is { } card
            ? _drag.IsGroupMove
                ? $"Qo'lda: {card.SubjectName} (+{_drag.CardsInHand.Count - 1} ta guruh kartasi)"
                : $"Qo'lda: {card.SubjectName} — {card.ScopeText}"
            : string.Empty;

        HoverRating = _drag.HoverEvaluation?.Rating;
        HoverReason = _drag.HoverEvaluation?.ReasonText ?? string.Empty;
    }

    /// <summary>
    /// SHIFT yoritishini kataklar va kun sarlavhalariga tarqatadi.
    /// </summary>
    /// <remarks>
    /// To'liq aylanish faqat yoritish to'plami o'zgarganda (SHIFT bosilganda, karta olinganda
    /// yoki tashlanganda) bajariladi — kursor harakatida emas.
    /// </remarks>
    private void ApplyHighlights()
    {
        var highlighting = _drag.IsHighlighting;
        var positions = new HashSet<SlotPosition>(_drag.HighlightedPositions);
        var days = new HashSet<WeekDay>(_drag.HighlightedPositions.Select(p => p.Day));

        foreach (var slot in _slotIndex.Values)
        {
            slot.IsHighlighted = highlighting && positions.Contains(new SlotPosition(slot.Day, slot.Period));
        }

        // Kun sarlavhalari ham bo'yaladi (aSc §4.1: "kun va dars sarlavhalari rangga bo'yaladi").
        foreach (var header in DayHeaders)
        {
            header.Rating = !highlighting
                ? null
                : days.Contains(header.Day)
                    ? PlacementRating.Preferred
                    : PlacementRating.Forbidden;
        }
    }

    // ================= Amallar =================

    /// <summary>Kartani qulflaydi / qulfdan chiqaradi (bazaga yoziladi).</summary>
    [RelayCommand]
    private void ToggleLock(TimetableCard? card)
    {
        if (card is null)
        {
            return;
        }

        Apply(new SetLockCommand(_board, card, !card.IsLocked));
    }

    /// <summary>Kartani jadvaldan olib, panelga qaytaradi.</summary>
    [RelayCommand]
    private void UnplaceCard(TimetableCard? card)
    {
        if (card is null || !card.IsPlaced)
        {
            return;
        }

        if (card.IsLocked)
        {
            StatusMessage = "Karta qulflangan — avval qulfni oching.";
            return;
        }

        Apply(new MoveCardCommand(_board, card, null));
    }

    /// <summary>Bo'sh katakda o'ng tugma — shu joyga mos kartalar (teskari qidiruv, aSc §4.3).</summary>
    public IReadOnlyList<TimetableCard> CandidatesFor(TimetableSlotViewModel slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return _board.CandidatesFor(slot.Day, slot.Period);
    }

    /// <summary>Bekor qiladi (<c>Ctrl+Z</c>).</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_history.Undo())
        {
            AfterBoardChanged("Bekor qilindi.");
        }
    }

    /// <summary>Qaytaradi (<c>Ctrl+Y</c>).</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_history.Redo())
        {
            AfterBoardChanged("Qaytarildi.");
        }
    }

    /// <summary>Masshtabni kattalashtiradi (<c>+</c>).</summary>
    [RelayCommand]
    private void ZoomIn() => Metrics.ZoomIn();

    /// <summary>Masshtabni kichiklashtiradi (<c>-</c>).</summary>
    [RelayCommand]
    private void ZoomOut() => Metrics.ZoomOut();

    /// <summary>Masshtabni asl holiga qaytaradi.</summary>
    [RelayCommand]
    private void ZoomReset() => Metrics.ZoomReset();

    /// <summary>Matn ranglarini invert qiladi (<c>*</c>).</summary>
    [RelayCommand]
    private void ToggleInvert() => Metrics.IsInverted = !Metrics.IsInverted;

    /// <summary>Ko'rinishni almashtiradi.</summary>
    [RelayCommand]
    private void SetViewKind(TimetableViewKind kind) => ViewKind = kind;

    /// <summary>Zichlikni almashtiradi.</summary>
    [RelayCommand]
    private void SetDensity(TimetableDensity density) => Metrics.Density = density;

    /// <summary>Komandani bajaradi, tarixga qo'shadi va bazaga yozadi.</summary>
    private void Apply(IUndoableCommand command)
    {
        _history.Execute(command);
        AfterBoardChanged(command.Title);
    }

    private void AfterBoardChanged(string status)
    {
        RefreshCards();
        RefreshUnplaced();
        ApplyHighlights();
        StatusMessage = status;

        // Bazaga yozish navbat orqali — AsyncOperationRunner naqshi buzilmaydi.
        _ = RunExclusiveAsync(PersistAsync);
    }

    private void OnHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoTooltip));
        OnPropertyChanged(nameof(RedoTooltip));
        OnPropertyChanged(nameof(HistoryText));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// O'zgargan kartalarni bazaga yozadi — <b>bitta ommaviy chaqiruvda</b>.
    /// </summary>
    /// <remarks>
    /// <c>CompositeCommand</c> (CTRL guruh ko'chishi, ommaviy amallar) N ta karta
    /// o'zgartirsa ham <c>PlaceManyAsync</c> BIR MARTA chaqiriladi va butun ish
    /// bitta tranzaksiyada bajariladi — ilgari har karta uchun alohida
    /// <c>SaveChanges</c> ketardi va o'rtada xato chiqsa jadval yarim holatda qolardi.
    /// </remarks>
    private async Task PersistAsync(CancellationToken ct)
    {
        if (_board.DirtyCardIds.Count == 0)
        {
            return;
        }

        try
        {
            var context = new BoardWriteContext(_scheduleId, _periodIdByNumber);
            var result = await _writer.SaveAsync(_board, context, ct).ConfigureAwait(true);

            if (result.HasRejections)
            {
                StatusMessage = "O'zgarish qabul qilinmadi.";
                await _dialogs
                    .ErrorAsync("O'zgarishni saqlab bo'lmadi:\n\n" +
                                string.Join("\n", result.Rejections.Select(r => "• " + r.Message)))
                    .ConfigureAwait(true);

                // Bazadagi holat o'zgarmadi — taxta qayta yuklanadi.
                await LoadCoreAsync(ct).ConfigureAwait(true);
                return;
            }

            _board.ClearDirty();

            if (result.NeedsReload)
            {
                // Kartochka Id lari o'zgardi — taxta va tarix qaytadan quriladi.
                // V2_08 dan keyin bu yo'lga tushilmaydi: yaratish/o'chirish nuqta
                // API'lari mavjud Id larni saqlaydi.
                await LoadCoreAsync(ct).ConfigureAwait(true);
                StatusMessage = "Saqlandi (jadval qayta yuklandi).";
            }
            else if (result.DeletedCards > 0 || result.CreatedCards > 0)
            {
                // Taxta ham, undo tarixi ham joyida qoladi.
                StatusMessage = "Saqlandi.";
            }
        }
        catch (OperationCanceledException)
        {
            // "Iflos" belgilar saqlanib qoladi — keyingi amalda qayta yoziladi.
        }
        catch (Exception ex)
        {
            StatusMessage = "Saqlashda xatolik.";
            await _dialogs.ErrorAsync("O'zgarishni saqlashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
    }

    private static string ToTimeText(TimeOnly value)
        => value.ToString("HH\\:mm", CultureInfo.InvariantCulture);
}

/// <summary>Smena tanlagichidagi bitta band.</summary>
/// <param name="ShiftNo">Smena raqami (0 — barcha smenalar).</param>
/// <param name="Name">Ko'rinadigan nom.</param>
public sealed record TimetableShiftOption(int ShiftNo, string Name);
