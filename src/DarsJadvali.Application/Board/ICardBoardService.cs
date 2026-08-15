using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Board;

/// <summary>
/// Jadval to'rining <c>Card</c>/<c>Lesson</c> asosidagi servisi — prezentatsiya qatlami
/// (Desktop/Web) uchun yagona kirish nuqtasi.
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> Eski <c>IScheduleService</c> faqat <c>ScheduleEntry</c> bilan
/// ishlaydi, unda esa juft dars, hafta maskasi, qulflash va guruh bo'linmasi YO'Q.
/// Shu sababli UI ularni standart qiymat bilan to'ldirishga majbur edi, qulf esa
/// bazaga umuman saqlanmasdi (dastur qayta ochilganda yo'qolardi).
/// <para>
/// <b>Tranzaksiya.</b> Yozuvchi metodlar butun ishni (kartochka + bandlik proyeksiyasi)
/// <c>IUnitOfWork.ExecuteInTransactionAsync</c> ichida bajaradi — undo/redo dagi
/// <c>CompositeCommand</c> N ta alohida <c>SaveChanges</c> qilmaydi (00 §6.4).
/// </para>
/// </remarks>
public interface ICardBoardService
{
    /// <summary>
    /// Jadvaldagi barcha kartochkalar. <paramref name="scheduleId"/> <c>null</c> — faol jadval.
    /// </summary>
    Task<IReadOnlyList<CardView>> GetCardsAsync(int? scheduleId = null, CancellationToken ct = default);

    /// <summary>
    /// To'liq joylashtirilmagan darslar (dars, fan, sinf, guruh, qolgan soat).
    /// </summary>
    Task<IReadOnlyList<UnplacedLessonView>> GetUnplacedAsync(
        int? scheduleId = null, CancellationToken ct = default);

    /// <summary>Bitta kartochkani ko'chiradi (<see cref="PlaceManyAsync"/> ning qisqartmasi).</summary>
    Task<CardBulkResult> PlaceAsync(
        CardPlacement placement, bool force = false, int? scheduleId = null, CancellationToken ct = default);

    /// <summary>
    /// Bir nechta kartochkani <b>bitta tranzaksiyada</b> ko'chiradi: bittasi rad etilsa
    /// hech biri yozilmaydi va bandlik proyeksiyasi ham tegilmaydi.
    /// </summary>
    /// <param name="placements">Ko'chirish so'rovlari.</param>
    /// <param name="force">
    /// Qulflangan kartochkani ham ko'chirish. To'qnashuv (o'qituvchi/guruh/xona bandligi)
    /// bunda ham RAD ETILADI — u baza darajasidagi kafolat.
    /// </param>
    /// <param name="scheduleId">Jadval varianti. <c>null</c> — faol jadval.</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    Task<CardBulkResult> PlaceManyAsync(
        IReadOnlyList<CardPlacement> placements,
        bool force = false,
        int? scheduleId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Kartochka qulfini <b>bazaga</b> saqlaydi. Ilgari qulf faqat xotirada edi va
    /// dastur qayta ochilganda yo'qolardi.
    /// </summary>
    /// <returns>Kartochka topilib, holat yozilgan bo'lsa <c>true</c>.</returns>
    Task<bool> SetLockAsync(int cardId, bool isLocked, CancellationToken ct = default);

    /// <summary>
    /// BITTA kartochkani o'chiradi (bandlik qatorlari bilan birga).
    /// </summary>
    /// <remarks>
    /// <b>Nima uchun kerak.</b> Ilgari prezentatsiya qatlami bitta kartochkani o'chirish
    /// uchun <c>ISchedulingStore.DeleteCardsAsync</c> + <c>InsertCardsAsync</c> bilan
    /// BUTUN jadvalni qayta yozardi. Oqibati: barcha <c>Card.Id</c> lar o'zgarardi,
    /// taxta to'liq qayta yuklanardi va undo tarixi tozalanardi.
    /// </remarks>
    /// <returns>Kartochka topilib o'chirilgan bo'lsa <c>true</c>.</returns>
    Task<bool> DeleteCardAsync(int cardId, CancellationToken ct = default);

