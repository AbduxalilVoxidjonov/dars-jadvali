using System.Diagnostics;
using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace DarsJadvali.Scheduling.Tests;

/// <summary>T-A-05 — anytime + cancellation.</summary>
public class CancellationTests
{
    private readonly ITestOutputHelper _out;

    public CancellationTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Cancelled_Generation_Still_Returns_Hard_Feasible_Solution()
    {
        var p = TestProblems.SmallSchool(classCount: 12);
        using var cts = new CancellationTokenSource();
        var progressSeen = 0;
        var progress = new Progress<GenerationProgress>(_ => Interlocked.Increment(ref progressSeen));

        // Optimize fazasi boshlangach bekor qilamiz.
        var options = new GenerationOptions
        {
            Seed = 17,
            Complexity = Complexity.Huge,          // ataylab katta byudjet
            ProgressInterval = TimeSpan.FromMilliseconds(1),
        };

        var clock = Stopwatch.StartNew();
        cts.CancelAfter(TimeSpan.FromMilliseconds(400));
        var result = new Scheduler().Generate(p, options, progress, cts.Token);
        clock.Stop();

        _out.WriteLine($"Bekor qilingandan keyin: {result}");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"juda uzoq davom etdi: {clock.Elapsed}");
        Assert.Equal(0, result.Cost.HardViolations);
        Assert.Empty(result.HardViolations);
        Assert.True(result.Solution.PlacedCount > 0);
    }

    [Fact]
    public void Already_Cancelled_Token_Returns_Immediately_With_Valid_State()
    {
        var p = TestProblems.SmallSchool();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 2 }, null, cts.Token);
        Assert.True(result.Cancelled);
        Assert.Equal(0, HardRules.Check(result.Solution));
    }

    [Fact]
    public void TimeLimit_Bounds_Generation()
    {
        var p = TestProblems.SmallSchool(classCount: 12);
        var clock = Stopwatch.StartNew();
        var result = new Scheduler().Generate(p, new GenerationOptions
        {
            Seed = 4,
            Complexity = Complexity.Huge,
            TimeLimit = TimeSpan.FromMilliseconds(500),
        });
        clock.Stop();

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"vaqt chegarasi ishlamadi: {clock.Elapsed}");
        Assert.Equal(0, result.Cost.HardViolations);
    }

    [Fact]
    public void Progress_Reports_Are_Delivered()
    {
        var p = TestProblems.SmallSchool();
        var reports = new List<GenerationProgress>();
        var progress = new Progress<GenerationProgress>(reports.Add);

        new Scheduler().Generate(p, new GenerationOptions
        {
            Seed = 6,
            Complexity = Complexity.Small,
            ProgressInterval = TimeSpan.Zero,
        }, progress);

        // Progress<T> asinxron — biroz kutamiz.
        for (int i = 0; i < 50 && reports.Count == 0; i++) Thread.Sleep(10);
        Assert.NotEmpty(reports);
    }
}
