using System.Diagnostics;
using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Evaluation;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Rooms;
using DarsJadvali.Scheduling.Util;

namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>Generatsiya natijasi.</summary>
public sealed class GenerationResult
{
    public required Solution Solution { get; init; }
    public required SolutionCost Cost { get; init; }
    public required VerificationReport Verification { get; init; }
    public RelaxationReport? Relaxation { get; init; }
    public required IReadOnlyList<HardViolation> HardViolations { get; init; }
    public required IReadOnlyList<(string Id, string Name, long Penalty)> PenaltyBreakdown { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required int RestartsUsed { get; init; }
    public required int OptimizeIterations { get; init; }
    public required bool Cancelled { get; init; }

    /// <summary>Barcha kartalar joylashgan va hard cheklovlar buzilmagan.</summary>
    public bool IsComplete => Cost.IsFeasible;

    public double PlacedPercent
        => Solution.CardSlots.Length == 0 ? 100.0 : 100.0 * Solution.PlacedCount / Solution.CardSlots.Length;

    public override string ToString()
        => $"joylashgan {Solution.PlacedCount}/{Solution.CardSlots.Length} ({PlacedPercent:F1}%), " +
           $"{Cost}, {Elapsed.TotalSeconds:F2}s";
}

/// <summary>
/// Butun 6 fazali pipeline (02-asc-.., 3.2):
/// Verify → Propagate → Construct → EjectionChain → Optimize → Relax (+ xona tayinlash fazasi).
/// Anytime: <see cref="CancellationToken"/> bekor qilinganda ham eng yaxshi topilgan yechim qaytadi.
/// Determinizm: bir xil <see cref="GenerationOptions.Seed"/> → bayt-bayt bir xil natija
/// (agar <see cref="GenerationOptions.TimeLimit"/> berilmagan va bekor qilinmagan bo'lsa).
/// </summary>
public sealed class Scheduler
{
    private readonly ConstraintSet _constraints;

    public Scheduler(ConstraintSet? constraints = null)
        => _constraints = constraints ?? ConstraintSet.CreateDefault();

    public ConstraintSet Constraints => _constraints;

    public GenerationResult Generate(
        Problem problem,
        GenerationOptions? options = null,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new GenerationOptions();
        var clock = Stopwatch.StartNew();
        var evaluator = new PenaltyEvaluator(_constraints);

        // ---------- FAZA 0: VERIFY ----------
        var verification = options.RunVerify ? Verifier.Verify(problem) : new VerificationReport();
        Report(progress, GenerationPhase.Verify, 0, 0, problem.Cards.Length, 0, 0, problem.Cards.Length, clock);

        if (!verification.IsOk && !options.ContinueOnVerifyFaults)
        {
            var emptyState = new SolutionState(problem);
            return Finish(emptyState, evaluator, verification, null, clock, 0, 0, false);
        }

        // ---------- FAZA 1: PROPAGATE ----------
        foreach (var c in problem.Cards) c.ConflictCount = 0;
        Propagator.ResetDomains(problem);
        var prop = Propagator.Propagate(problem);
        Report(progress, GenerationPhase.Propagate, 0, 0, problem.Cards.Length, 0, 0, problem.Cards.Length, clock);

        var state = new SolutionState(problem);
        Solution? best = null;
        SolutionCost bestCost = new(int.MaxValue, int.MaxValue, long.MaxValue);
        int restartsUsed = 0;

        if (!prop.Feasible)
        {
            verification.Add("PROPAGATION_FAILED",
                $"Domain qisqartirish natijasida karta #{prop.FailedCardId} uchun joy qolmadi.",
                "C-AVL-01..05");
        }

        int restarts = Math.Max(1, options.EffectiveRestarts);
        for (int r = 0; r < restarts; r++)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (options.TimeLimit.HasValue && clock.Elapsed > options.TimeLimit.Value) break;

            restartsUsed = r + 1;
            var rng = new Xoshiro256SS(unchecked(options.Seed * 1000003 + r));

            // ---------- FAZA 2: CONSTRUCT ----------
            var constructor = new Constructor(state, evaluator, rng);
            constructor.Construct(options.EffectiveBacktracks, cancellationToken);
            Report(progress, GenerationPhase.Construct, r, state.PlacedCount, problem.Cards.Length,
                   0, 0, state.UnplacedCount, clock);

            // ---------- FAZA 3: EJECTION CHAIN ----------
            if (state.UnplacedCount > 0)
            {
                var repair = new EjectionChainRepair(state, rng, options.EffectiveEjectionDepth);
                repair.Repair(Math.Max(1000, options.EffectiveBacktracks), cancellationToken);
                Report(progress, GenerationPhase.EjectionChain, r, state.PlacedCount, problem.Cards.Length,
                       0, 0, state.UnplacedCount, clock);
            }

            var cost = evaluator.FastCost(state);
            if (Better(cost, bestCost))
            {
                bestCost = cost;
                best = state.Snapshot();
            }
            if (cost.Unplaced == 0) break;
        }

