using System.Diagnostics;
using DarsJadvali.Scheduling.Evaluation;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Util;

namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>
/// Faza 4 — SOFT-CONSTRAINT OPTIMIZATION (02-asc-.., 4.7).
/// Simulated annealing + tabu gibrid. Neighborhood: SingleMove, Swap, RoomChange, BlockSwap, KempeChain.
/// Hard invariant hech qachon buzilmaydi (<see cref="SolutionState.TryApply"/> kafolatlaydi).
/// </summary>
public sealed class Optimizer
{
    private readonly SolutionState _state;
    private readonly PenaltyEvaluator _eval;
    private readonly Xoshiro256SS _rng;
    private readonly GenerationOptions _options;
    private readonly Move _move = new();
    private readonly int[] _tabuUntil;
    private readonly int _slots;
    private readonly List<int> _placed = new();
    private readonly List<int> _chain = new();
    private readonly HashSet<int> _chainSet = new();

    public Optimizer(SolutionState state, PenaltyEvaluator eval, Xoshiro256SS rng, GenerationOptions options)
    {
        _state = state;
        _eval = eval;
        _rng = rng;
        _options = options;
        _slots = state.Grid.SlotCount;
        _tabuUntil = new int[state.CardCount * _slots];
    }

    public long BestCost { get; private set; }
    public int Iterations { get; private set; }

    /// <summary>Optimizatsiya. Anytime: bekor qilinganda ham eng yaxshi topilgan yechim qaytadi.</summary>
    public Solution Optimize(
        int maxIterations,
        Stopwatch clock,
        TimeSpan? timeLimit,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct)
    {
        RefreshPlaced();
        long current = _eval.EvaluateSoft(_state);
        var best = _state.Snapshot();
        BestCost = current;

        double temperature = _options.InitialTemperature;
        int tenure = _options.TabuTenure;
        var lastReport = TimeSpan.Zero;
        int sinceImprovement = 0;

        for (int it = 0; it < maxIterations; it++)
        {
            Iterations = it;

            if ((it & 0x3FF) == 0)
            {
                if (ct.IsCancellationRequested) break;
                if (timeLimit.HasValue && clock.Elapsed > timeLimit.Value) break;
                if (progress is not null && clock.Elapsed - lastReport >= _options.ProgressInterval)
                {
                    lastReport = clock.Elapsed;
                    progress.Report(new GenerationProgress(GenerationPhase.Optimize, it,
                        _state.PlacedCount, _state.CardCount, current, BestCost,
                        _state.UnplacedCount, clock.Elapsed));
                }
            }

            if (!BuildMove(it)) continue;

            bool tabu = IsTabu(it);
            if (!_eval.TryApplyWithDelta(_state, _move, out long delta)) continue;

            bool aspiration = current + delta < BestCost;
            bool accept;
            if (tabu && !aspiration)
            {
                accept = false;
            }
            else if (delta <= 0)
            {
                accept = true;
            }
            else
            {
                double t = Math.Max(temperature, 1e-6);
                accept = _rng.NextDouble() < Math.Exp(-delta / t);
            }

            if (!accept)
            {
                _state.Undo(_move);
            }
            else
            {
                current += delta;
                MarkTabu(it + tenure + _rng.Next(8));
                if (current < BestCost)
                {
                    BestCost = current;
                    best = _state.Snapshot();
                    sinceImprovement = 0;
                }
            }

            sinceImprovement++;
            temperature *= _options.CoolingRate;
            if (sinceImprovement > 50_000)
            {
                temperature = _options.InitialTemperature * 0.5;   // reheat
                sinceImprovement = 0;
            }
        }

        _state.RestoreFrom(best);
        return best;
    }

    // ------------------------------------------------------------------
    // Neighborhood
    // ------------------------------------------------------------------

    private bool BuildMove(int iteration)
    {
        if (_placed.Count == 0) return false;

        double r = _rng.NextDouble();
        if (r < 0.35) return BuildSingleMove();
        if (r < 0.65) return BuildSwap();
        if (r < 0.75) return BuildRoomChange();
        if (r < 0.85) return BuildBlockSwap();
        return BuildKempeChain();
    }

    private void RefreshPlaced()
    {
        _placed.Clear();
        var p = _state.Problem;
        for (int i = 0; i < p.Cards.Length; i++)
            if (_state.IsPlaced(i) && !p.Cards[i].IsLocked) _placed.Add(i);
    }

    private bool BuildSingleMove()
    {
        int cardId = _placed[_rng.Next(_placed.Count)];
        var card = _state.Problem.Cards[cardId];
        int from = _state.SlotOfCard(cardId);
        int to = PickSlot(card.Domain);
        if (to < 0 || to == from) return false;

        _move.Reset(MoveKind.SingleMove);
        _move.Add(cardId, from, _state.RoomOfCard(cardId), to, -1);
        return true;
    }

