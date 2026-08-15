using DarsJadvali.Scheduling.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace DarsJadvali.Scheduling.Tests;

/// <summary>T-A-01 — determinizm.</summary>
public class DeterminismTests
{
    private readonly ITestOutputHelper _out;

    public DeterminismTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Same_Seed_Produces_Byte_Identical_Solution()
    {
        string? reference = null;
        long referenceCost = 0;

        for (int run = 0; run < 3; run++)
        {
            var p = TestProblems.SmallSchool();
            var result = new Scheduler().Generate(p, new GenerationOptions
            {
                Seed = 20240814,
                Complexity = Complexity.Small,
                MaxOptimizeIterations = 30_000,
            });
            var fp = result.Solution.Fingerprint();
            if (reference is null)
            {
                reference = fp;
                referenceCost = result.Cost.SoftCost;
            }
            else
            {
                Assert.Equal(reference, fp);
                Assert.Equal(referenceCost, result.Cost.SoftCost);
            }
        }
        _out.WriteLine($"Barqaror jarima: {referenceCost}");
    }

    [Fact]
    public void Different_Seeds_Usually_Produce_Different_Solutions()
    {
        var fingerprints = new HashSet<string>();
        for (int seed = 1; seed <= 6; seed++)
        {
            var p = TestProblems.SmallSchool();
            var result = new Scheduler().Generate(p, new GenerationOptions
            {
                Seed = seed,
                Complexity = Complexity.Small,
                MaxOptimizeIterations = 30_000,
            });
            fingerprints.Add(result.Solution.Fingerprint());
        }
        Assert.True(fingerprints.Count >= 4, $"6 ta seed'dan {fingerprints.Count} xil natija chiqdi");
    }

    [Fact]
    public void Determinism_Holds_For_Divided_Classes()
    {
        string? reference = null;
        for (int run = 0; run < 3; run++)
        {
            var p = TestProblems.DividedClass(out _, out _, out _, out _, out _);
            var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 555, Complexity = Complexity.Normal });
            var fp = result.Solution.Fingerprint();
            reference ??= fp;
            Assert.Equal(reference, fp);
        }
    }
}
