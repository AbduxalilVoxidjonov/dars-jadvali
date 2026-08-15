using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>Faza 0 da topilgan xato (aSc "Verify specification", #1127/#1802).</summary>
public readonly record struct VerificationFault(string Code, string Message, string? ConstraintId = null);

/// <summary>Faza 0 hisoboti.</summary>
public sealed class VerificationReport
{
    private readonly List<VerificationFault> _faults = new();

    public IReadOnlyList<VerificationFault> Faults => _faults;
    public bool IsOk => _faults.Count == 0;

    internal void Add(string code, string message, string? constraintId = null)
        => _faults.Add(new VerificationFault(code, message, constraintId));

    public override string ToString()
        => IsOk ? "Tekshiruv muvaffaqiyatli." : string.Join(Environment.NewLine, _faults.Select(f => $"[{f.Code}] {f.Message}"));
}

/// <summary>
/// Faza 0 — VERIFY SPECIFICATION (02-asc-.., 4.2).
/// Arifmetik va Hall shartlarini tekshiradi; muvaffaqiyatsizlikda tushunarli xabar beradi.
/// </summary>
public static class Verifier
{
    public static VerificationReport Verify(Problem p)
    {
        var r = new VerificationReport();
        var grid = p.Grid;

        // --- 1. Har karta uchun domain bo'sh emasmi ---
        foreach (var c in p.Cards)
        {
            if (c.Domain.IsEmpty)
            {
                var l = p.Lessons[c.LessonId];
                r.Add("CARD_NO_DOMAIN",
                    $"'{l.Name}' darsi uchun ({c.Length} soatlik karta #{c.Id}) mos pozitsiya qolmadi: " +
                    $"time-off, kunlar cheklovi yoki xona mavjudligi juda qattiq.", "C-AVL-01..05");
            }
        }

        // --- 2. O'qituvchi haddan tashqari yuklangan (#4013/#4018) ---
        for (int t = 0; t < p.Teachers.Length; t++)
        {
            int demand = 0;
            foreach (var cid in p.CardsOfTeacher[t]) demand += p.Cards[cid].Length;
            int free = grid.FullMask.AndNot(p.Teachers[t].Availability.Forbidden).PopCount();
            if (demand > free)
                r.Add("TEACHER_OVERLOADED",
                    $"O'qituvchi '{p.Teachers[t].Name}' haddan tashqari yuklangan: {demand} soat kerak, " +
                    $"lekin faqat {free} ta bo'sh pozitsiya bor.", "C-GBL-01");
        }

        // --- 3. Sinf haddan tashqari yuklangan (bo'linishlar hisobga olinadi) ---
        for (int c = 0; c < p.Classes.Length; c++)
        {
            int demand = ClassPeriodDemand(p, c);
            int free = grid.FullMask.AndNot(p.Classes[c].Availability.Forbidden).PopCount();
            if (demand > free)
                r.Add("CLASS_OVERLOADED",
                    $"Sinf '{p.Classes[c].Name}' uchun {demand} soat kerak, lekin faqat {free} ta pozitsiya bor.",
                    "C-GBL-02");
        }

        // --- 4. Xona yetishmovchiligi (C-ROM-01) ---
        VerifyRooms(p, r);

        // --- 5. Haftada kunlardan ko'p marta (#4148, C-DST-15) ---
        foreach (var l in p.Lessons)
        {
            int cards = p.CardsOfLesson[l.Id].Length;
            var subj = p.Subjects[l.SubjectId];
            int days = l.AllowedDays?.Length ?? grid.TotalDays;
            if (subj.OncePerDay && cards > days)
                r.Add("TOO_FREQUENT",
                    $"'{l.Name}' darsi haftada {cards} marta o'tiladi, lekin faqat {days} kun mavjud " +
                    $"va fan bir kunda bir marta bo'lishi belgilangan.", "C-DST-05");
        }

        // --- 6. Qulflangan kartalar ziddiyati (C-GBL-06) ---
        VerifyLocked(p, r);

        // --- 7. Hall sharti: resurs kartalarining domain birlashmasi yetarlimi ---
        VerifyHall(p, r);

        return r;
    }

    private static int ClassPeriodDemand(Problem p, int classId)
    {
        // Bir xil divisiontag'li guruhlar parallel o'tadi → shu bo'linish ichida
        // eng ko'p yuklangan guruhning soati olinadi. Bo'linishlar esa qo'shiladi.
        var perTag = new Dictionary<int, Dictionary<int, int>>();
        foreach (var cid in p.CardsOfClass[classId])
        {
            var card = p.Cards[cid];
            int idx = Array.IndexOf(card.ClassIds, classId);
            int tag = card.ClassDivisionTags[idx];
            if (!perTag.TryGetValue(tag, out var byGroup))
            {
                byGroup = new Dictionary<int, int>();
                perTag[tag] = byGroup;
            }
            foreach (var g in card.GroupIds)
            {
                if (p.Groups[g].ClassId != classId) continue;
                byGroup.TryGetValue(g, out int v);
                byGroup[g] = v + card.Length;
            }
        }
        int total = 0;
        foreach (var kv in perTag)
        {
            int max = 0;
            foreach (var v in kv.Value.Values) if (v > max) max = v;
            total += max;
        }
        return total;
    }

    private static void VerifyRooms(Problem p, VerificationReport r)
    {
        if (p.Rooms.Length == 0) return;

        // Har xil "ruxsat etilgan xonalar to'plami" bo'yicha talab / imkoniyat.
        var demandBySet = new Dictionary<string, (int[] Rooms, int Demand, string Sample)>();
        foreach (var c in p.Cards)
        {
            if (!c.NeedsRoom) continue;
            var key = string.Join(',', c.AllowedRoomIds);
            var lessonName = p.Lessons[c.LessonId].Name;
            if (demandBySet.TryGetValue(key, out var e))
                demandBySet[key] = (e.Rooms, e.Demand + c.Length, e.Sample);
            else
                demandBySet[key] = (c.AllowedRoomIds, c.Length, lessonName);
        }

        foreach (var kv in demandBySet.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var (rooms, demand, sample) = kv.Value;
            if (rooms.Length == 0) continue;
            int capacity = 0;
            foreach (var rid in rooms)
                capacity += p.Grid.FullMask.AndNot(p.Rooms[rid].Availability.Forbidden).PopCount()
                            * Math.Max(1, p.Rooms[rid].ParallelLessons);
            if (demand > capacity)
            {
                var names = string.Join(", ", rooms.Select(x => p.Rooms[x].Name));
                r.Add("ROOM_SHORTAGE",
                    $"Xona yetishmaydi: '{sample}' kabi darslar uchun {demand} soat kerak, " +
                    $"lekin [{names}] xonalari jami {capacity} soat bera oladi.", "C-ROM-01");
            }
        }

        // Sig'im (C-ROM-02) — birorta ham mos xona qolmadimi.
        foreach (var l in p.Lessons)
        {
            if (l.AllowedRoomIds.Length == 0) continue;
            var cards = p.CardsOfLesson[l.Id];
            if (cards.Length == 0) continue;
            if (p.Cards[cards[0]].AllowedRoomIds.Length == 0)
                r.Add("ROOM_CAPACITY",
                    $"'{l.Name}' darsi uchun sig'imi yetadigan xona yo'q " +
                    $"({p.Cards[cards[0]].StudentCount} o'quvchi).", "C-ROM-02");
        }
    }

    private static void VerifyLocked(Problem p, VerificationReport r)
    {
        var state = new SolutionState(p);
        foreach (var c in p.Cards)
        {
            if (!c.IsLocked) continue;
            int room = c.LockedRoom >= 0 ? c.LockedRoom : state.FindRoom(c, c.LockedSlot);
            if (!state.CanPlace(c, c.LockedSlot, room))
            {
                r.Add("LOCKED_CONFLICT",
                    $"'{p.Lessons[c.LessonId].Name}' darsining qulflangan kartasi " +
                    $"(kun {p.Grid.DayOfSlot(c.LockedSlot)}, dars {p.Grid.PeriodOfSlot(c.LockedSlot)}) " +
                    $"boshqa qulflangan karta yoki taqiq bilan ziddiyatda.", "C-GBL-06");
                continue;
            }
            state.Place(c, c.LockedSlot, room);
        }
    }

    private static void VerifyHall(Problem p, VerificationReport r)
    {
        // O'qituvchilar
        for (int t = 0; t < p.Teachers.Length; t++)
        {
            var union = SlotMask.Empty;
            int demand = 0;
            foreach (var cid in p.CardsOfTeacher[t])
            {
                union |= p.Cards[cid].Domain;
                demand += p.Cards[cid].Length;
            }
            if (demand > 0 && union.PopCount() < demand)
                r.Add("HALL_TEACHER",
                    $"O'qituvchi '{p.Teachers[t].Name}': darslari uchun jami {union.PopCount()} ta " +
                    $"mumkin bo'lgan pozitsiya bor, lekin {demand} soat kerak.", "C-GBL-01");
        }

        // Guruhlar
        for (int g = 0; g < p.Groups.Length; g++)
        {
            var union = SlotMask.Empty;
            int demand = 0;
            foreach (var cid in p.CardsOfGroup[g])
            {
                union |= p.Cards[cid].Domain;
                demand += p.Cards[cid].Length;
            }
            if (demand > 0 && union.PopCount() < demand)
                r.Add("HALL_GROUP",
                    $"Guruh '{p.Groups[g].Name}': {union.PopCount()} ta mumkin pozitsiya, " +
                    $"lekin {demand} soat kerak.", "C-GBL-02");
        }
    }
}