    private bool BuildSwap()
    {
        int a = _placed[_rng.Next(_placed.Count)];
        var cardA = _state.Problem.Cards[a];
        var neighbors = _state.Problem.Neighbors[a];
        int b;
        if (neighbors.Length > 0 && _rng.NextDouble() < 0.8)
        {
            b = neighbors[_rng.Next(neighbors.Length)];
            if (!_state.IsPlaced(b) || _state.Problem.Cards[b].IsLocked) return false;
        }
        else
        {
            b = _placed[_rng.Next(_placed.Count)];
        }
        if (a == b) return false;

        var cardB = _state.Problem.Cards[b];
        if (cardA.Length != cardB.Length) return false;

        int sa = _state.SlotOfCard(a);
        int sb = _state.SlotOfCard(b);
        if (!cardA.Domain.Test(sb) || !cardB.Domain.Test(sa)) return false;

        _move.Reset(MoveKind.Swap);
        _move.Add(a, sa, _state.RoomOfCard(a), sb, -1);
        _move.Add(b, sb, _state.RoomOfCard(b), sa, -1);
        return true;
    }

    private bool BuildRoomChange()
    {
        int cardId = _placed[_rng.Next(_placed.Count)];
        var card = _state.Problem.Cards[cardId];
        if (card.AllowedRoomIds.Length < 2) return false;
        int slot = _state.SlotOfCard(cardId);
        int newRoom = card.AllowedRoomIds[_rng.Next(card.AllowedRoomIds.Length)];
        if (newRoom == _state.RoomOfCard(cardId)) return false;

        _move.Reset(MoveKind.RoomChange);
        _move.Add(cardId, slot, _state.RoomOfCard(cardId), slot, newRoom);
        return true;
    }

    private bool BuildBlockSwap()
    {
        var p = _state.Problem;
        if (p.Classes.Length == 0) return false;
        int cls = _rng.Next(p.Classes.Length);
        var grid = _state.Grid;
        if (grid.TotalDays < 2) return false;
        int d1 = _rng.Next(grid.TotalDays);
        int d2 = _rng.Next(grid.TotalDays);
        if (d1 == d2) return false;
        int shift = (d2 - d1) * grid.Periods;

        _move.Reset(MoveKind.BlockSwap);
        foreach (var cardId in p.CardsOfClass[cls])
        {
            int slot = _state.SlotOfCard(cardId);
            if (slot < 0) continue;
            var card = p.Cards[cardId];
            if (card.IsLocked) return false;
            int day = grid.DayOfSlot(slot);
            if (day == d1) _move.Add(cardId, slot, _state.RoomOfCard(cardId), slot + shift, -1);
            else if (day == d2) _move.Add(cardId, slot, _state.RoomOfCard(cardId), slot - shift, -1);
        }
        return _move.Count > 0;
    }

    /// <summary>
    /// Kempe chain: ikki slot orasidagi to'qnashuv grafi komponentini butunlay almashtirish.
    /// Hard cheklovlarni saqlagan holda katta sakrash beradi (02-asc-.., 4.7).
    /// </summary>
    private bool BuildKempeChain()
    {
        var p = _state.Problem;
        int seed = _placed[_rng.Next(_placed.Count)];
        var seedCard = p.Cards[seed];
        int s1 = _state.SlotOfCard(seed);
        int s2 = PickSlot(seedCard.Domain);
        if (s2 < 0 || s2 == s1) return false;

        _chain.Clear();
        _chainSet.Clear();
        _chain.Add(seed);
        _chainSet.Add(seed);

        for (int i = 0; i < _chain.Count && _chain.Count <= 16; i++)
        {
            int cur = _chain[i];
            var card = p.Cards[cur];
            if (card.IsLocked || card.Length != seedCard.Length) return false;
            int from = _state.SlotOfCard(cur);
            int to = from == s1 ? s2 : s1;
            var extent = _state.Extent(to, card.Length);

            foreach (var nb in p.Neighbors[cur])
            {
                if (_chainSet.Contains(nb)) continue;
                int ns = _state.SlotOfCard(nb);
                if (ns < 0) continue;
                if (ns != s1 && ns != s2) continue;
                if (!_state.Extent(ns, p.Cards[nb].Length).Intersects(extent)) continue;
                _chain.Add(nb);
                _chainSet.Add(nb);
            }
        }
        if (_chain.Count > 16) return false;

        _move.Reset(MoveKind.KempeChain);
        foreach (var id in _chain)
        {
            var card = p.Cards[id];
            if (card.IsLocked) return false;
            int from = _state.SlotOfCard(id);
            int to = from == s1 ? s2 : s1;
            if (!card.Domain.Test(to)) return false;
            _move.Add(id, from, _state.RoomOfCard(id), to, -1);
        }
        return _move.Count > 0;
    }

    private int PickSlot(SlotMask domain)
    {
        int n = domain.PopCount();
        if (n == 0) return -1;
        int target = _rng.Next(n);
        int s = domain.FirstSet();
        for (int i = 0; i < target; i++) s = domain.FirstSet(s + 1);
        return s;
    }

    // ------------------------------------------------------------------
    // Tabu
    // ------------------------------------------------------------------

    private bool IsTabu(int iteration)
    {
        for (int i = 0; i < _move.Count; i++)
        {
            int idx = _move.CardId(i) * _slots + _move.ToSlot(i);
            if (idx >= 0 && _tabuUntil[idx] > iteration) return true;
        }
        return false;
    }

    private void MarkTabu(int until)
    {
        for (int i = 0; i < _move.Count; i++)
        {
            int from = _move.FromSlot(i);
            if (from < 0) continue;
            _tabuUntil[_move.CardId(i) * _slots + from] = until;
        }
    }
}
