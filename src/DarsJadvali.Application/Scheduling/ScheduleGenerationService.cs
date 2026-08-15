using System.Diagnostics;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Pipeline;

namespace DarsJadvali.Application.Scheduling;

/// <summary>
/// <c>DarsJadvali.Scheduling</c> yadrosini EF ma'lumot modeliga ulaydigan generatsiya servisi.
/// </summary>
/// <remarks>
/// <b>Eski <c>GreedyScheduleGenerator</c> dan farqlari (05-audit K-01..K-04, K-12, K-13):</b>
/// <list type="number">
/// <item><b>Tranzaksiya.</b> "eskisini o'chir → yangisini yoz → bandlikni qayta qur"
/// ketma-ketligi BITTA tranzaksiyada. Yozish o'rtasida xato bo'lsa eski jadval joyida
/// qoladi (eski generator eski jadvalni commit bilan o'chirib yuborardi).</item>
/// <item><b>Qidiruv.</b> Yadro 6 fazali (Verify → Propagate → Construct → EjectionChain →
/// → Optimize → Relax), ya'ni backtracking, local search va skorlash bor.</item>
/// <item><b>Determinizm.</b> Urug' butun jarayonga uzatiladi.</item>
/// <item><b>Diagnostika.</b> Hard buzilishlar, soft jarima taqsimoti, tekshiruv xatolari va
/// yumshatish tavsiyalari qaytariladi.</item>
/// </list>
/// <para>
/// <b>Qadamlar tartibi ataylab shunday:</b> og'ir hisob-kitob (yadro) tranzaksiyadan
/// TASHQARIDA bajariladi — aks holda SQLite yozuv qulfi butun generatsiya davomida
/// ushlab turilardi. Tranzaksiya faqat yozish bosqichini qamrab oladi.
/// </para>
/// </remarks>
public sealed class ScheduleGenerationService : IScheduleGenerationService
{
    private readonly IUnitOfWork _uow;
    private readonly ISchedulingStore _store;
    private readonly ISchedulingMapper _mapper;
    private readonly ICardOccurrenceProjector _projector;

    /// <summary>Yangi servis yaratadi.</summary>
    public ScheduleGenerationService(
        IUnitOfWork uow,
        ISchedulingStore store,
        ISchedulingMapper mapper,
        ICardOccurrenceProjector projector)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    /// <inheritdoc />
    public string Name => "aSc uslubidagi generator";

    /// <inheritdoc />
    public string Description =>
        "Cheklovlarni tarqatish, ejection chain va simulated annealing bilan ishlaydigan " +
        "ko'p fazali generator: kartochkalarni joylashtiradi, jarimani kamaytiradi va " +
        "natijani bitta tranzaksiyada saqlaydi.";