    /// <summary>
    /// Rejadagi dars uchun YANGI kartochka yaratadi va uni berilgan kun/soatga qo'yadi.
    /// </summary>
    /// <remarks>
    /// To'qnashuv <see cref="PlaceManyAsync"/> bilan bir xil qoidalar bo'yicha
    /// tekshiriladi: rad etilsa bazaga umuman tegilmaydi.
    /// <para>
    /// Ilgari bu yo'l umuman yo'q edi va prezentatsiya qatlami <c>ISchedulingStore</c>
    /// hamda bandlik proyektorini TO'G'RIDAN-TO'G'RI chaqirishga majbur edi.
    /// </para>
    /// </remarks>
    /// <param name="request">
    /// Yaratish so'rovi: dars, kun, soat, uzunlik, hafta maskasi hamda ixtiyoriy xona
    /// (<see cref="CardCreateRequest.ClassroomIds"/> yoki
    /// <see cref="CardCreateRequest.RoomNumber"/>) va qulf holati.
    /// </param>
    /// <param name="scheduleId">Jadval varianti. <c>null</c> — faol jadval.</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    Task<CardCreateResult> CreateCardAsync(
        CardCreateRequest request, int? scheduleId = null, CancellationToken ct = default);

    /// <summary>
    /// <see cref="CreateCardAsync(CardCreateRequest, int?, CancellationToken)"/> ning
    /// qisqartmasi — faol jadvalga, xonasiz va qulfsiz kartochka qo'yadi.
    /// </summary>
    /// <remarks>
    /// Ataylab <b>standart interfeys a'zosi</b> (default interface member): mavjud
    /// implementatsiyalar va testlardagi soxta obyektlar o'zgarishsiz qoladi, lekin
    /// prezentatsiya qatlami shu qisqa imzoni chaqira oladi.
    /// </remarks>
    /// <param name="lessonId">Dars ta'rifi Id.</param>
    /// <param name="dayNo">Kun raqami (0-based).</param>
    /// <param name="periodId">Boshlanish dars soati Id.</param>
    /// <param name="length">Ketma-ket soatlar soni (juft dars uchun 2).</param>
    /// <param name="weeksMask">Hafta maskasi (A/B hafta).</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    Task<CardCreateResult> CreateCardAsync(
        int lessonId, int dayNo, int periodId, int length, int weeksMask,
        CancellationToken ct = default)
        => CreateCardAsync(
            new CardCreateRequest(lessonId, dayNo, periodId, length, weeksMask), null, ct);
}

/// <summary><see cref="ICardBoardService"/> implementatsiyasi.</summary>
public sealed class CardBoardService : ICardBoardService
{
    private readonly IUnitOfWork _uow;
    private readonly ISchedulingStore _store;
    private readonly ICardOccurrenceProjector _projector;

