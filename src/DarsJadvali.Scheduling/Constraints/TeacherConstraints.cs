using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Constraints;

/// <summary>(o'qituvchi, kun) scope'li cheklovlar uchun umumiy asos.</summary>
public abstract class TeacherDayConstraint : ConstraintBase
{
    public override void EnumerateScopes(SolutionState state, List<Scope> output)
    {
        var p = state.Problem;
        for (int t = 0; t < p.Teachers.Length; t++)
            for (int d = 0; d < p.Grid.TotalDays; d++)
                output.Add(new Scope(ScopeKind.TeacherDay, t, d));
    }

    public override void AffectedScopes(SolutionState state, Move move, List<Scope> output)
    {
        Span<int> days = stackalloc int[2];
        for (int i = 0; i < move.Count; i++)
        {
            CollectDays(state, move, i, days, out int n);
            var card = state.Problem.Cards[move.CardId(i)];
            foreach (var t in card.TeacherIds)
                for (int k = 0; k < n; k++)
                    output.Add(new Scope(ScopeKind.TeacherDay, t, days[k]));
        }
    }
}

/// <summary>(o'qituvchi, hafta) scope'li cheklovlar uchun umumiy asos.</summary>
public abstract class TeacherWeekConstraint : ConstraintBase
{
    public override void EnumerateScopes(SolutionState state, List<Scope> output)
    {
        var p = state.Problem;
        for (int t = 0; t < p.Teachers.Length; t++)
            for (int w = 0; w < p.Grid.Weeks; w++)
                output.Add(new Scope(ScopeKind.TeacherWeek, t, w));
    }

    public override void AffectedScopes(SolutionState state, Move move, List<Scope> output)
    {
        Span<int> days = stackalloc int[2];
        var grid = state.Grid;
        for (int i = 0; i < move.Count; i++)
        {
            CollectDays(state, move, i, days, out int n);
            var card = state.Problem.Cards[move.CardId(i)];
            foreach (var t in card.TeacherIds)
                for (int k = 0; k < n; k++)
                    output.Add(new Scope(ScopeKind.TeacherWeek, t, grid.WeekOfDay(days[k])));
        }
    }
}

/// <summary>C-TCH-02 — o'qituvchining kunlik oynalari (max windows per day, #2662/#3460).</summary>
public sealed class TeacherGapsPerDayConstraint : TeacherDayConstraint
{
    public override string Id => "C-TCH-02";
    public override string Name => "O'qituvchining kunlik oynalari";

    public TeacherGapsPerDayConstraint() { Weight = 300; Importance = Importance.Normal; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var t = state.Problem.Teachers[scope.A];
        if (t.MaxGapsPerDay < 0) return 0;
        int gaps = DayBits.Gaps(state.TeacherDayBits(scope.A, scope.B));
        return Math.Max(0, gaps - t.MaxGapsPerDay);
    }
}

/// <summary>C-TCH-01 — o'qituvchining haftalik oynalari (#1210/#1212/#3461, fault #1819).</summary>
public sealed class TeacherGapsPerWeekConstraint : TeacherWeekConstraint
{
    public override string Id => "C-TCH-01";
    public override string Name => "O'qituvchining haftalik oynalari";

    public TeacherGapsPerWeekConstraint() { Weight = 300; Importance = Importance.Normal; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var p = state.Problem;
        var t = p.Teachers[scope.A];
        if (t.MaxGapsPerWeek < 0) return 0;
        int total = 0;
        int start = scope.B * p.Grid.DaysPerWeek;
        for (int d = start; d < start + p.Grid.DaysPerWeek; d++)
            total += DayBits.Gaps(state.TeacherDayBits(scope.A, d));
        return Math.Max(0, total - t.MaxGapsPerWeek);
    }
}

/// <summary>
/// C-TCH-10 — ketma-ket darslar chegarasi (#1217..#1219, #3472, fault #1851).
/// Kvadratik jarima: <c>Sum_run max(0, len - maxConsec)^2</c>.
/// </summary>
public sealed class TeacherMaxConsecutiveConstraint : TeacherDayConstraint
{
    public override string Id => "C-TCH-10";
    public override string Name => "O'qituvchining ketma-ket darslari";

    public TeacherMaxConsecutiveConstraint() { Weight = 400; Importance = Importance.Normal; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var t = state.Problem.Teachers[scope.A];
        if (t.MaxConsecutivePeriods < 0) return 0;
        return DayBits.ConsecutivePenalty(state.TeacherDayBits(scope.A, scope.B), t.MaxConsecutivePeriods);
    }
}

/// <summary>C-TCH-14 / C-TCH-15 — kunlik min/max darslar (#3453, #3454; bo'sh kun ruxsat).</summary>
public sealed class TeacherDailyLoadConstraint : TeacherDayConstraint
{
    public override string Id => "C-TCH-14/15";
    public override string Name => "O'qituvchining kunlik yuki (min/max)";

    public TeacherDailyLoadConstraint() { Weight = 300; Importance = Importance.Normal; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var t = state.Problem.Teachers[scope.A];
        if (t.MaxPeriodsPerDay < 0 && t.MinPeriodsPerDay < 0) return 0;
        int count = DayBits.Count(state.TeacherDayBits(scope.A, scope.B));
        long v = 0;
        if (t.MaxPeriodsPerDay >= 0) v += Math.Max(0, count - t.MaxPeriodsPerDay);
        if (t.MinPeriodsPerDay >= 0 && count > 0) v += Math.Max(0, t.MinPeriodsPerDay - count);
        return v;
    }
}

/// <summary>
/// C-TCH-07 / C-TCH-08 — haftalik dars kunlari soni (#1211/#3458/#3459, fault #1820).
/// "O'qituvchining bo'sh kuni" talabi <c>MaxDaysPerWeek = DaysPerWeek - 1</c> orqali ifodalanadi.
/// </summary>
public sealed class TeacherDaysTaughtConstraint : TeacherWeekConstraint
{
    public override string Id => "C-TCH-07/08";
    public override string Name => "O'qituvchining dars kunlari soni (bo'sh kun)";

    public TeacherDaysTaughtConstraint() { Weight = 400; Importance = Importance.Normal; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var p = state.Problem;
        var t = p.Teachers[scope.A];
        if (t.MaxDaysPerWeek < 0 && t.MinDaysPerWeek < 0) return 0;
        int used = 0;
        int start = scope.B * p.Grid.DaysPerWeek;
        for (int d = start; d < start + p.Grid.DaysPerWeek; d++)
            if (state.TeacherDayBits(scope.A, d) != 0UL) used++;
        long v = 0;
        if (t.MaxDaysPerWeek >= 0) v += Math.Max(0, used - t.MaxDaysPerWeek);
        if (t.MinDaysPerWeek >= 0) v += Math.Max(0, t.MinDaysPerWeek - used);
        return v;
    }
}
