namespace DarsJadvali.Scheduling.Pipeline;

/// <summary>aSc "Complexity of generation" (#1336, #1348..#1351) — qidiruv byudjeti.</summary>
public enum Complexity
{
    Small = 0,
    Normal = 1,
    Large = 2,
    Huge = 3,
}

/// <summary>Generatsiya fazasi (02-asc-.., 3.2).</summary>
public enum GenerationPhase
{
    Verify = 0,
    Propagate = 1,
    Construct = 2,
    EjectionChain = 3,
    Optimize = 4,
    Relax = 5,
    Rooms = 6,
    Done = 7,
}

/// <summary>Generatsiya parametrlari.</summary>
public sealed class GenerationOptions
{
    /// <summary>Determinizm uchun urug'. Bir xil seed → bayt-bayt bir xil natija.</summary>
    public int Seed { get; set; } = 12345;

    public Complexity Complexity { get; set; } = Complexity.Normal;

    /// <summary>Faza 0 (Verify) ni bajarish.</summary>
    public bool RunVerify { get; set; } = true;

    /// <summary>Verify xatolari topilsa ham generatsiyani davom ettirish.</summary>
    public bool ContinueOnVerifyFaults { get; set; } = true;

    /// <summary>Faza 5 (Relax) ni ishga tushirish (agar to'liq yechim topilmasa).</summary>
    public bool AllowRelaxation { get; set; } = true;

    /// <summary>Construct fazasidagi restartlar soni. -1 = Complexity'dan olinadi.</summary>
    public int Restarts { get; set; } = -1;

    /// <summary>Construct fazasidagi backtrack limiti. -1 = Complexity'dan.</summary>
    public int MaxBacktracks { get; set; } = -1;

    /// <summary>Optimize fazasidagi iteratsiyalar soni. -1 = Complexity'dan.</summary>
    public int MaxOptimizeIterations { get; set; } = -1;

    /// <summary>Ejection chain chuqurligi. -1 = Complexity'dan.</summary>
    public int EjectionMaxDepth { get; set; } = -1;

    /// <summary>Umumiy vaqt chegarasi. <c>null</c> = cheklanmagan (faqat CancellationToken).</summary>
    public TimeSpan? TimeLimit { get; set; }

    /// <summary>Simulated annealing boshlang'ich harorati.</summary>
    public double InitialTemperature { get; set; } = 1500.0;

    /// <summary>Geometrik sovutish koeffitsienti.</summary>
    public double CoolingRate { get; set; } = 0.99995;

    /// <summary>Tabu ro'yxati uzunligi.</summary>
    public int TabuTenure { get; set; } = 12;

    /// <summary>Progress hisobotlari orasidagi minimal interval.</summary>
    public TimeSpan ProgressInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    internal int EffectiveRestarts => Restarts >= 0 ? Restarts : Complexity switch
    {
        Complexity.Small => 1,
        Complexity.Normal => 4,
        Complexity.Large => 16,
        _ => 48,
    };

    internal int EffectiveBacktracks => MaxBacktracks >= 0 ? MaxBacktracks : Complexity switch
    {
        Complexity.Small => 2_000,
        Complexity.Normal => 20_000,
        Complexity.Large => 200_000,
        _ => 2_000_000,
    };

    internal int EffectiveOptimizeIterations => MaxOptimizeIterations >= 0 ? MaxOptimizeIterations : Complexity switch
    {
        Complexity.Small => 20_000,
        Complexity.Normal => 200_000,
        Complexity.Large => 2_000_000,
        _ => 20_000_000,
    };

    internal int EffectiveEjectionDepth => EjectionMaxDepth >= 0 ? EjectionMaxDepth : Complexity switch
    {
        Complexity.Small => 2,
        Complexity.Normal => 4,
        Complexity.Large => 6,
        _ => 10,
    };
}

/// <summary>Generatsiya jarayoni hisoboti (#1811 Rating, #1812 Collisions, #2770 Cards left).</summary>
public readonly record struct GenerationProgress(
    GenerationPhase Phase,
    int Iteration,
    int PlacedCards,
    int TotalCards,
    long SoftCost,
    long BestSoftCost,
    int UnplacedCards,
    TimeSpan Elapsed)
{
    public double PlacedPercent => TotalCards == 0 ? 100.0 : 100.0 * PlacedCards / TotalCards;

    public override string ToString()
        => $"{Phase} it={Iteration} joylashgan={PlacedCards}/{TotalCards} " +
           $"({PlacedPercent:F1}%) jarima={SoftCost} eng yaxshi={BestSoftCost} {Elapsed.TotalSeconds:F1}s";
}
