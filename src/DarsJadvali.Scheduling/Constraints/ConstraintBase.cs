using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Constraints;

/// <summary>Umumiy asos: <c>Evaluate</c> har doim scope'lar yig'indisi sifatida hisoblanadi.</summary>
public abstract class ConstraintBase : IConstraint
{
    private readonly List<Scope> _buf = new();

    public abstract string Id { get; }
    public abstract string Name { get; }
    public Importance Importance { get; set; } = Importance.Normal;
    public int Weight { get; set; } = 100;
    public bool Enabled { get; set; } = true;
    public bool AllowRelaxation { get; set; } = true;
    public bool IsHard => Importance == Importance.Strict;

    public abstract void EnumerateScopes(SolutionState state, List<Scope> output);
    public abstract void AffectedScopes(SolutionState state, Move move, List<Scope> output);
    public abstract long ScopeViolation(SolutionState state, in Scope scope);

    public long Evaluate(SolutionState state)
    {
        if (!Enabled) return 0;
        _buf.Clear();
        EnumerateScopes(state, _buf);
        long sum = 0;
        for (int i = 0; i < _buf.Count; i++)
            sum += ScopeViolation(state, _buf[i]);
        return sum * Weight;
    }

    // ---- yordamchilar ----

    /// <summary>Harakatdagi kartalarga tegishli (resurs, kun) juftliklarini yig'ish uchun kunlar.</summary>
    protected static void CollectDays(SolutionState s, Move m, int index, Span<int> days, out int count)
    {
        count = 0;
        var grid = s.Grid;
        int from = m.FromSlot(index);
        int to = m.ToSlot(index);
        if (from >= 0) days[count++] = grid.DayOfSlot(from);
        if (to >= 0)
        {
            int d = grid.DayOfSlot(to);
            bool dup = false;
            for (int i = 0; i < count; i++) if (days[i] == d) dup = true;
            if (!dup) days[count++] = d;
        }
    }
}
