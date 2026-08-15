using System.Diagnostics;
using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace DarsJadvali.Scheduling.Tests;

public class IntegrationTests
{
    private readonly ITestOutputHelper _out;

    public IntegrationTests(ITestOutputHelper output) => _out = output;

    /// <summary>T-I-01: kichik maktab — 6 sinf, 5 kun x 7 dars, hard buzilishsiz to'liq yechim.</summary>
    [Fact]
    public void SmallSchool_Is_Fully_Scheduled()
    {
        var p = TestProblems.SmallSchool();
        Assert.Equal(6 * 25, p.Cards.Length);

        var clock = Stopwatch.StartNew();
        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 2024, Complexity = Complexity.Normal });
        clock.Stop();

        _out.WriteLine($"6 sinf: {result}");
        foreach (var (id, name, penalty) in result.PenaltyBreakdown.Where(x => x.Penalty > 0))
            _out.WriteLine($"  {id} {name}: {penalty}");

        Assert.True(result.IsComplete, result.ToString());
        Assert.Empty(result.HardViolations);
        Assert.Equal(p.Cards.Length, result.Solution.PlacedCount);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60));
    }

    /// <summary>T-I-02: o'rta maktab — 20 sinf.</summary>
    [Fact]
    public void MediumSchool_Is_Fully_Scheduled()
    {
        var p = TestProblems.SmallSchool(classCount: 20, days: 5, periods: 8);
        var clock = Stopwatch.StartNew();
        var result = new Scheduler().Generate(p, new GenerationOptions
        {
            Seed = 77,
            Complexity = Complexity.Normal,
            MaxOptimizeIterations = 200_000,
        });
        clock.Stop();

        _out.WriteLine($"20 sinf: {result}");
        Assert.Empty(result.HardViolations);
        Assert.True(result.PlacedPercent >= 99.0, $"joylashgan: {result.PlacedPercent:F1}%");
    }

    /// <summary>T-A-06: optimizatsiya jarimani oshirmaydi (eng yaxshi natija monoton).</summary>
    [Fact]
    public void Optimization_Does_Not_Increase_Soft_Cost()
    {
        var p = TestProblems.SmallSchool();

        var noOpt = new Scheduler().Generate(p, new GenerationOptions
        {
            Seed = 31,
            Complexity = Complexity.Small,
            MaxOptimizeIterations = 0,
        });

        var p2 = TestProblems.SmallSchool();
        var withOpt = new Scheduler().Generate(p2, new GenerationOptions
        {
            Seed = 31,
            Complexity = Complexity.Small,
            MaxOptimizeIterations = 200_000,
        });

        _out.WriteLine($"optimizatsiyasiz: {noOpt.Cost.SoftCost}, optimizatsiya bilan: {withOpt.Cost.SoftCost}");
        Assert.True(withOpt.Cost.SoftCost <= noOpt.Cost.SoftCost);
        Assert.Empty(withOpt.HardViolations);
    }

    /// <summary>T-I-09: qisman qulflangan jadval — qulflangan kartalar qimirlamaydi.</summary>
    [Fact]
    public void Partially_Locked_Timetable_Keeps_Locked_Cards()
    {
        var b = new Model.ProblemBuilder(new Model.TimeGrid(5, 7));
        string[] names = { "Matematika", "Ona tili", "Ingliz tili", "Tarix", "Biologiya" };
        int[] hours = { 5, 4, 3, 3, 3 };
        var subjects = names.Select(b.AddSubject).ToArray();

        var lockedExpectation = new List<(int LessonId, int Day, int Period)>();
        for (int ci = 0; ci < 4; ci++)
        {
            var cls = b.AddClass($"7-{(char)('A' + ci)}", 28);
            var whole = b.AddEntireClassGroup(cls);
            for (int si = 0; si < subjects.Length; si++)
            {
                var t = b.AddTeacher($"O'q-{ci}-{si}");
                var lesson = b.AddLesson(subjects[si], new[] { t }, new[] { whole }, hours[si]);
                if (si < 3)
                {
                    // Har sinfda 3 ta dars qulflanadi — turli kunlarda, turli soatlarda.
                    lesson.Locked.Add(new Model.FixedPlacement(si, ci));
                    lockedExpectation.Add((lesson.Id, si, ci));
                }
            }
        }
        var p = b.Build();

        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 12, Complexity = Complexity.Normal });

        _out.WriteLine($"Qulflangan jadval: {result}");
        Assert.Empty(result.HardViolations);
        Assert.True(result.IsComplete, result.ToString());

        foreach (var (lessonId, day, period) in lockedExpectation)
        {
            int cardId = p.CardsOfLesson[lessonId][0];
            Assert.True(p.Cards[cardId].IsLocked);
            Assert.Equal(p.Grid.SlotOf(day, period), result.Solution.CardSlots[cardId]);
        }
    }
}