    /// <summary>Yangi servis yaratadi.</summary>
    public CardBoardService(
        IUnitOfWork uow, ISchedulingStore store, ICardOccurrenceProjector projector)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CardView>> GetCardsAsync(
        int? scheduleId = null, CancellationToken ct = default)
    {
        var id = await ActiveScheduleResolver.ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);
        return await _store.LoadCardViewsAsync(id, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnplacedLessonView>> GetUnplacedAsync(
        int? scheduleId = null, CancellationToken ct = default)
    {
        var id = await ActiveScheduleResolver.ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);
        return await _store.LoadUnplacedLessonsAsync(id, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<CardBulkResult> PlaceAsync(
        CardPlacement placement, bool force = false, int? scheduleId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return PlaceManyAsync(new[] { placement }, force, scheduleId, ct);
    }

    /// <inheritdoc />
    public async Task<CardBulkResult> PlaceManyAsync(
        IReadOnlyList<CardPlacement> placements,
        bool force = false,
        int? scheduleId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placements);

        if (placements.Count == 0)
        {
            return new CardBulkResult(true, Array.Empty<CardPlacementResult>(), 0);
        }

        var targetId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);

        // Ma'lumot BIR MARTA o'qiladi: N ta ko'chirish uchun N ta to'liq o'qish qilinmaydi.
        var cards = await _store.LoadCardViewsAsync(targetId, ct).ConfigureAwait(false);
        var occupancy = await _store.LoadOccupancyAsync(targetId, ct).ConfigureAwait(false);
        var input = await _store.LoadAsync(targetId, ct).ConfigureAwait(false);

        var board = new CardBoardState(cards, occupancy, input);

        // Ko'chayotgan kartochkalarning ESKI bandligi oldindan bo'shatiladi — aks holda
        // ikki kartochkaning o'rin almashtirishi (A → B ning joyi, B → A ning joyi)
        // noto'g'ri "band" deb rad etilardi.
        board.Release(placements.Select(p => p.CardId));

        var results = new List<CardPlacementResult>(placements.Count);
        var applied = new List<CardPlacement>(placements.Count);

        foreach (var placement in placements)
        {
            ct.ThrowIfCancellationRequested();

            var conflicts = board.Evaluate(placement, force);
            if (conflicts.Count > 0)
            {
                results.Add(new CardPlacementResult(placement.CardId, false, conflicts));
                continue;
            }

            // Keyingi so'rovlar shu qarorni KO'RADI (ikkita karta bir slotga tushmasligi uchun).
            board.Apply(placement);
            applied.Add(placement);
            results.Add(new CardPlacementResult(placement.CardId, true, Array.Empty<Conflict>()));
        }

        if (results.Any(r => !r.Placed))
        {
            // "Hammasi yoki hech narsa" — bazaga umuman tegilmaydi.
            return new CardBulkResult(false, results, 0);
        }

        var rows = await _uow.ExecuteInTransactionAsync(async token =>
        {
            // Ko'chirish tartibi store'da hisoblanadi: o'rin almashtirish ham ishlaydi.
            await _store.MoveCardsAsync(applied, token).ConfigureAwait(false);

            // Proyeksiya kartochkalar yozilgandan KEYIN va IKKI FAZADA qayta quriladi
            // (avval hammasining eski qatorlari o'chadi) — "o'rin almashtirish"
            // stsenariysida unikal indeks noto'g'ri to'smasligi uchun.
            return await _projector
                .RebuildForCardsAsync(applied.Select(p => p.CardId).ToList(), token)
                .ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        return new CardBulkResult(true, results, rows);
    }

    /// <inheritdoc />
    public Task<bool> SetLockAsync(int cardId, bool isLocked, CancellationToken ct = default)
        => _uow.ExecuteInTransactionAsync(
            token => _store.SetCardLockAsync(cardId, isLocked, token), ct);

    /// <inheritdoc />
    public Task<bool> DeleteCardAsync(int cardId, CancellationToken ct = default)
        => _uow.ExecuteInTransactionAsync(
            token => _store.DeleteCardAsync(cardId, token), ct);

    /// <inheritdoc />
    public async Task<CardCreateResult> CreateCardAsync(
        CardCreateRequest request, int? scheduleId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var targetId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);

        var input = await _store.LoadAsync(targetId, ct).ConfigureAwait(false);

        var lesson = input.Lessons.FirstOrDefault(l => l.Id == request.LessonId);
        if (lesson is null)
        {
            return Rejected(ConflictCodes.ClassBusy,
                $"Dars ta'rifi topilmadi (ID: {request.LessonId}).");
        }

        var length = Math.Max(1, request.Length);
        var weeksMask = request.WeeksMask <= 0 ? 1 : request.WeeksMask;

        // Yangi kartochka HALI bazada yo'q, shuning uchun tekshiruvga sun'iy (manfiy
        // Id'li) CardView qo'shiladi: bandlik qoidalari ko'chirish bilan AYNAN bir xil
        // yo'ldan — CardBoardState.Evaluate orqali — baholanadi.
        const int draftId = -1;
        var draft = new CardView(
            CardId: draftId,
            ScheduleId: targetId,
            LessonId: lesson.Id,
            SubjectId: lesson.SubjectId,
            SubjectName: input.Subjects.FirstOrDefault(s => s.Id == lesson.SubjectId)?.Name ?? string.Empty,
            TeacherIds: input.LessonTeachers.Where(x => x.LessonId == lesson.Id)
                .Select(x => x.TeacherId).Distinct().ToList(),
            TeacherNames: Array.Empty<string>(),
            SchoolClassIds: input.LessonClasses.Where(x => x.LessonId == lesson.Id)
                .Select(x => x.SchoolClassId).Distinct().ToList(),
            ClassName: string.Empty,
            StudentGroupIds: input.LessonGroups.Where(x => x.LessonId == lesson.Id)
                .Select(x => x.StudentGroupId).Distinct().ToList(),
            GroupName: string.Empty,
            DayNo: request.DayNo,
            PeriodId: request.PeriodId,
            PeriodNo: 0,
            Length: length,
            WeeksMask: weeksMask,
            IsLocked: false,
            RoomNumber: request.RoomNumber)
        {
            ClassroomIds = request.ClassroomIds,
        };

        var cards = await _store.LoadCardViewsAsync(targetId, ct).ConfigureAwait(false);
        var occupancy = await _store.LoadOccupancyAsync(targetId, ct).ConfigureAwait(false);

        var board = new CardBoardState(cards.Append(draft).ToList(), occupancy, input);
        var conflicts = board.Evaluate(
            new CardPlacement(draftId, request.DayNo, request.PeriodId, weeksMask), force: false);

        if (conflicts.Count > 0) return new CardCreateResult(false, 0, conflicts);

        var write = new CardWrite(
            CoreCardId: 0,
            ScheduleId: targetId,
            LessonId: lesson.Id,
            PeriodId: request.PeriodId,
            DayNo: request.DayNo,
            WeeksMask: weeksMask,
            IsLocked: request.IsLocked,
            ClassroomIds: request.ClassroomIds,
            Length: length)
        {
            RoomNumber = request.RoomNumber,
        };

        return await _uow.ExecuteInTransactionAsync(async token =>
        {
            var ids = await _store.InsertCardsAsync(new[] { write }, token).ConfigureAwait(false);
            if (ids.Count == 0)
            {
                return new CardCreateResult(false, 0, new[]
                {
                    new Conflict(ConflictSeverity.Error, ConflictCodes.ClassBusy,
                        "Kartochka yozilmadi."),
                });
            }

            var rows = await _projector
                .RebuildForCardsAsync(new[] { ids[0] }, token).ConfigureAwait(false);

            return new CardCreateResult(true, ids[0], Array.Empty<Conflict>(), rows);
        }, ct).ConfigureAwait(false);
    }

    private static CardCreateResult Rejected(string code, string message)
        => new(false, 0, new[] { new Conflict(ConflictSeverity.Error, code, message) });
}

/// <summary>
/// Kartochkalarning xotiradagi holati: ko'chirishni baholaydi va qabul qilingan
/// ko'chirishni o'ziga qo'llaydi.
/// </summary>
/// <remarks>
/// Bu <b>Application darajasidagi</b> tekshiruv. Baza darajasidagi kafolat —
/// <c>UX_CardOccurrences_Schedule_Resource_Slot</c> unikal indeksi — joyida qoladi
/// va oxirgi himoya bo'lib xizmat qiladi.
/// </remarks>
internal sealed class CardBoardState
{
    private readonly Dictionary<int, CardView> _cards;
    private readonly Dictionary<int, int> _periodNoById;
    private readonly Dictionary<int, int> _maxPeriodNoOfDay;
    private readonly HashSet<int> _activeDays;