    /// <inheritdoc />
    public async Task<ScheduleGenerationReport> GenerateAsync(
        ScheduleGenerationOptions options,
        IProgress<ScheduleGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var clock = Stopwatch.StartNew();
        var messages = new List<string>();
        var scheduleId = options.ScheduleId ?? 0;

        try
        {
            return await RunAsync(options, progress, clock, messages, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilish — xato emas. Tranzaksiya qaytarilgan, baza tegilmagan.
            clock.Stop();
            messages.Add("Jarayon bekor qilindi — eski jadval joyida qoldi.");
            return CancelledReport(scheduleId, messages, clock.Elapsed);
        }
    }

    private async Task<ScheduleGenerationReport> RunAsync(
        ScheduleGenerationOptions options,
        IProgress<ScheduleGenerationProgress>? progress,
        Stopwatch clock,
        List<string> messages,
        CancellationToken ct)
    {
        var scheduleId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow, options.ScheduleId, ct).ConfigureAwait(false);

        // --- 1. O'qish (tranzaksiyadan tashqarida, qamrovi aniq so'rovlar bilan) ---
        var input = await _store.LoadAsync(scheduleId, ct).ConfigureAwait(false);
        if (!options.KeepLocked && input.LockedCards.Count > 0)
        {
            input = input with { LockedCards = Array.Empty<Domain.Entities.Card>() };
            messages.Add("Qulflangan kartochkalar hisobga olinmadi (KeepLocked = false).");
        }

        // --- 2. O'girish + qidiruv (sof hisob, bazaga tegmaydi) ---
        var mapped = _mapper.BuildProblem(input);
        messages.AddRange(mapped.Notes);

        var coreOptions = new GenerationOptions
        {
            Seed = options.Seed,
            Complexity = options.Complexity,
            TimeLimit = options.TimeLimit,
            AllowRelaxation = options.AllowRelaxation,
        };

        var scheduler = new Scheduler(ConstraintSet.CreateDefault());
        var coreProgress = progress is null
            ? null
            : new Progress<GenerationProgress>(p => progress.Report(ToProgress(p)));

        var result = scheduler.Generate(mapped.Problem, coreOptions, coreProgress, ct);

        var cards = _mapper.BuildCards(input, mapped, result.Solution);

        // --- 3. Application darajasidagi qoida: GROUP_DIVISION_OVERLAP ---
        var views = _mapper.BuildPlacedViews(input, cards);
        var conflicts = GroupDivisionOverlapValidator.Check(views);

        var complete = result.IsComplete;
        var cancelled = result.Cancelled || ct.IsCancellationRequested;

        // --- 4. Yozish shartlari ---
        var refuseReason = ResolveRefusal(options, cancelled, complete, conflicts);
        if (refuseReason is not null)
        {
            messages.Add(refuseReason);
            clock.Stop();
            return BuildReport(scheduleId, result, cards, conflicts, messages, clock.Elapsed,
                               applied: false, occurrenceRows: 0, cancelled: cancelled);
        }

        // --- 5. Yozish — BUTUNLAY bitta tranzaksiyada ---
        var occurrenceRows = await _uow.ExecuteInTransactionAsync(async token =>
        {
            // Qulflangan kartochkalar ham o'chiriladi va AYNAN o'sha pozitsiyada qayta
            // yoziladi (yadro ularni C-GBL-06 bo'yicha qimirlatmaydi). Shu tufayli
            // "yarim eski, yarim yangi" holat umuman bo'lmaydi.
            var removed = await _store
                .DeleteCardsAsync(scheduleId, keepLocked: false, token)
                .ConfigureAwait(false);

            var ids = await _store.InsertCardsAsync(cards, token).ConfigureAwait(false);
            for (var i = 0; i < ids.Count && i < cards.Count; i++)
            {
                mapped.Map.CardDbIds[cards[i].CoreCardId] = ids[i];
            }

            messages.Add($"Eski jadval o'rniga {cards.Count} ta kartochka yozildi ({removed} tasi o'chirildi).");
            return await _projector.RebuildForScheduleAsync(scheduleId, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        // Nima joylashmaganini ANIQ ko'rsatish uchun (taxmin emas) — yozilgandan keyin o'qiladi.
        var unplaced = await _store.LoadUnplacedLessonsAsync(scheduleId, ct).ConfigureAwait(false);

        clock.Stop();
        return BuildReport(scheduleId, result, cards, conflicts, messages, clock.Elapsed,
                           applied: true, occurrenceRows: occurrenceRows, cancelled: false,
                           unplaced: unplaced);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Conflict>> ValidateAsync(
        int? scheduleId = null, CancellationToken ct = default)
    {
        var id = await ActiveScheduleResolver.ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);
        var placed = await _store.LoadPlacedCardsAsync(id, ct).ConfigureAwait(false);
        return GroupDivisionOverlapValidator.Check(placed);
    }

    // =====================================================================

    /// <summary>
    /// Natijani yozmaslik sababi (yoki <c>null</c> — yozish mumkin).
    /// Bekor qilinganda ATAYLAB hech narsa yozilmaydi: eski jadval buzilmaydi (00 §6.4/4).
    /// </summary>
    private static string? ResolveRefusal(
        ScheduleGenerationOptions options, bool cancelled, bool complete,
        IReadOnlyList<Conflict> conflicts)
    {
        if (cancelled)
        {
            return "Jarayon bekor qilindi — hech narsa o'zgartirilmadi, eski jadval joyida qoldi.";
        }

        if (conflicts.Count > 0)
        {
            return "Natijada bo'linish ziddiyati topildi (GROUP_DIVISION_OVERLAP) — jadval saqlanmadi.";
        }

        if (!complete && !options.SavePartial)
        {
            return "Jadval to'liq tuzilmadi va qisman saqlash o'chirilgan — hech narsa yozilmadi.";
        }

        return null;
    }

    /// <summary>Bekor qilingan (hech narsa yozilmagan) natija.</summary>
    private static ScheduleGenerationReport CancelledReport(
        int scheduleId, IReadOnlyList<string> messages, TimeSpan elapsed) => new()
        {
            Success = false,
            Applied = false,
            Cancelled = true,
            ScheduleId = scheduleId,
            PlacedCards = 0,
            TotalCards = 0,
            OccurrenceRows = 0,
            SoftCost = 0,
            HardViolations = Array.Empty<string>(),
            PenaltyBreakdown = Array.Empty<PenaltyShare>(),
            VerificationFaults = Array.Empty<string>(),
            RelaxationSuggestions = Array.Empty<string>(),
            Conflicts = Array.Empty<Conflict>(),
            Messages = messages,
            Elapsed = elapsed,
        };

    private static ScheduleGenerationProgress ToProgress(GenerationProgress p) => new(
        p.Phase,
        PhaseName(p.Phase),
        p.PlacedPercent,
        p.PlacedCards,
        p.TotalCards,
        p.SoftCost,
        p.BestSoftCost,
        p.Elapsed);

    /// <summary>Faza nomining o'zbekcha ko'rinishi.</summary>
    public static string PhaseName(GenerationPhase phase) => phase switch
    {
        GenerationPhase.Verify => "Tekshiruv",
        GenerationPhase.Propagate => "Cheklovlarni tarqatish",
        GenerationPhase.Construct => "Dastlabki joylashtirish",
        GenerationPhase.EjectionChain => "Zanjirli tuzatish",
        GenerationPhase.Optimize => "Optimallashtirish",
        GenerationPhase.Relax => "Yumshatish tahlili",
        GenerationPhase.Rooms => "Xonalarni tayinlash",
        _ => "Tayyor",
    };

    private static ScheduleGenerationReport BuildReport(
        int scheduleId,
        GenerationResult result,
        IReadOnlyList<CardWrite> cards,
        IReadOnlyList<Conflict> conflicts,
        List<string> messages,
        TimeSpan elapsed,
        bool applied,
        int occurrenceRows,
        bool cancelled,
        IReadOnlyList<Board.UnplacedLessonView>? unplaced = null)
    {
        var total = result.Solution.CardSlots.Length;
        var placed = result.Solution.PlacedCount;

        messages.Insert(0, placed == total
            ? $"Jadval tayyor: {placed} ta kartochka joylashtirildi."
            : $"{placed} ta kartochka joylashtirildi, {total - placed} tasi joylashmadi.");

        return new ScheduleGenerationReport
        {
            Success = result.IsComplete && conflicts.Count == 0 && !cancelled,
            Applied = applied,
            Cancelled = cancelled,
            ScheduleId = scheduleId,
            PlacedCards = applied ? cards.Count : placed,
            TotalCards = total,
            OccurrenceRows = occurrenceRows,
            SoftCost = result.Cost.SoftCost,
            HardViolations = result.HardViolations
                .Select(v => $"[{v.ConstraintId}] {v.Message}")
                .ToList(),
            PenaltyBreakdown = result.PenaltyBreakdown
                .Where(x => x.Penalty > 0)
                .Select(x => new PenaltyShare(x.Id, x.Name, x.Penalty))
                .ToList(),
            VerificationFaults = result.Verification.Faults
                .Select(f => $"[{f.Code}] {f.Message}")
                .ToList(),
            RelaxationSuggestions = result.Relaxation is null
                ? Array.Empty<string>()
                : result.Relaxation.Suggestions.Select(s => s.Message).ToList(),
            Conflicts = conflicts,
            Messages = messages,
            Elapsed = elapsed,
            UnplacedLessons = unplaced ?? Array.Empty<Board.UnplacedLessonView>(),
        };
    }
}
