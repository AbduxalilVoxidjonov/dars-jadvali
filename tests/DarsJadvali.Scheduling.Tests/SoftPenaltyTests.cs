using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Evaluation;
using DarsJadvali.Scheduling.Model;
using Xunit;

namespace DarsJadvali.Scheduling.Tests;

public class SoftPenaltyTests
{
    private static (Problem, SolutionState) BuildSingleClass(
        int periodsPerWeek, int days = 5, int periods = 6, Action<ProblemBuilder, TeacherDef, ClassDef, SubjectDef>? cfg = null)
    {
        var b = new ProblemBuilder(new TimeGrid(days, periods));
        var t = b.AddTeacher("Ali");
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s = b.AddSubject("Matematika");
        cfg?.Invoke(b, t, cls, s);
        b.AddLesson(s, new[] { t }, new[] { g }, periodsPerWeek);
        var p = b.Build();
        return (p, new SolutionState(p));
    }

    /// <summary>T-S-01: o'qituvchida 1- va 6-dars → 4 ta oyna.</summary>
    [Fact]
    public void Teacher_Gaps_Are_Penalised()
    {
        var (p, state) = BuildSingleClass(2, cfg: (_, t, _, _) => t.MaxGapsPerDay = 0);
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        state.Place(p.Cards[1], p.Grid.SlotOf(0, 5), -1);

        var c = new TeacherGapsPerDayConstraint();
        Assert.Equal(4 * 300, c.Evaluate(state));
    }

    [Fact]
    public void Teacher_Gaps_Zero_When_Within_Limit()
    {
        var (p, state) = BuildSingleClass(2, cfg: (_, t, _, _) => t.MaxGapsPerDay = 4);
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        state.Place(p.Cards[1], p.Grid.SlotOf(0, 5), -1);
        Assert.Equal(0, new TeacherGapsPerDayConstraint().Evaluate(state));
    }

    /// <summary>T-S-06: sinfda oyna → C-CLS-01 (w = 800).</summary>
    [Fact]
    public void Class_Gap_Costs_800_Per_Window()
    {
        var (p, state) = BuildSingleClass(2);
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        state.Place(p.Cards[1], p.Grid.SlotOf(0, 2), -1);

        var c = new ClassGapsConstraint();
        Assert.Equal(800, c.Weight);
        Assert.Equal(800, c.Evaluate(state));
    }

    [Fact]
    public void Class_Without_Gaps_Has_Zero_Penalty()
    {
        var (p, state) = BuildSingleClass(2);
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        state.Place(p.Cards[1], p.Grid.SlotOf(0, 1), -1);
        Assert.Equal(0, new ClassGapsConstraint().Evaluate(state));
    }

    /// <summary>T-S-02: 5 ta ketma-ket dars, maxConsec = 4 → (5-4)^2 = 1.</summary>
    [Fact]
    public void Teacher_Consecutive_Penalty_Is_Quadratic()
    {
        var (p, state) = BuildSingleClass(5, periods: 8, cfg: (_, t, _, _) => t.MaxConsecutivePeriods = 4);
        for (int i = 0; i < 5; i++) state.Place(p.Cards[i], p.Grid.SlotOf(0, i), -1);
        Assert.Equal(1 * 400, new TeacherMaxConsecutiveConstraint().Evaluate(state));
    }

    /// <summary>T-S-04: fan haftada 5 marta, 5 kunda bittadan → taqsimot jarimasi 0.</summary>
    [Fact]
    public void Perfect_Weekly_Distribution_Has_Zero_Penalty()
    {
        var (p, state) = BuildSingleClass(5);
        for (int d = 0; d < 5; d++) state.Place(p.Cards[d], p.Grid.SlotOf(d, 0), -1);
        Assert.Equal(0, new EquableDistributionConstraint().Evaluate(state));
        Assert.Equal(0, new SubjectOncePerDayConstraint().Evaluate(state));
    }