    /// <summary>(kun, soat, hafta, resurs) → egallab turgan kartochka Id.</summary>
    private readonly Dictionary<(int Day, int Period, int Week, ResourceKind Kind, int Id), int> _busy = new();

    /// <summary>Kartochka Id → u band qiladigan resurslar.</summary>
    private readonly Dictionary<int, List<(ResourceKind Kind, int Id)>> _resources = new();

    internal CardBoardState(
        IReadOnlyList<CardView> cards,
        IReadOnlyList<CardOccupancy> occupancy,
        Scheduling.SchedulingInput input)
    {
        _cards = cards.ToDictionary(c => c.CardId);
        _periodNoById = input.Periods.Where(p => !p.IsBreak).ToDictionary(p => p.Id, p => p.PeriodNo);

        var hasDayNo = input.WorkDays.Any(w => w.DayNo > 0);
        _activeDays = input.WorkDays
            .Where(w => w.IsActive)
            .Select(w => hasDayNo ? w.DayNo : DayNumbering.ToDayNo(w.DayOfWeek))
            .ToHashSet();

        // Ish kunlari sozlanmagan bo'lsa cheklov qo'yilmaydi.
        if (_activeDays.Count == 0)
        {
            _activeDays = input.Periods.Count == 0
                ? new HashSet<int>()
                : Enumerable.Range(0, Math.Max(1, input.Year.DaysPerWeek)).ToHashSet();
        }

        var maxPeriodNo = _periodNoById.Count == 0 ? 0 : _periodNoById.Values.Max();
        _maxPeriodNoOfDay = _activeDays.ToDictionary(d => d, _ => maxPeriodNo);

        foreach (var row in occupancy)
        {
            _busy[(row.DayNo, row.PeriodNo, row.WeekNo, row.ResourceKind, row.ResourceId)] = row.CardId;

            if (!_resources.TryGetValue(row.CardId, out var list))
            {
                list = new List<(ResourceKind, int)>();
                _resources[row.CardId] = list;
            }

            if (!list.Contains((row.ResourceKind, row.ResourceId)))
            {
                list.Add((row.ResourceKind, row.ResourceId));
            }
        }

        FillMissingResources(cards, input);
    }

