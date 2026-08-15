using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Constraints;

/// <summary>Buzilgan hard cheklov haqida ma'lumot.</summary>
public readonly record struct HardViolation(string ConstraintId, string Message);

/// <summary>
/// Hard cheklovlarni yechimdan MUSTAQIL ravishda qayta hisoblab tekshiradi
/// (occupancy bitmask'lariga tayanmaydi — shu sabab T-A-07 "occupancy konsistentligi" testi
/// haqiqiy tekshiruv bo'ladi).
/// </summary>
public static class HardRules
{
    /// <summary>
    /// Barcha hard buzilishlarni sanaydi:
    /// C-GBL-01 (teacher), C-GBL-02 (class/group), C-GBL-03 (room), C-GBL-06 (locked),
    /// C-GBL-08 (divisiontag), C-AVL-01..05 (forbidden), C-ROM-01/02, C-DBL-01.
    /// </summary>
    public static int Check(Solution solution, List<HardViolation>? output = null)
    {
        var p = solution.Problem;
        var grid = p.Grid;
        int slots = grid.SlotCount;
        int n = 0;

        var teacherAt = new int[p.Teachers.Length * slots];
        var groupAt = new int[p.Groups.Length * slots];
        var roomAt = new int[Math.Max(1, p.Rooms.Length) * slots];
        var classTagAt = new int[p.Classes.Length * slots];
        Array.Fill(teacherAt, -1);
        Array.Fill(groupAt, -1);
        Array.Fill(roomAt, -1);
        Array.Fill(classTagAt, -1);

        for (int cardId = 0; cardId < p.Cards.Length; cardId++)
        {
            int slot = solution.CardSlots[cardId];
            if (slot < 0) continue;
            var card = p.Cards[cardId];
            int room = solution.CardRooms[cardId];

            // C-DBL-01 — karta kun chegarasidan chiqmasligi kerak.
            if (grid.PeriodOfSlot(slot) + card.Length > grid.Periods)
            {
                n++; output?.Add(new HardViolation("C-DBL-01",
                    $"Karta #{cardId} ({card.Length} soatlik) kun chegarasidan chiqib ketdi."));
                continue;
            }

            // C-GBL-06 — qulflangan karta o'z joyida.
            if (card.IsLocked && slot != card.LockedSlot)
            {
                n++; output?.Add(new HardViolation("C-GBL-06",
                    $"Karta #{cardId} qulflangan, lekin noto'g'ri pozitsiyada."));
            }

            // C-AVL-01..05 — taqiqlangan pozitsiya (domain orqali).
            if (!card.BaseDomain.Test(slot))
            {
                n++; output?.Add(new HardViolation("C-AVL-01..05",
                    $"Karta #{cardId} taqiqlangan pozitsiyada (time-off yoki kun cheklovi)."));
            }

            for (int i = 0; i < card.Length; i++)
            {
                int s = slot + i;

                foreach (var t in card.TeacherIds)
                {
                    int idx = t * slots + s;
                    if (teacherAt[idx] >= 0)
                    {
                        n++; output?.Add(new HardViolation("C-GBL-01",
                            $"O'qituvchi '{p.Teachers[t].Name}' bir vaqtda ikkita darsda " +
                            $"(kun {grid.DayOfSlot(s)}, dars {grid.PeriodOfSlot(s)})."));
                    }
                    else teacherAt[idx] = cardId;
                }

                foreach (var g in card.GroupIds)
                {
                    int idx = g * slots + s;
                    if (groupAt[idx] >= 0)
                    {
                        n++; output?.Add(new HardViolation("C-GBL-02",
                            $"Guruh '{p.Groups[g].Name}' bir vaqtda ikkita darsda " +
                            $"(kun {grid.DayOfSlot(s)}, dars {grid.PeriodOfSlot(s)})."));
                    }
                    else groupAt[idx] = cardId;
                }

                for (int ci = 0; ci < card.ClassIds.Length; ci++)
                {
                    int cls = card.ClassIds[ci];
                    int tag = card.ClassDivisionTags[ci];
                    int idx = cls * slots + s;
                    if (classTagAt[idx] >= 0 && classTagAt[idx] != tag)
                    {
                        n++; output?.Add(new HardViolation("C-GBL-08",
                            $"Sinf '{p.Classes[cls].Name}': turli bo'linishlar (divisiontag {classTagAt[idx]} va {tag}) " +
                            $"bir vaqtda (kun {grid.DayOfSlot(s)}, dars {grid.PeriodOfSlot(s)})."));
                    }
                    else classTagAt[idx] = tag;
                }

                if (room >= 0)
                {
                    int idx = room * slots + s;
                    if (roomAt[idx] >= 0 && p.Rooms[room].ParallelLessons <= 1)
                    {
                        n++; output?.Add(new HardViolation("C-GBL-03",
                            $"Xona '{p.Rooms[room].Name}' da bir vaqtda ikkita dars."));
                    }
                    else roomAt[idx] = cardId;
                }
            }

            // C-ROM-01/02
            if (card.NeedsRoom)
            {
                if (room < 0)
                {
                    n++; output?.Add(new HardViolation("C-GBL-07", $"Karta #{cardId} uchun xona tayinlanmagan."));
                }
                else if (Array.IndexOf(card.AllowedRoomIds, room) < 0)
                {
                    n++; output?.Add(new HardViolation("C-ROM-01",
                        $"Karta #{cardId} ruxsat etilmagan xonada ('{p.Rooms[room].Name}')."));
                }
                else if (p.UseRoomCapacities && p.Rooms[room].Capacity > 0
                         && card.StudentCount > p.Rooms[room].Capacity)
                {
                    n++; output?.Add(new HardViolation("C-ROM-02",
                        $"Xona sig'imi yetarli emas: '{p.Rooms[room].Name}' ({p.Rooms[room].Capacity}) " +
                        $"< {card.StudentCount} o'quvchi."));
                }
            }
        }

        return n;
    }
}
