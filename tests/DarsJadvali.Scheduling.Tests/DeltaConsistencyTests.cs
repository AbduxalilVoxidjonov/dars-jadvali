using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Evaluation;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Pipeline;
using DarsJadvali.Scheduling.Util;
using Xunit;
using Xunit.Abstractions;

namespace DarsJadvali.Scheduling.Tests;

/// <summary>
/// T-A-02 — ENG MUHIM INVARIANT:
/// <c>DeltaPenalty(m) == Evaluate(after) - Evaluate(before)</c>.
/// </summary>
public class DeltaConsistencyTests
{
    private readonly ITestOutputHelper _out;

    public DeltaConsistencyTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(777)]
    public void Delta_Matches_Full_Evaluation_On_Random_Moves(int seed)
    {
        var p = TestProblems.SmallSchool();
        var (state, eval) = Prepare(p, seed);
        var rng = new Xoshiro256SS(seed * 31 + 5);
        var move = new Move();

        int checkedMoves = 0;
        int attempts = 0;
        while (checkedMoves < 1200 && attempts < 200_000)
        {
            attempts++;
            if (!BuildRandomMove(state, rng, move)) continue;

            long before = eval.EvaluateSoft(state);
            eval.BeginDelta(state, move);
            if (!state.TryApply(move)) { eval.AbortDelta(); continue; }
            long delta = eval.EndDelta(state);
            long after = eval.EvaluateSoft(state);

            Assert.Equal(after - before, delta);
            checkedMoves++;

            if (rng.NextDouble() < 0.5) state.Undo(move);
        }

        _out.WriteLine($"Tekshirilgan ko'chishlar: {checkedMoves} ({attempts} urinish)");
        Assert.True(checkedMoves >= 1000, $"kamida 1000 ta ko'chish kerak, {checkedMoves} ta bo'ldi");
    }

    [Fact]
    public void Delta_Matches_Full_Evaluation_On_Divided_Class()
    {
        var p = TestProblems.DividedClass(out _, out _, out _, out _, out _);
        var (state, eval) = Prepare(p, 4);
        var rng = new Xoshiro256SS(1234);
        var move = new Move();

        int checkedMoves = 0;
        for (int i = 0; i < 40_000 && checkedMoves < 1000; i++)
        {
            if (!BuildRandomMove(state, rng, move)) continue;
            long before = eval.EvaluateSoft(state);
            eval.BeginDelta(state, move);
            if (!state.TryApply(move)) { eval.AbortDelta(); continue; }
            long delta = eval.EndDelta(state);
            Assert.Equal(eval.EvaluateSoft(state) - before, delta);
            checkedMoves++;
            if (rng.NextDouble() < 0.5) state.Undo(move);
        }
        Assert.True(checkedMoves > 100);
    }

    /// <summary>T-A-03: Apply(m); Undo(m) → holat bayt-bayt asl holatiga qaytadi.</summary>
    [Fact]
    public void Undo_Restores_Exact_State()
    {
        var p = TestProblems.SmallSchool();
        var (state, eval) = Prepare(p, 8);
        var rng = new Xoshiro256SS(31337);
        var move = new Move();

        int tested = 0;
        for (int i = 0; i < 50_000 && tested < 500; i++)
        {
            if (!BuildRandomMove(state, rng, move)) continue;
            string before = state.Snapshot().Fingerprint();
            long costBefore = eval.EvaluateSoft(state);
            if (!state.TryApply(move)) continue;
            state.Undo(move);
            Assert.Equal(before, state.Snapshot().Fingerprint());
            Assert.Equal(costBefore, eval.EvaluateSoft(state));
            tested++;
        }
        Assert.True(tested > 100);
    }

    /// <summary>Muvaffaqiyatsiz TryApply holatni o'zgartirmaydi.</summary>
    [Fact]
    public void Failed_Apply_Leaves_State_Untouched()
    {
        var p = TestProblems.SmallSchool();
        var (state, _) = Prepare(p, 12);
        var rng = new Xoshiro256SS(555);
        var move = new Move();

        int failures = 0;
        for (int i = 0; i < 100_000 && failures < 200; i++)
        {
            if (!BuildRandomMove(state, rng, move)) continue;
            string before = state.Snapshot().Fingerprint();
            if (state.TryApply(move)) { state.Undo(move); continue; }
            Assert.Equal(before, state.Snapshot().Fingerprint());
            failures++;
        }
        Assert.True(failures > 10, $"kamida bir necha muvaffaqiyatsiz urinish kutilgan edi ({failures})");
    }

    /// <summary>T-A-07: occupancy bitmask'lari kartalar ro'yxatidan qayta hisoblangani bilan mos.</summary>
    [Fact]
    public void Occupancy_Is_Consistent_With_Placements()
    {
        var p = TestProblems.SmallSchool();
        var (state, _) = Prepare(p, 21);
        var snapshot = state.Snapshot();

        for (int t = 0; t < p.Teachers.Length; t++)
        {
            var expected = SlotMask.Empty;
            foreach (var cid in p.CardsOfTeacher[t])
            {
                int slot = snapshot.CardSlots[cid];
                if (slot >= 0) expected |= SlotMask.Range(slot, p.Cards[cid].Length);
            }
            Assert.Equal(expected, state.TeacherBusy(t));
        }

        for (int g = 0; g < p.Groups.Length; g++)
        {
            var expected = SlotMask.Empty;
            foreach (var cid in p.CardsOfGroup[g])
            {
                int slot = snapshot.CardSlots[cid];
                if (slot >= 0) expected |= SlotMask.Range(slot, p.Cards[cid].Length);
            }
            Assert.Equal(expected, state.GroupBusy(g));
        }
    }

    // ------------------------------------------------------------------

    private static (SolutionState, PenaltyEvaluator) Prepare(Problem p, int seed)
    {
        Propagator.ResetDomains(p);
        Propagator.Propagate(p);
        var eval = new PenaltyEvaluator(ConstraintSet.CreateDefault());
        var state = new SolutionState(p);
        var constructor = new Constructor(state, eval, new Xoshiro256SS(seed));
        constructor.Construct(50_000, CancellationToken.None);
        return (state, eval);
    }

    /// <summary>Tasodifiy SingleMove / Swap / KempeChain-ga o'xshash ko'p kartali ko'chish.</summary>
    private static bool BuildRandomMove(SolutionState state, Xoshiro256SS rng, Move move)
    {
        var p = state.Problem;
        var placed = new List<int>();
        for (int i = 0; i < p.Cards.Length; i++)
            if (state.IsPlaced(i)) placed.Add(i);
        if (placed.Count < 2) return false;

        double r = rng.NextDouble();
        if (r < 0.5)
        {
            int id = placed[rng.Next(placed.Count)];
            var card = p.Cards[id];
            int to = PickSlot(card.Domain, rng);
            if (to < 0 || to == state.SlotOfCard(id)) return false;
            move.Reset(MoveKind.SingleMove);
            move.Add(id, state.SlotOfCard(id), state.RoomOfCard(id), to, -1);
            return true;
        }

        if (r < 0.9)
        {
            int a = placed[rng.Next(placed.Count)];
            int b = placed[rng.Next(placed.Count)];
            if (a == b) return false;
            if (p.Cards[a].Length != p.Cards[b].Length) return false;
            int sa = state.SlotOfCard(a), sb = state.SlotOfCard(b);
            move.Reset(MoveKind.Swap);
            move.Add(a, sa, state.RoomOfCard(a), sb, -1);
            move.Add(b, sb, state.RoomOfCard(b), sa, -1);
            return true;
        }

        // Ko'p kartali (3 ta) tsiklik ko'chish
        int c1 = placed[rng.Next(placed.Count)];
        int c2 = placed[rng.Next(placed.Count)];
        int c3 = placed[rng.Next(placed.Count)];
        if (c1 == c2 || c2 == c3 || c1 == c3) return false;
        if (p.Cards[c1].Length != p.Cards[c2].Length || p.Cards[c2].Length != p.Cards[c3].Length) return false;
        move.Reset(MoveKind.KempeChain);
        move.Add(c1, state.SlotOfCard(c1), state.RoomOfCard(c1), state.SlotOfCard(c2), -1);
        move.Add(c2, state.SlotOfCard(c2), state.RoomOfCard(c2), state.SlotOfCard(c3), -1);
        move.Add(c3, state.SlotOfCard(c3), state.RoomOfCard(c3), state.SlotOfCard(c1), -1);
        return true;
    }

    private static int PickSlot(SlotMask domain, Xoshiro256SS rng)
    {
        int n = domain.PopCount();
        if (n == 0) return -1;
        int target = rng.Next(n);
        int s = domain.FirstSet();
        for (int i = 0; i < target; i++) s = domain.FirstSet(s + 1);
        return s;
    }
}
