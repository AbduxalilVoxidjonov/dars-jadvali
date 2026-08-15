using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>Yumshatish taklifi (#3226 "Relaxed constraints").</summary>
public readonly record struct RelaxationSuggestion(string ConstraintId, string Message, int CardId);

/// <summary>Faza 5 hisoboti.</summary>
public sealed class RelaxationReport
{
    private readonly List<RelaxationSuggestion> _items = new();

    public IReadOnlyList<RelaxationSuggestion> Suggestions => _items;
    public bool IsEmpty => _items.Count == 0;

    internal void Add(string id, string message, int cardId)
    {
        foreach (var s in _items)
            if (s.ConstraintId == id && s.Message == message) return;
        _items.Add(new RelaxationSuggestion(id, message, cardId));
    }

    public override string ToString()
        => IsEmpty ? "Yumshatish talab qilinmaydi."
                   : string.Join(Environment.NewLine, _items.Select(s => $"[{s.ConstraintId}] {s.Message}"));
}

/// <summary>
/// Faza 5 — RELAXATION (02-asc-.., 4.8; #3072, #2732, #3224).
/// To'liq yechim topilmaganda: har bir joylanmagan karta uchun QAYSI cheklovni yumshatish
/// yordam berishini aniqlaydi (cheklovni birma-bir "o'chirib ko'rish" — aSc #1832/#1833).
/// </summary>
public static class Relaxer
{
    public static RelaxationReport Analyze(SolutionState state)
    {
        var report = new RelaxationReport();
        var p = state.Problem;
        var grid = p.Grid;

        for (int cardId = 0; cardId < p.Cards.Length; cardId++)
        {
            if (state.IsPlaced(cardId)) continue;
            var card = p.Cards[cardId];
            var lesson = p.Lessons[card.LessonId];
            var startMask = grid.StartMaskForLength(card.Length);

            // Variant A — kunlar cheklovini (C-CYC-03) yumshatish.
            if (lesson.AllowedDays is not null &&
                HasFreeSlot(state, card, ProblemMask(state, card, ignoreDays: true) & startMask))
            {
                report.Add("C-CYC-03",
                    $"'{lesson.Name}': ruxsat etilgan kunlar ro'yxatini kengaytirish kerak.", cardId);
                continue;
            }

            // Variant B — o'qituvchi time-off (C-AVL-01).
            if (HasFreeSlot(state, card, ProblemMask(state, card, ignoreTeacherOff: true) & startMask))
            {
                report.Add("C-AVL-01",
                    $"'{lesson.Name}': o'qituvchining band vaqtlari (time-off) juda qattiq.", cardId);
                continue;
            }

            // Variant C — sinf/guruh time-off (C-AVL-02).
            if (HasFreeSlot(state, card, ProblemMask(state, card, ignoreClassOff: true) & startMask))
            {
                report.Add("C-AVL-02",
                    $"'{lesson.Name}': sinf/guruhning band vaqtlari juda qattiq.", cardId);
                continue;
            }

            // Variant D — fan time-off (C-AVL-03).
            if (HasFreeSlot(state, card, ProblemMask(state, card, ignoreSubjectOff: true) & startMask))
            {
                report.Add("C-AVL-03",
                    $"'{lesson.Name}': fanning vaqt cheklovi juda qattiq.", cardId);
                continue;
            }

            // Variant E — xona (C-ROM-01/02).
            if (card.NeedsRoom && HasFreeSlot(state, card, card.Domain, ignoreRoom: true))
            {
                report.Add("C-ROM-01/02",
                    $"'{lesson.Name}': xona yetishmaydi yoki sig'imi kichik.", cardId);
                continue;
            }

            // Variant F — qo'sh dars uzluksizligi (C-DBL-01).
            if (card.Length > 1 && HasFreeSlot(state, SingleUnitProbe(card), grid.FullMask))
            {
                report.Add("C-DBL-01",
                    $"'{lesson.Name}': {card.Length} soatlik qo'sh dars uchun uzluksiz joy yo'q — " +
                    $"uni alohida soatlarga bo'lish kerak.", cardId);
                continue;
            }

            report.Add("C-GBL-01/02",
                $"'{lesson.Name}': resurs to'qnashuvi — o'qituvchi yoki sinf jadvali to'lib ketgan. " +
                $"Dars soatini kamaytirish yoki o'quv kunini uzaytirish kerak.", cardId);
        }

        return report;
    }

    /// <summary>Cheklovlardan biri e'tiborga olinmagan holdagi maska.</summary>
    private static SlotMask ProblemMask(SolutionState state, Card card,
        bool ignoreDays = false, bool ignoreTeacherOff = false,
        bool ignoreClassOff = false, bool ignoreSubjectOff = false)
    {
        var p = state.Problem;
        var m = p.Grid.FullMask;

        if (!ignoreTeacherOff)
            foreach (var t in card.TeacherIds) m = m.AndNot(p.Teachers[t].Availability.Forbidden);

        if (!ignoreClassOff)
        {
            foreach (var c in card.ClassIds) m = m.AndNot(p.Classes[c].Availability.Forbidden);
            foreach (var g in card.GroupIds) m = m.AndNot(p.Groups[g].Availability.Forbidden);
        }

        if (!ignoreSubjectOff)
            m = m.AndNot(p.Subjects[card.SubjectId].Availability.Forbidden);

        if (!ignoreDays)
        {
            var lesson = p.Lessons[card.LessonId];
            if (lesson.AllowedDays is not null) m &= p.Grid.MaskForDays(lesson.AllowedDays);
        }

        return ProblemBuilder.Erode(m, card.Length, p.Grid.SlotCount);
    }

    private static Card SingleUnitProbe(Card card)
    {
        return new Card
        {
            Id = card.Id,
            LessonId = card.LessonId,
            SubjectId = card.SubjectId,
            Length = 1,
            TeacherIds = card.TeacherIds,
            GroupIds = card.GroupIds,
            ClassIds = card.ClassIds,
            ClassDivisionTags = card.ClassDivisionTags,
            AllowedRoomIds = card.AllowedRoomIds,
            StudentCount = card.StudentCount,
            Domain = card.BaseDomain,
        };
    }

    private static bool HasFreeSlot(SolutionState state, Card card, SlotMask candidates, bool ignoreRoom = false)
    {
        var original = card.Domain;
        try
        {
            card.Domain = candidates;
            for (int s = candidates.FirstSet(); s >= 0; s = candidates.FirstSet(s + 1))
            {
                if (ignoreRoom)
                {
                    var probe = SingleUnitProbe(card);
                    probe.Length = card.Length;
                    probe.AllowedRoomIds = Array.Empty<int>();
                    probe.Domain = candidates;
                    if (state.CanPlace(probe, s, -1)) return true;
                }
                else
                {
                    int room = state.FindRoom(card, s);
                    if (card.NeedsRoom && room < 0) continue;
                    if (state.CanPlace(card, s, room)) return true;
                }
            }
            return false;
        }
        finally
        {
            card.Domain = original;
        }
    }
}