    /// <summary>
    /// Bandlik qatorlari hali qurilmagan kartochkalar uchun resurslar darsdan
    /// (o'qituvchi + guruh) tiklanadi — aks holda ular hech qanday to'qnashuv
    /// bermay qolardi.
    /// </summary>
    private void FillMissingResources(
        IReadOnlyList<CardView> cards, Scheduling.SchedulingInput input)
    {
        var missing = cards.Where(c => !_resources.ContainsKey(c.CardId)).ToList();
        if (missing.Count == 0) return;

        // "Butun sinf" guruhi sinfning BARCHA guruhlarini band qiladi (proyektor bilan bir xil qoida).
        var groupsOfClass = input.Groups
            .Where(g => !g.IsDeleted)
            .GroupBy(g => g.SchoolClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());

        var entireClassOf = input.Groups
            .Where(g => g.IsEntireClass && !g.IsDeleted)
            .ToDictionary(g => g.Id, g => g.SchoolClassId);

        foreach (var card in missing)
        {
            var list = new List<(ResourceKind Kind, int Id)>();

            foreach (var teacherId in card.TeacherIds)
            {
                list.Add((ResourceKind.Teacher, teacherId));
            }

            foreach (var groupId in card.StudentGroupIds)
            {
                if (entireClassOf.TryGetValue(groupId, out var classId) &&
                    groupsOfClass.TryGetValue(classId, out var siblings))
                {
                    foreach (var id in siblings) list.Add((ResourceKind.StudentGroup, id));
                }
                else
                {
                    list.Add((ResourceKind.StudentGroup, groupId));
                }
            }

            // V2_07: tayinlangan xona ham resurs. Aks holda bandlik qatorlari hali
            // qurilmagan kartochka xonani "bo'sh" deb ko'rsatardi va bir xonaga ikki
            // dars qo'yish Application darajasida o'tib ketardi.
            foreach (var classroomId in card.ClassroomIds)
            {
                list.Add((ResourceKind.Classroom, classroomId));
            }

            _resources[card.CardId] = list.Distinct().ToList();
        }
    }

    /// <summary>Berilgan kartochkalarning eski bandligini bo'shatadi.</summary>
    internal void Release(IEnumerable<int> cardIds)
    {
        var ids = cardIds.ToHashSet();
        if (ids.Count == 0) return;

        foreach (var key in _busy.Where(kv => ids.Contains(kv.Value)).Select(kv => kv.Key).ToList())
        {
            _busy.Remove(key);
        }
    }

