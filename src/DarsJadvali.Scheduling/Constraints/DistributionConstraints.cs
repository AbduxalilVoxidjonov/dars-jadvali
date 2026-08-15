using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Constraints;

/// <summary>(sinf, fan) scope'li cheklovlar uchun umumiy asos.</summary>
public abstract class ClassSubjectConstraint : ConstraintBase
{
    public override void EnumerateScopes(SolutionState state, List<Scope> output)
    {
        foreach (var (cls, subj) in state.Problem.ClassSubjectPairs)
            output.Add(new Scope(ScopeKind.ClassSubject, cls, subj));
    }

    public override void AffectedScopes(SolutionState state, Move move, List<Scope> output)
    {
        for (int i = 0; i < move.Count; i++)
        {
            var card = state.Problem.Cards[move.CardId(i)];
            foreach (var cls in card.ClassIds)
                output.Add(new Scope(ScopeKind.ClassSubject, cls, card.SubjectId));
        }
    }

    /// <summary>
    /// (sinf, fan) juftligi bo'yicha kunlik dars sanoqlari.
    /// <b>Muhim:</b> bir xil slotdagi parallel guruh kartalari (divisiontag) BITTA dars sifatida
    /// sanaladi — aks holda bo'lingan darslar sun'iy ravishda "bir kunda ikki marta" bo'lib ko'rinardi.
    /// </summary>
    protected static void FillDayCounts(SolutionState state, in Scope scope, Span<int> counts, out int total)
    {
        counts.Clear();
        total = 0;
        var p = state.Problem;

        var starts = SlotMask.Empty;
        foreach (var cardId in p.CardsForClassSubject(scope.A, scope.B))
        {
            var card = p.Cards[cardId];
            if (card.SkipDistribution) continue;   // C-DST-04
            int slot = state.SlotOfCard(cardId);
            if (slot < 0) continue;
            starts = starts.Set(slot);
        }

        var grid = p.Grid;
        for (int d = 0; d < grid.TotalDays; d++)
        {
            int n = System.Numerics.BitOperations.PopCount(starts.Extract(grid.DayStart(d), grid.Periods));
            counts[d] = n;
            total += n;
        }
    }
}

/// <summary>
/// C-DST-05 — fan bir kunda faqat bir marta (#3888, fault #1824 "%1 more times per day").
/// </summary>
public sealed class SubjectOncePerDayConstraint : ClassSubjectConstraint
{
    public override string Id => "C-DST-05";
    public override string Name => "Fan bir kunda bir marta";

    public SubjectOncePerDayConstraint() { Weight = 600; Importance = Importance.High; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var subj = state.Problem.Subjects[scope.B];
        if (!subj.OncePerDay) return 0;
        Span<int> counts = stackalloc int[state.Grid.TotalDays];
        FillDayCounts(state, scope, counts, out int total);
        if (total == 0) return 0;
        long v = 0;
        for (int d = 0; d < counts.Length; d++)
            if (counts[d] > 1) v += counts[d] - 1;
        return v;
    }
}

/// <summary>
/// C-DST-01 — haftalik tekis taqsimot (#2027, #2306, #2312, #2313; fault #1739).
/// Daraja: None / Low / Medium / Ideal (#3727..#3730).
/// <c>v = SameDayUnit * extras + Sum (idealGap - actualGap)^2</c> (02-asc-.., 5.2).
/// </summary>
public sealed class EquableDistributionConstraint : ClassSubjectConstraint
{
    private const int SameDayUnit = 4;

    public override string Id => "C-DST-01";
    public override string Name => "Haftalik tekis taqsimot";

    public EquableDistributionConstraint() { Weight = 500; Importance = Importance.High; }

    public override long ScopeViolation(SolutionState state, in Scope scope)
    {
        var grid = state.Grid;
        var subj = state.Problem.Subjects[scope.B];
        var level = subj.Distribution;
        if (level == DistributionLevel.None) return 0;

        Span<int> counts = stackalloc int[grid.TotalDays];
        FillDayCounts(state, scope, counts, out int total);
        if (total == 0) return 0;

        long v = 0;
        for (int w = 0; w < grid.Weeks; w++)
        {
            int start = w * grid.DaysPerWeek;
            int end = start + grid.DaysPerWeek;

            int n = 0;
            for (int d = start; d < end; d++) n += counts[d];
            if (n == 0) continue;

            for (int d = start; d < end; d++)
                if (counts[d] > 1) v += SameDayUnit * (counts[d] - 1);

            if (level >= DistributionLevel.Medium && n > 1)
            {
                // idealGap = Days / lessonsPerWeek, 10x aniqlikda butun sonlarda.
                int ideal10 = 10 * grid.DaysPerWeek / n;
                int prev = -1;
                for (int d = start; d < end; d++)
                {
                    if (counts[d] == 0) continue;
                    if (prev >= 0)
                    {
                        int dev = ideal10 - 10 * (d - prev);
                        if (dev > 0) v += (long)dev * dev / 100;
                    }
                    prev = d;
                }
            }
        }

        if (level == DistributionLevel.Ideal) v *= 2;
        return v;
    }
}
