using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Evaluation;

/// <summary>Yechim narxi: leksikografik (joylanmagan, hard) &gt; soft.</summary>
public readonly record struct SolutionCost(int Unplaced, int HardViolations, long SoftCost)
{
    /// <summary>BigM — soft jarimalarning maksimal yig'indisidan katta (02-asc-.., 5.1).</summary>
    public const long BigM = 1_000_000_000L;

    public long Total => (long)(Unplaced + HardViolations) * BigM + SoftCost;

    public bool IsFeasible => Unplaced == 0 && HardViolations == 0;

    public override string ToString() => $"unplaced={Unplaced}, hard={HardViolations}, soft={SoftCost}";
}

/// <summary>
/// Baholash yadrosi: to'liq <see cref="Evaluate"/> va inkremental delta.
/// <b>Invariant:</b> <c>DeltaPenalty(m) == Evaluate(after) - Evaluate(before)</c>.
/// Buni kafolatlaydigan mexanizm — dekompozitsiya: har bir cheklov jarimasi mustaqil
/// <see cref="Scope"/> lar yig'indisi sifatida ifodalanadi, delta esa faqat
/// harakat ta'sir qilgan scope'larni "oldin/keyin" qayta hisoblaydi.
/// </summary>
public sealed class PenaltyEvaluator
{
    private readonly ConstraintSet _constraints;
    private readonly List<Scope> _allBuf = new();
    private readonly List<Scope> _flat = new();
    private readonly List<(int Start, int End)> _ranges = new();
    private readonly HashSet<Scope> _dedup = new();
    private long _before;
    private bool _deltaOpen;

    public PenaltyEvaluator(ConstraintSet constraints) => _constraints = constraints;

    public ConstraintSet Constraints => _constraints;

    /// <summary>To'liq soft jarima.</summary>
    public long EvaluateSoft(SolutionState state)
    {
        long sum = 0;
        var items = _constraints.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var c = items[i];
            if (!c.Enabled || c.IsHard) continue;
            sum += c.Evaluate(state);
        }
        return sum;
    }

    /// <summary>Har bir cheklov bo'yicha jarima taqsimoti (diagnostika, #3226).</summary>
    public IReadOnlyList<(string Id, string Name, long Penalty)> Breakdown(SolutionState state)
    {
        var res = new List<(string, string, long)>();
        foreach (var c in _constraints.Items)
        {
            if (!c.Enabled || c.IsHard) continue;
            res.Add((c.Id, c.Name, c.Evaluate(state)));
        }
        return res;
    }

    /// <summary>To'liq narx (hard buzilishlar qayta hisoblanadi).</summary>
    public SolutionCost Evaluate(SolutionState state)
    {
        var snapshot = state.Snapshot();
        int hard = HardRules.Check(snapshot);
        return new SolutionCost(state.UnplacedCount, hard, EvaluateSoft(state));
    }

    /// <summary>Tez narx: hard invariant saqlanadi deb faraz qilinadi (local search ichida).</summary>
    public SolutionCost FastCost(SolutionState state)
        => new(state.UnplacedCount, 0, EvaluateSoft(state));

    // ------------------------------------------------------------------
    // Inkremental delta
    // ------------------------------------------------------------------

    /// <summary>
    /// Delta hisoblashni boshlaydi: ta'sirlangan scope'larni yig'ib, "oldingi" qiymatlarni saqlaydi.
    /// Keyin harakatni qo'llash kerak, so'ng <see cref="EndDelta"/>.
    /// </summary>
    public void BeginDelta(SolutionState state, Move move)
    {
        _flat.Clear();
        _ranges.Clear();
        long before = 0;

        var items = _constraints.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var c = items[i];
            if (!c.Enabled || c.IsHard)
            {
                _ranges.Add((_flat.Count, _flat.Count));
                continue;
            }

            int start = _flat.Count;
            _dedup.Clear();
            var tmpStart = _flat.Count;
            c.AffectedScopes(state, move, _flat);
            // takrorlarni olib tashlash (delta ikki marta hisoblanmasligi uchun)
            int write = tmpStart;
            for (int r = tmpStart; r < _flat.Count; r++)
            {
                if (_dedup.Add(_flat[r]))
                    _flat[write++] = _flat[r];
            }
            _flat.RemoveRange(write, _flat.Count - write);

            for (int r = start; r < _flat.Count; r++)
                before += c.ScopeViolation(state, _flat[r]) * c.Weight;

            _ranges.Add((start, _flat.Count));
        }

        _before = before;
        _deltaOpen = true;
    }

    /// <summary>Harakat qo'llangandan keyingi delta.</summary>
    public long EndDelta(SolutionState state)
    {
        if (!_deltaOpen) throw new InvalidOperationException("BeginDelta chaqirilmagan.");
        long after = 0;
        var items = _constraints.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var c = items[i];
            var (start, end) = _ranges[i];
            for (int r = start; r < end; r++)
                after += c.ScopeViolation(state, _flat[r]) * c.Weight;
        }
        _deltaOpen = false;
        return after - _before;
    }

    /// <summary>BeginDelta'ni bekor qilish (harakat qo'llanmadi).</summary>
    public void AbortDelta() => _deltaOpen = false;

    /// <summary>
    /// Qulay yordamchi: harakatni qo'llab, delta'ni qaytaradi.
    /// Muvaffaqiyatsiz bo'lsa (hard cheklov) <c>false</c> va holat o'zgarmaydi.
    /// </summary>
    public bool TryApplyWithDelta(SolutionState state, Move move, out long delta)
    {
        BeginDelta(state, move);
        if (!state.TryApply(move))
        {
            AbortDelta();
            delta = 0;
            return false;
        }
        delta = EndDelta(state);
        return true;
    }
}