    /// <summary>Ko'chirishni baholaydi; bo'sh ro'yxat = ruxsat.</summary>
    internal IReadOnlyList<Conflict> Evaluate(CardPlacement placement, bool force)
    {
        var conflicts = new List<Conflict>();

        if (!_cards.TryGetValue(placement.CardId, out var card))
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.ClassBusy,
                $"Ko'chiriladigan kartochka topilmadi (ID: {placement.CardId})."));
            return conflicts;
        }

        if (card.IsLocked && !force)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.ClassBusy,
                $"«{card.SubjectName}» kartochkasi qulflangan — avval qulfni oching."));
            return conflicts;
        }

        if (!_activeDays.Contains(placement.DayNo))
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.DayInactive,
                $"{placement.DayNo + 1}-kun ish kuni emas — dars qo'yib bo'lmaydi."));
        }

        if (!_periodNoById.TryGetValue(placement.PeriodId, out var startPeriodNo))
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.LessonOutOfRange,
                $"Dars soati topilmadi (ID: {placement.PeriodId})."));
            return conflicts;
        }

        // Juft dars kun oxiridan chiqib ketmasligi kerak.
        var max = _maxPeriodNoOfDay.TryGetValue(placement.DayNo, out var m) ? m : 0;
        if (card.Length > 1 && startPeriodNo + card.Length - 1 > max)
        {
            conflicts.Add(new Conflict(ConflictSeverity.Error, ConflictCodes.LessonOutOfRange,
                $"{card.Length} soatlik dars kunga sig'maydi (oxirgi soat — {max})."));
        }

        if (conflicts.Count > 0) return conflicts;

        var weeksMask = placement.WeeksMask ?? card.WeeksMask;
        var resources = _resources.TryGetValue(card.CardId, out var list)
            ? list
            : new List<(ResourceKind Kind, int Id)>();

        foreach (var week in Weeks(weeksMask))
        {
            for (var offset = 0; offset < Math.Max(1, card.Length); offset++)
            {
                foreach (var (kind, id) in resources)
                {
                    var key = (placement.DayNo, startPeriodNo + offset, week, kind, id);
                    if (_busy.TryGetValue(key, out var owner) && owner != card.CardId)
                    {
                        conflicts.Add(new Conflict(ConflictSeverity.Error, Code(kind),
                            Message(kind, card, startPeriodNo + offset, _cards.TryGetValue(owner, out var o) ? o : null)));
                        return conflicts;
                    }
                }
            }
        }

        return conflicts;
    }

    /// <summary>Qabul qilingan ko'chirishni xotiradagi holatga yozadi.</summary>
    internal void Apply(CardPlacement placement)
    {
        if (!_cards.TryGetValue(placement.CardId, out var card)) return;
        if (!_periodNoById.TryGetValue(placement.PeriodId, out var startPeriodNo)) return;

        // Eski bandlik olib tashlanadi.
        foreach (var key in _busy.Where(kv => kv.Value == card.CardId).Select(kv => kv.Key).ToList())
        {
            _busy.Remove(key);
        }

        var weeksMask = placement.WeeksMask ?? card.WeeksMask;
        var resources = _resources.TryGetValue(card.CardId, out var list)
            ? list
            : new List<(ResourceKind Kind, int Id)>();

        foreach (var week in Weeks(weeksMask))
        {
            for (var offset = 0; offset < Math.Max(1, card.Length); offset++)
            {
                foreach (var (kind, id) in resources)
                {
                    _busy[(placement.DayNo, startPeriodNo + offset, week, kind, id)] = card.CardId;
                }
            }
        }

        _cards[card.CardId] = card with
        {
            DayNo = placement.DayNo,
            PeriodId = placement.PeriodId,
            PeriodNo = startPeriodNo,
            WeeksMask = weeksMask,
        };
    }

    private static IEnumerable<int> Weeks(int weeksMask)
    {
        if (weeksMask <= 0)
        {
            yield return 0;
            yield break;
        }

        for (var bit = 0; bit < 32; bit++)
        {
            if ((weeksMask & (1 << bit)) != 0) yield return bit;
        }
    }

    private static string Code(ResourceKind kind) => kind switch
    {
        ResourceKind.Teacher => ConflictCodes.TeacherBusy,
        ResourceKind.Classroom => ConflictCodes.RoomBusy,
        _ => ConflictCodes.ClassBusy,
    };

    private static string Message(ResourceKind kind, CardView card, int periodNo, CardView? other)
    {
        var what = other is null ? "boshqa dars" : $"«{other.SubjectName}»";
        return kind switch
        {
            ResourceKind.Teacher =>
                $"O'qituvchi {periodNo}-soatda band ({what}).",
            ResourceKind.Classroom =>
                $"Xona {periodNo}-soatda band ({what}).",
            _ =>
                $"{card.ClassName} sinfida {periodNo}-soat allaqachon band ({what}).",
        };
    }
}
