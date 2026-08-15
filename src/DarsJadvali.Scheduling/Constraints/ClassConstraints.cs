using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Constraints;

/// <summary>(sinf, kun) scope'li cheklovlar uchun umumiy asos.</summary>
public abstract class ClassDayConstraint : ConstraintBase
{
    public override void EnumerateScopes(SolutionState state, List<Scope> output)
    {
        var p = state.Problem;
        for (int c = 0; c < p.Classes.Length; c++)
            for (int d = 0; d < p.Grid.TotalDays; d++)
                output.Add(new Scope(ScopeKind.ClassDay, c, d));
    }

    public override void AffectedScopes(SolutionState state, Move move, List<Scope> output)
    {
        Span<int> days = stackalloc int[2];
        for (int i = 0; i < move.Count; i++)
        {
            CollectDays(state, move, i, days, out int n);
            var card = state.Problem.Cards[move.CardId(i)];
            foreach (var cls in card.ClassIds)
                for (int k = 0; k < n; k++)
                    output.Add(new Scope(ScopeKind.ClassDay, cls, days[k]));
        }
    }
}

/// <summary>
/// C-CLS-01 — sinfda oyna bo'lmasligi kerak (fault #1821). Eng qimmat soft cheklov, w = 800.
/// Sinf bandligi = uning barcha guruhlari bandligining birlashmasi.
/// </summary>
public sealed class ClassGapsConstraint : ClassDayConstraint
{
    public override string Id => "C-CLS-01";
    public override string Name => "Sinf jadvalidagi oynalar";

    public ClassGapsConstraint() { Weight = 800; Importance = Importance.High; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var cls = state.Problem.Classes[scope.A];
        int gaps = DayBits.Gaps(state.ClassDayBits(scope.A, scope.B));
        return Math.Max(0, gaps - Math.Max(0, cls.MaxGapsPerDay));
    }
}

/// <summary>C-CLS-03 — sinfning kunlik min/max darslari (#2650, #2651; fault #2308).</summary>
public sealed class ClassDailyLoadConstraint : ClassDayConstraint
{
    public override string Id => "C-CLS-03";
    public override string Name => "Sinfning kunlik darslari (min/max)";

    public ClassDailyLoadConstraint() { Weight = 400; Importance = Importance.Normal; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var cls = state.Problem.Classes[scope.A];
        if (cls.MaxLessonsPerDay < 0 && cls.MinLessonsPerDay < 0) return 0;
        int count = DayBits.Count(state.ClassDayBits(scope.A, scope.B));
        long v = 0;
        if (cls.MaxLessonsPerDay >= 0) v += Math.Max(0, count - cls.MaxLessonsPerDay);
        if (cls.MinLessonsPerDay >= 0 && count > 0) v += Math.Max(0, cls.MinLessonsPerDay - count);
        return v;
    }
}

/// <summary>
/// C-AVL-06 — `?` belgilangan pozitsiya jarimasi (#1744, #3500, tip #18:
/// "allowed, but it is not good"). Har bir band `?` soati uchun 1 birlik.
/// </summary>
public sealed class QuestionMarkedPositionConstraint : ConstraintBase
{
    public override string Id => "C-AVL-06";
    public override string Name => "`?` belgilangan pozitsiyalar";

    public QuestionMarkedPositionConstraint() { Weight = 100; Importance = Importance.Normal; }

    public override void EnumerateScopes(SolutionState state, List<Scope> output)
    {
        for (int i = 0; i < state.CardCount; i++)
            output.Add(new Scope(ScopeKind.Card, i, 0));
    }

    public override void AffectedScopes(SolutionState state, Move move, List<Scope> output)
    {
        for (int i = 0; i < move.Count; i++)
            output.Add(new Scope(ScopeKind.Card, move.CardId(i), 0));
    }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        int slot = state.SlotOfCard(scope.A);
        if (slot < 0) return 0;
        var card = state.Problem.Cards[scope.A];
        return (card.QuestionMarked & SlotMask.Range(slot, card.Length)).PopCount();
    }
}
