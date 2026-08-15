using System.Diagnostics;
using DarsJadvali.Scheduling.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace DarsJadvali.Scheduling.Tests;

/// <summary>
/// Katta stsenariy benchmark'i: 30 sinf x 150 guruh x ~1170 karta.
/// Odatiy test yugurishida qisqa byudjet bilan ishlaydi (~3 s).
/// To'liq o'lchov uchun: <c>DJ_BENCH=1 dotnet test --filter Category=Benchmark</c>.
/// </summary>
[Trait("Category", "Benchmark")]
public class BenchmarkTests
{
    private readonly ITestOutputHelper _out;

    public BenchmarkTests(ITestOutputHelper output) => _out = output;

    private static bool FullRun => Environment.GetEnvironmentVariable("DJ_BENCH") == "1";

    [Fact]
    [Trait("Category", "Benchmark")]
    public void LargeSchool_30Classes_150Groups()
    {
        var p = TestProblems.LargeSchool();

        _out.WriteLine($"Sinflar: {p.Classes.Length}, guruhlar: {p.Groups.Length}, " +
                       $"o'qituvchilar: {p.Teachers.Length}, kartalar: {p.Cards.Length}, " +
                       $"slotlar: {p.Grid.SlotCount}");

        Assert.Equal(30, p.Classes.Length);
        Assert.Equal(150, p.Groups.Length);
        Assert.True(p.Cards.Length >= 1100, $"kartalar: {p.Cards.Length}");

        var verifyClock = Stopwatch.StartNew();
        var verification = Verifier.Verify(p);
        verifyClock.Stop();
        _out.WriteLine($"Faza 0 (Verify): {verifyClock.ElapsedMilliseconds} ms, " +
                       $"xatolar: {verification.Faults.Count}");
        foreach (var f in verification.Faults.Take(5)) _out.WriteLine($"  {f.Code}: {f.Message}");

        var options = new GenerationOptions
        {
            Seed = 20240814,
            Complexity = FullRun ? Complexity.Large : Complexity.Normal,
            TimeLimit = FullRun ? TimeSpan.FromSeconds(90) : TimeSpan.FromSeconds(4),
        };

        var clock = Stopwatch.StartNew();
        var result = new Scheduler().Generate(p, options);
        clock.Stop();

        _out.WriteLine("------------------------------------------------------------");
        _out.WriteLine($"Vaqt:            {clock.Elapsed.TotalSeconds:F2} s");
        _out.WriteLine($"Joylashgan:      {result.Solution.PlacedCount}/{p.Cards.Length} ({result.PlacedPercent:F2}%)");
        _out.WriteLine($"Hard buzilish:   {result.Cost.HardViolations}");
        _out.WriteLine($"Soft jarima:     {result.Cost.SoftCost}");
        _out.WriteLine($"Restartlar:      {result.RestartsUsed}");
        _out.WriteLine($"SA iteratsiya:   {result.OptimizeIterations}");
        _out.WriteLine("Jarima taqsimoti:");
        foreach (var (id, name, penalty) in result.PenaltyBreakdown.OrderByDescending(x => x.Penalty))
            _out.WriteLine($"  {id,-12} {name,-40} {penalty}");
        _out.WriteLine("------------------------------------------------------------");

        Assert.Equal(0, result.Cost.HardViolations);
        Assert.Empty(result.HardViolations);
        Assert.True(result.PlacedPercent >= 90.0, $"joylashgan: {result.PlacedPercent:F2}%");
    }
}