    /// <summary>T-S-03: haftada 2 marta, ketma-ket kunlarda → jarima &gt; 0.</summary>
    [Fact]
    public void Adjacent_Days_Are_Penalised_When_Spread_Is_Possible()
    {
        var (p, state) = BuildSingleClass(2);
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        state.Place(p.Cards[1], p.Grid.SlotOf(1, 0), -1);
        long adjacent = new EquableDistributionConstraint().Evaluate(state);
        Assert.True(adjacent > 0, "ketma-ket kunlar jarimalanishi kerak");

        state.Unplace(p.Cards[1]);
        state.Place(p.Cards[1], p.Grid.SlotOf(3, 0), -1);
        Assert.Equal(0, new EquableDistributionConstraint().Evaluate(state));
    }

    /// <summary>C-DST-05: bir kunda ikki marta → jarima.</summary>
    [Fact]
    public void Same_Day_Repeat_Is_Penalised()
    {
        var (p, state) = BuildSingleClass(2);
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        state.Place(p.Cards[1], p.Grid.SlotOf(0, 1), -1);

        Assert.Equal(600, new SubjectOncePerDayConstraint().Evaluate(state));
        Assert.True(new EquableDistributionConstraint().Evaluate(state) > 0);
    }

    /// <summary>T-S-05: `?` belgilangan pozitsiya → C-AVL-06 = 100.</summary>
    [Fact]
    public void QuestionMarked_Position_Costs_100()
    {
        var (p, state) = BuildSingleClass(1, cfg: (bb, t, _, _) =>
            t.Availability.Set(bb.Grid, 0, 5, AvailabilityState.Questioned));

        state.Place(p.Cards[0], p.Grid.SlotOf(0, 5), -1);
        Assert.Equal(100, new QuestionMarkedPositionConstraint().Evaluate(state));

        state.Unplace(p.Cards[0]);
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        Assert.Equal(0, new QuestionMarkedPositionConstraint().Evaluate(state));
    }

    /// <summary>C-CLS-03: kunlik maksimal darslar.</summary>
    [Fact]
    public void Class_Daily_Max_Is_Penalised()
    {
        var (p, state) = BuildSingleClass(4, cfg: (_, _, cls, _) => cls.MaxLessonsPerDay = 2);
        for (int i = 0; i < 4; i++) state.Place(p.Cards[i], p.Grid.SlotOf(0, i), -1);
        Assert.Equal(2 * 400, new ClassDailyLoadConstraint().Evaluate(state));
    }

    /// <summary>C-TCH-07: o'qituvchining bo'sh kuni (haftada 4 kundan ko'p emas).</summary>
    [Fact]
    public void Teacher_Free_Day_Requirement()
    {
        var (p, state) = BuildSingleClass(5, cfg: (_, t, _, _) => t.MaxDaysPerWeek = 4);
        for (int d = 0; d < 5; d++) state.Place(p.Cards[d], p.Grid.SlotOf(d, 0), -1);
        Assert.Equal(400, new TeacherDaysTaughtConstraint().Evaluate(state));

        state.Unplace(p.Cards[4]);
        state.Place(p.Cards[4], p.Grid.SlotOf(0, 2), -1);
        Assert.Equal(0, new TeacherDaysTaughtConstraint().Evaluate(state));
    }

    /// <summary>To'liq baholash = scope'lar yig'indisi (dekompozitsiya invarianti).</summary>
    [Fact]
    public void Evaluate_Equals_Sum_Of_Scope_Violations()
    {
        var (p, state) = BuildSingleClass(5, cfg: (_, t, _, _) =>
        {
            t.MaxGapsPerDay = 0;
            t.MaxConsecutivePeriods = 2;
            t.MaxDaysPerWeek = 3;
        });
        state.Place(p.Cards[0], p.Grid.SlotOf(0, 0), -1);
        state.Place(p.Cards[1], p.Grid.SlotOf(0, 1), -1);
        state.Place(p.Cards[2], p.Grid.SlotOf(0, 2), -1);
        state.Place(p.Cards[3], p.Grid.SlotOf(1, 4), -1);
        state.Place(p.Cards[4], p.Grid.SlotOf(2, 0), -1);

        var set = ConstraintSet.CreateDefault();
        var eval = new PenaltyEvaluator(set);
        long total = eval.EvaluateSoft(state);

        long manual = 0;
        var scopes = new List<Scope>();
        foreach (var c in set.Items)
        {
            scopes.Clear();
            c.EnumerateScopes(state, scopes);
            foreach (var s in scopes) manual += c.ScopeViolation(state, s) * c.Weight;
        }
        Assert.Equal(manual, total);
    }
}
