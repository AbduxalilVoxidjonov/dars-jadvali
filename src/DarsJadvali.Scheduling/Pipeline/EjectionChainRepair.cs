using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Util;

namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>
/// Faza 3 — EJECTION CHAIN / MIN-CONFLICTS REPAIR (02-asc-.., 4.6).
/// "Kick out and reinsert": joylanmagan kartani majburan qo'yib, to'qnashganlarni chiqarib
/// tashlaydi va zanjir bo'ylab qayta joylaydi. <see cref="Card.ConflictCount"/> — aging (#3982).
/// </summary>
public sealed class EjectionChainRepair
{
    private readonly SolutionState _state;
    private readonly Xoshiro256SS _rng;
    private readonly int _maxDepth;
    private readonly int _maxVictims;
    private readonly int[] _slotBuf;
    private int _budget;

    public EjectionChainRepair(SolutionState state, Xoshiro256SS rng, int maxDepth, int maxVictims = 3)
    {
        _state = state;
        _rng = rng;
        _maxDepth = Math.Max(1, maxDepth);
        _maxVictims = maxVictims;
        _slotBuf = new int[state.Grid.SlotCount];
    }

    /// <summary>Joylanmagan kartalarni zanjir orqali joylashtirishga urinadi.</summary>
    public int Repair(int budget, CancellationToken ct)
    {
        _budget = budget;
        int fixedCount = 0;
        var p = _state.Problem;

        var unplaced = new List<int>();
        for (int i = 0; i < p.Cards.Length; i++)
            if (!_state.IsPlaced(i)) unplaced.Add(i);

        // Eng qiyin (ko'p konfliktli) kartalar oldin.
        unplaced.Sort((a, b) =>
        {
            int cmp = p.Cards[b].ConflictCount.CompareTo(p.Cards[a].ConflictCount);
            return cmp != 0 ? cmp : a.CompareTo(b);
        });

        foreach (var id in unplaced)
        {
            if (ct.IsCancellationRequested || _budget <= 0) break;
            if (_state.IsPlaced(id)) continue;
            var snapshot = _state.Snapshot();
            if (TryInsert(p.Cards[id], 0, ct)) fixedCount++;
            else _state.RestoreFrom(snapshot);
        }
        return fixedCount;
    }

    private bool TryInsert(Card card, int depth, CancellationToken ct)
    {
        if (_budget-- <= 0 || ct.IsCancellationRequested) return false;

        // 1) To'g'ridan-to'g'ri bo'sh joy bormi.
        var feasible = _state.FeasibleSlots(card);
        if (!feasible.IsEmpty)
        {
            int chosen = PickRandomSet(feasible);
            int room = _state.FindRoom(card, chosen);
            if (!card.NeedsRoom || room >= 0)
            {
                _state.Place(card, chosen, room);
                return true;
            }
        }

        if (depth >= _maxDepth) return false;

        // 2) Majburiy joylash — qurbonlarni chiqarib tashlash (kick-out chain).
        int n = CollectSlots(card.Domain);
        _rng.Shuffle(_slotBuf, n);
        int tried = 0;

        for (int i = 0; i < n && tried < 8; i++)
        {
            int slot = _slotBuf[i];
            var victims = CollectVictims(card, slot);
            if (victims.Count == 0 || victims.Count > _maxVictims) continue;
            tried++;

            var snapshot = _state.Snapshot();
            foreach (var v in victims) _state.Unplace(_state.Problem.Cards[v]);

            int room = _state.FindRoom(card, slot);
            if ((card.NeedsRoom && room < 0) || !_state.CanPlace(card, slot, room))
            {
                _state.RestoreFrom(snapshot);
                continue;
            }
            _state.Place(card, slot, room);

            bool ok = true;
            foreach (var v in victims)
            {
                _state.Problem.Cards[v].ConflictCount++;
                if (!TryInsert(_state.Problem.Cards[v], depth + 1, ct)) { ok = false; break; }
            }
            if (ok) return true;
            _state.RestoreFrom(snapshot);
        }
        return false;
    }

    private int CollectSlots(SlotMask mask)
    {
        int n = 0;
        for (int s = mask.FirstSet(); s >= 0 && n < _slotBuf.Length; s = mask.FirstSet(s + 1))
            _slotBuf[n++] = s;
        return n;
    }

    private int PickRandomSet(SlotMask mask)
    {
        int n = mask.PopCount();
        int target = _rng.Next(n);
        int s = mask.FirstSet();
        for (int i = 0; i < target; i++) s = mask.FirstSet(s + 1);
        return s;
    }

    /// <summary>Kartani <paramref name="slot"/> ga qo'yish uchun chiqarilishi kerak bo'lgan kartalar.</summary>
    private List<int> CollectVictims(Card card, int slot)
    {
        var res = new List<int>(4);
        var p = _state.Problem;
        var extent = _state.Extent(slot, card.Length);

        foreach (var nb in p.Neighbors[card.Id])
        {
            int s = _state.SlotOfCard(nb);
            if (s < 0) continue;
            var other = p.Cards[nb];
            if (other.IsLocked) return new List<int>();   // qulflangan kartani chiqarib bo'lmaydi
            if (_state.Extent(s, other.Length).Intersects(extent)) res.Add(nb);
        }

        // Xona to'qnashuvi (C-GBL-03)
        if (card.NeedsRoom && res.Count <= _maxVictims)
        {
            bool anyFree = false;
            foreach (var r in card.AllowedRoomIds)
            {
                if (!_state.RoomBusy(r).Intersects(extent)) { anyFree = true; break; }
            }
            if (!anyFree) return new List<int>();
        }
        return res;
    }
}
