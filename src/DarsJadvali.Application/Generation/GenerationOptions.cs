namespace DarsJadvali.Application.Generation;

/// <summary>Jadval generatsiyasi sozlamalari.</summary>
public sealed record GenerationOptions
{
    /// <summary>Generatsiyadan oldin mavjud jadval tozalansinmi.</summary>
    public bool ClearExisting { get; init; } = true;

    /// <summary>Maksimal iteratsiyalar soni.</summary>
    public int MaxIterations { get; init; } = 1000;

    /// <summary>Populyatsiya hajmi (genetik algoritm uchun).</summary>
    public int PopulationSize { get; init; } = 50;

    /// <summary>Mutatsiya ehtimoli (genetik algoritm uchun).</summary>
    public double MutationRate { get; init; } = 0.05;

    /// <summary>Tasodifiy sonlar urug'i (takrorlanuvchi natija uchun).</summary>
    public int? RandomSeed { get; init; }

    /// <summary>
    /// Qaysi dars jadvaliga (variantiga) yozilsin. <c>null</c> — faol jadval.
    /// Generator faqat shu jadvalga yozadi va <see cref="ClearExisting"/> ham faqat shuni tozalaydi.
    /// </summary>
    public int? ScheduleId { get; init; }
}

/// <summary>Generatsiya jarayoni haqida xabar.</summary>
/// <param name="Current">Bajarilgan qadam.</param>
/// <param name="Total">Jami qadamlar.</param>
/// <param name="Fitness">Joriy sifat ko'rsatkichi (0..1).</param>
/// <param name="Message">O'zbekcha izoh.</param>
public sealed record GenerationProgress(int Current, int Total, double Fitness, string Message);

/// <summary>Generatsiya natijasi.</summary>
/// <param name="Success">Barcha soatlar joylashtirildimi.</param>
/// <param name="PlacedCount">Qo'yilgan darslar soni.</param>
/// <param name="UnplacedCount">Joylashtirilmagan darslar soni.</param>
/// <param name="Messages">Izohlar.</param>
/// <param name="Elapsed">Sarflangan vaqt.</param>
public sealed record GenerationResult(
    bool Success,
    int PlacedCount,
    int UnplacedCount,
    IReadOnlyList<string> Messages,
    TimeSpan Elapsed);