        best ??= state.Snapshot();
        state.RestoreFrom(best);

        // ---------- Xona tayinlash fazasi (#1855) ----------
        if (problem.Rooms.Length > 0)
        {
            RoomAssigner.AssignAll(state);
            Report(progress, GenerationPhase.Rooms, 0, state.PlacedCount, problem.Cards.Length,
                   0, 0, state.UnplacedCount, clock);
        }

        // ---------- FAZA 4: OPTIMIZE ----------
        int iterations = 0;
        if (state.PlacedCount > 0)
        {
            var optRng = new Xoshiro256SS(unchecked(options.Seed * 7919 + 13));
            var optimizer = new Optimizer(state, evaluator, optRng, options);
            var improved = optimizer.Optimize(options.EffectiveOptimizeIterations, clock,
                                              options.TimeLimit, progress, cancellationToken);
            iterations = optimizer.Iterations;
            state.RestoreFrom(improved);
        }

        // ---------- FAZA 5: RELAX ----------
        RelaxationReport? relaxation = null;
        if (state.UnplacedCount > 0 && options.AllowRelaxation)
        {
            relaxation = Relaxer.Analyze(state);
            Report(progress, GenerationPhase.Relax, 0, state.PlacedCount, problem.Cards.Length,
                   0, 0, state.UnplacedCount, clock);
        }

        return Finish(state, evaluator, verification, relaxation, clock, restartsUsed, iterations,
                      cancellationToken.IsCancellationRequested);
    }

    private static bool Better(SolutionCost a, SolutionCost b)
        => a.Unplaced != b.Unplaced ? a.Unplaced < b.Unplaced : a.SoftCost < b.SoftCost;

    private static GenerationResult Finish(
        SolutionState state, PenaltyEvaluator evaluator, VerificationReport verification,
        RelaxationReport? relaxation, Stopwatch clock, int restarts, int iterations, bool cancelled)
    {
        clock.Stop();
        var solution = state.Snapshot();
        var violations = new List<HardViolation>();
        int hard = HardRules.Check(solution, violations);
        var cost = new SolutionCost(solution.UnplacedCount, hard, evaluator.EvaluateSoft(state));

        return new GenerationResult
        {
            Solution = solution,
            Cost = cost,
            Verification = verification,
            Relaxation = relaxation,
            HardViolations = violations,
            PenaltyBreakdown = evaluator.Breakdown(state),
            Elapsed = clock.Elapsed,
            RestartsUsed = restarts,
            OptimizeIterations = iterations,
            Cancelled = cancelled,
        };
    }

    private static void Report(IProgress<GenerationProgress>? progress, GenerationPhase phase, int iteration,
                               int placed, int total, long soft, long bestSoft, int unplaced, Stopwatch clock)
        => progress?.Report(new GenerationProgress(phase, iteration, placed, total, soft, bestSoft, unplaced, clock.Elapsed));
}
