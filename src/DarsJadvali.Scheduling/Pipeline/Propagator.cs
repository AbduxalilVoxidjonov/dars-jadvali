using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>Faza 1 natijasi.</summary>
public readonly record struct PropagationResult(bool Feasible, int RemovedSlots, int FailedCardId);

/// <summary>
/// Faza 1 — PREPROCESSING / DOMAIN REDUCTION (AC-3 uslubi, 02-asc-.., 4.3).
/// Singleton propagation (unit propagation) + qulflangan kartalardan kaskad.
/// </summary>
public static class Propagator
{
    /// <summary>Domain'larni <see cref="Card.BaseDomain"/> dan tiklaydi.</summary>
    public static void ResetDomains(Problem p)
    {
        foreach (var c in p.Cards) c.Domain = c.BaseDomain;
    }

    /// <summary>AC-3 uslubidagi domain qisqartirish.</summary>
    public static PropagationResult Propagate(Problem p)
    {
        int removed = 0;
        var assigned = new bool[p.Cards.Length];
        var queue = new Queue<int>();
        var inQueue = new bool[p.Cards.Length];

        foreach (var c in p.Cards)
        {
            if (c.Domain.IsEmpty) return new PropagationResult(false, removed, c.Id);
            if (c.Domain.PopCount() == 1)
            {
                queue.Enqueue(c.Id);
                inQueue[c.Id] = true;
            }
        }

        var neighbors = p.Neighbors;

        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            inQueue[id] = false;
            if (assigned[id]) continue;
            var card = p.Cards[id];
            int slot = card.Domain.FirstSet();
            if (slot < 0) return new PropagationResult(false, removed, id);
            assigned[id] = true;

            foreach (var nb in neighbors[id])
            {
                if (assigned[nb]) continue;
                var other = p.Cards[nb];
                // other ning [s, s+Lo) oralig'i card ning [slot, slot+Lc) bilan kesishmasligi kerak.
                int lo = slot - other.Length + 1;
                int hi = slot + card.Length - 1;
                var before = other.Domain;
                var dom = before;
                for (int s = Math.Max(0, lo); s <= hi; s++)
                    dom = dom.Clear(s);
                if (dom != before)
                {
                    removed += before.PopCount() - dom.PopCount();
                    other.Domain = dom;
                    if (dom.IsEmpty) return new PropagationResult(false, removed, nb);
                    if (dom.PopCount() == 1 && !inQueue[nb])
                    {
                        queue.Enqueue(nb);
                        inQueue[nb] = true;
                    }
                }
            }
        }

        return new PropagationResult(true, removed, -1);
    }

    /// <summary>Kartalarning qiyinlik bahosi: MRV + degree + uzunlik + xona cheklovi + aging (#3982).</summary>
    public static void ComputeDifficulty(Problem p)
    {
        foreach (var c in p.Cards)
        {
            int dom = Math.Max(1, c.Domain.PopCount());
            double rooms = c.AllowedRoomIds.Length == 0 ? 0.0 : 1.0 / c.AllowedRoomIds.Length;
            c.Difficulty = 1000.0 / dom
                         + 0.01 * c.Degree
                         + 2.0 * c.Length
                         + 5.0 * rooms
                         + 3.0 * c.ConflictCount;
        }
    }
}
