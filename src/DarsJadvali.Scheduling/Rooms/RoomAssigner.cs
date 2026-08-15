using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Rooms;

/// <summary>
/// Xona tayinlash — ALOHIDA faza (aSc `#1836`, `#1855`).
/// Har slot uchun kartalar x ruxsat etilgan xonalar bipartite grafi quriladi va
/// Hopcroft–Karp bilan maksimal moslik topiladi (02-asc-.., 4.5).
/// Xonasiz maktabda (bo'sh xona ro'yxati) faza umuman ishlamaydi — cheklov yo'q.
/// </summary>
public static class RoomAssigner
{
    /// <summary>
    /// Barcha joylashtirilgan kartalar uchun xonalarni qayta taqsimlaydi.
    /// Qaytadi: xona topilmagan kartalar soni.
    /// </summary>
    public static int AssignAll(SolutionState state)
    {
        var p = state.Problem;
        if (p.Rooms.Length == 0) return 0;

        int failed = 0;
        var byStart = new List<int>[p.Grid.SlotCount];

        for (int i = 0; i < p.Cards.Length; i++)
        {
            int slot = state.SlotOfCard(i);
            if (slot < 0 || !p.Cards[i].NeedsRoom) continue;
            (byStart[slot] ??= new List<int>()).Add(i);
        }

        for (int slot = 0; slot < p.Grid.SlotCount; slot++)
        {
            var cards = byStart[slot];
            if (cards is null || cards.Count == 0) continue;

            // 1) Xonalarni bo'shatish (kartalar joyida qoladi).
            var savedSlots = new int[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                savedSlots[i] = state.SlotOfCard(cards[i]);
                state.Unplace(p.Cards[cards[i]]);
            }

            // 2) O'ng tugunlar: (xona, nusxa) juftliklari.
            var rightRoom = new List<int>();
            var seen = new HashSet<int>();
            foreach (var cid in cards)
                foreach (var r in p.Cards[cid].AllowedRoomIds)
                    if (seen.Add(r))
                        for (int k = 0; k < Math.Max(1, p.Rooms[r].ParallelLessons); k++)
                            rightRoom.Add(r);

            var adjacency = new IReadOnlyList<int>[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                var card = p.Cards[cards[i]];
                var list = new List<int>();
                for (int j = 0; j < rightRoom.Count; j++)
                    if (state.IsRoomAvailable(card, savedSlots[i], rightRoom[j]))
                        list.Add(j);
                adjacency[i] = list;
            }

            var match = HopcroftKarp.Match(cards.Count, rightRoom.Count, adjacency, out _);

            // 3) Natijani qo'llash.
            for (int i = 0; i < cards.Count; i++)
            {
                var card = p.Cards[cards[i]];
                int room = match[i] >= 0 ? rightRoom[match[i]] : -1;
                if (room < 0) room = state.FindRoom(card, savedSlots[i]);
                if (room < 0)
                {
                    failed++;
                    continue;   // karta joylanmagan holda qoladi
                }
                state.Place(card, savedSlots[i], room);
            }
        }

        return failed;
    }
}
