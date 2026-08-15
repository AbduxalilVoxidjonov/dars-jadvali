using DarsJadvali.Scheduling.Evaluation;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Util;

namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>
/// Faza 2 — CONSTRUCTIVE PLACEMENT (randomized MRV + degree, 02-asc-.., 4.4).
/// Karta tanlash: MRV (eng kam mumkin bo'lgan slot) + degree + aging (#3982) tie-break.
/// Slot tanlash: eng kam soft jarima o'sishi + shovqin (randomized).
/// </summary>
public sealed class Constructor
{
    private readonly SolutionState _state;
    private readonly PenaltyEvaluator _eval;
    private readonly Xoshiro256SS _rng;
    private readonly Move _move = new();
    private readonly int[] _candidates;

    /// <summary>MRV uchun tanlanadigan nomzod kartalar soni (randomized sampling).</summary>
    private const int SampleSize = 20;

    /// <summary>Bitta karta uchun tekshiriladigan maksimal slot soni.</summary>
    private const int MaxSlotProbe = 96;

    public Constructor(SolutionState state, PenaltyEvaluator evaluator, Xoshiro256SS rng)
    {
        _state = state;
        _eval = evaluator;
        _rng = rng;
        _candidates = new int[state.CardCount];
    }

    public int Backtracks { get; private set; }

    /// <summary>Barcha kartalarni joylashtirishga urinadi. <c>true</c> = to'liq.</summary>
    public bool Construct(int maxBacktracks, CancellationToken ct)
    {
        var p = _state.Problem;
        _state.ClearAll();
        if (!_state.PlaceLockedCards()) return false;

        var unplaced = new List<int>(p.Cards.Length);
        foreach (var c in p.Cards)
            if (!_state.IsPlaced(c.Id)) unplaced.Add(c.Id);

        Propagator.ComputeDifficulty(p);
        unplaced.Sort((a, b) =>
        {
            int cmp = p.Cards[b].Difficulty.CompareTo(p.Cards[a].Difficulty);
            return cmp != 0 ? cmp : a.CompareTo(b);
        });

        Backtracks = 0;
        while (unplaced.Count > 0)
        {
            if (ct.IsCancellationRequested) return false;

            int pick = SelectCard(unplaced);
            int cardId = unplaced[pick];
            var card = p.Cards[cardId];

            if (!TryPlaceBest(card))
            {
                Backtracks++;
                card.ConflictCount++;
                if (Backtracks > maxBacktracks) return false;
                Eject(card, unplaced);
                continue;
            }

            unplaced.RemoveAt(pick);
        }
        return true;
    }

    /// <summary>MRV: namuna ichidan eng kam feasible slotli kartani tanlaydi.</summary>
    private int SelectCard(List<int> unplaced)
    {
        var p = _state.Problem;
        int n = unplaced.Count;
        int samples = Math.Min(SampleSize, n);
        int bestIdx = 0;
        long bestScore = long.MaxValue;

        for (int i = 0; i < samples; i++)
        {
            int idx = samples == n ? i : _rng.Next(n);
            var card = p.Cards[unplaced[idx]];
            int feasible = _state.FeasibleSlots(card).PopCount();
            // MRV birinchi o'rinda, keyin degree/difficulty (kamaytiruvchi).
            long score = (long)feasible * 100000L - (long)(card.Difficulty * 10.0) - card.ConflictCount;
            if (score < bestScore)
            {
                bestScore = score;
                bestIdx = idx;
            }
        }
        return bestIdx;
    }

    /// <summary>Kartani eng kam soft jarima beruvchi slotga qo'yadi.</summary>
    private bool TryPlaceBest(Card card)
    {
        var feasible = _state.FeasibleSlots(card);
        if (feasible.IsEmpty) return false;

        int count = 0;
        for (int s = feasible.FirstSet(); s >= 0 && count < _candidates.Length; s = feasible.FirstSet(s + 1))
            _candidates[count++] = s;

        if (count > MaxSlotProbe)
        {
            _rng.Shuffle(_candidates, count);
            count = MaxSlotProbe;
        }

        long best = long.MaxValue;
        int bestSlot = -1;
        int bestRoom = -1;

        for (int i = 0; i < count; i++)
        {
            int slot = _candidates[i];
            int room = _state.FindRoom(card, slot);
            if (card.NeedsRoom && room < 0) continue;

            _move.Reset(MoveKind.SingleMove);
            _move.Add(card.Id, -1, -1, slot, room);
            if (!_eval.TryApplyWithDelta(_state, _move, out long delta)) continue;
            _state.Undo(_move);

            long score = delta + (long)(_rng.NextDouble() * 200.0);
            if (score < best)
            {
                best = score;
                bestSlot = slot;
                bestRoom = room;
            }
        }

        if (bestSlot < 0) return false;
        _state.Place(card, bestSlot, bestRoom);
        return true;
    }

    /// <summary>
    /// Qisman randomized backtrack: kartani to'sib turgan qo'shnilardan bir nechtasini olib tashlaydi
    /// (kick-out) va ularni qayta joylashtirish navbatiga qo'shadi.
    /// </summary>
    private void Eject(Card card, List<int> unplaced)
    {
        var p = _state.Problem;
        var neighbors = p.Neighbors[card.Id];
        int k = 1 + _rng.Next(3);
        int tries = 0;
        while (k > 0 && tries < 32 && neighbors.Length > 0)
        {
            tries++;
            int nb = neighbors[_rng.Next(neighbors.Length)];
            var other = p.Cards[nb];
            if (!_state.IsPlaced(nb) || other.IsLocked) continue;
            _state.Unplace(other);
            other.ConflictCount++;
            unplaced.Add(nb);
            k--;
        }
    }
}
