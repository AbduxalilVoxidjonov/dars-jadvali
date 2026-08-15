using DarsJadvali.Application.Validation;
using DarsJadvali.Scheduling.Pipeline;

namespace DarsJadvali.Application.Scheduling;

/// <summary>Yangi (kartochka asosidagi) generatsiya sozlamalari.</summary>
/// <remarks>
/// Eski <c>Application.Generation.GenerationOptions</c> dan farqi: bu yerda o'lik
/// sozlamalar (<c>PopulationSize</c>, <c>MutationRate</c>, <c>MaxIterations</c>) yo'q —
/// hammasi yadro qidiruv byudjetiga bog'langan (05-audit K-10, K-17).
/// </remarks>
public sealed record ScheduleGenerationOptions
{
    /// <summary>Qaysi jadval variantiga yozilsin. <c>null</c> — faol jadval.</summary>
    public int? ScheduleId { get; init; }

    /// <summary>Determinizm urug'i: bir xil urug' + bir xil ma'lumot → bir xil jadval.</summary>
    public int Seed { get; init; } = 12345;

    /// <summary>Qidiruv byudjeti (aSc "Complexity of generation").</summary>
    public Complexity Complexity { get; init; } = Complexity.Normal;

    /// <summary>Umumiy vaqt chegarasi. <c>null</c> — faqat <c>CancellationToken</c>.</summary>
    public TimeSpan? TimeLimit { get; init; }

    /// <summary>
    /// Yechim to'liq bo'lmasa ham natija bazaga yozilsinmi.
    /// <c>false</c> bo'lsa to'liq bo'lmagan jadval yozilmaydi (tranzaksiya qaytariladi).
    /// </summary>
    public bool SavePartial { get; init; } = true;

    /// <summary>Faza 5 (Relax) tahlilini bajarish.</summary>
    public bool AllowRelaxation { get; init; } = true;

    /// <summary>Qulflangan kartochkalar joyida qolsinmi (C-GBL-06).</summary>
    public bool KeepLocked { get; init; } = true;
}

/// <summary>Generatsiya jarayoni haqida xabar (UI uchun).</summary>
/// <param name="Phase">Yadro fazasi.</param>
/// <param name="PhaseName">Fazaning o'zbekcha nomi.</param>
/// <param name="Percent">Bajarilgan foiz (0..100).</param>
/// <param name="PlacedCards">Joylashtirilgan kartochkalar.</param>
/// <param name="TotalCards">Jami kartochkalar.</param>
/// <param name="SoftCost">Joriy jarima.</param>
/// <param name="BestSoftCost">Eng yaxshi topilgan jarima.</param>
/// <param name="Elapsed">Sarflangan vaqt.</param>
public readonly record struct ScheduleGenerationProgress(
    GenerationPhase Phase,
    string PhaseName,
    double Percent,
    int PlacedCards,
    int TotalCards,
    long SoftCost,
    long BestSoftCost,
    TimeSpan Elapsed)
{
    /// <summary>Foydalanuvchiga ko'rsatiladigan qator.</summary>
    public override string ToString()
        => $"{PhaseName}: {PlacedCards}/{TotalCards} ({Percent:F0}%), jarima {SoftCost}";
}

/// <summary>Bitta soft cheklovning jarima ulushi.</summary>
/// <param name="ConstraintId">Cheklov kodi, masalan "C-CLS-01".</param>
/// <param name="Name">Cheklov nomi (o'zbekcha).</param>
/// <param name="Penalty">To'plangan jarima.</param>
public sealed record PenaltyShare(string ConstraintId, string Name, long Penalty);

/// <summary>Yangi generatsiya natijasi.</summary>
public sealed record ScheduleGenerationReport
{
    /// <summary>Jadval to'liq va hard cheklovlar buzilmagan.</summary>
    public required bool Success { get; init; }

    /// <summary>Natija bazaga yozildimi (tranzaksiya commit bo'ldimi).</summary>
    public required bool Applied { get; init; }

    /// <summary>Bekor qilinganmi.</summary>
    public required bool Cancelled { get; init; }

    /// <summary>Qaysi jadval variantiga ishlangani.</summary>
    public required int ScheduleId { get; init; }

    /// <summary>Joylashtirilgan kartochkalar.</summary>
    public required int PlacedCards { get; init; }

    /// <summary>Jami kartochkalar (joylashtirilishi kerak bo'lgan).</summary>
    public required int TotalCards { get; init; }

    /// <summary>Joylashtirilmagan kartochkalar.</summary>
    public int UnplacedCards => Math.Max(0, TotalCards - PlacedCards);

    /// <summary>
    /// Joylashtirilmagan darslarning ANIQ ro'yxati (dars, fan, sinf, guruh, qolgan soat).
    /// </summary>
    /// <remarks>
    /// Ilgari hisobot faqat <see cref="UnplacedCards"/> sonini berardi va UI paneli
    /// "haftalik me'yor − qo'yilgan soat" bilan taxmin qilardi. Endi ro'yxat
    /// <c>Lesson.PeriodsPerWeek</c> va <c>SUM(Card.Length)</c> ayirmasidan olinadi.
    /// Yozilmagan (rad etilgan) natijada bo'sh bo'ladi.
    /// </remarks>
    public IReadOnlyList<Board.UnplacedLessonView> UnplacedLessons { get; init; } =
        Array.Empty<Board.UnplacedLessonView>();

    /// <summary>Yozilgan bandlik qatorlari soni.</summary>
    public required int OccurrenceRows { get; init; }

    /// <summary>Yakuniy soft jarima.</summary>
    public required long SoftCost { get; init; }

    /// <summary>Buzilgan hard cheklovlar (bo'sh bo'lishi kutiladi).</summary>
    public required IReadOnlyList<string> HardViolations { get; init; }

    /// <summary>Soft jarimaning cheklovlar bo'yicha taqsimoti.</summary>
    public required IReadOnlyList<PenaltyShare> PenaltyBreakdown { get; init; }

    /// <summary>Faza 0 (Verify) xatolari — o'zbekcha, sabab ko'rsatilgan.</summary>
    public required IReadOnlyList<string> VerificationFaults { get; init; }

    /// <summary>Faza 5 (Relax) tavsiyalari: qaysi cheklovni yumshatish yordam beradi.</summary>
    public required IReadOnlyList<string> RelaxationSuggestions { get; init; }

    /// <summary>Application darajasidagi konfliktlar (jumladan <c>GROUP_DIVISION_OVERLAP</c>).</summary>
    public required IReadOnlyList<Conflict> Conflicts { get; init; }

    /// <summary>Mapper izohlari va umumiy xabarlar.</summary>
    public required IReadOnlyList<string> Messages { get; init; }

    /// <summary>Sarflangan vaqt.</summary>
    public required TimeSpan Elapsed { get; init; }
}

/// <summary>
/// Kartochka (<c>Lesson</c> + <c>Card</c>) asosidagi yangi generatsiya servisi.
/// </summary>
/// <remarks>
/// Eski <c>IScheduleGenerator</c> (<c>ScheduleEntry</c> asosidagi) buzilmaydi —
/// bu API yonma-yon turadi. Butun amal BITTA tranzaksiyada bajariladi (00 §6.4).
/// </remarks>
public interface IScheduleGenerationService
{
    /// <summary>Algoritm nomi.</summary>
    string Name { get; }

    /// <summary>Algoritm haqida qisqacha.</summary>
    string Description { get; }

    /// <summary>
    /// Jadvalni generatsiya qiladi va natijani <c>Card</c> + <c>CardOccurrence</c> ga yozadi.
    /// Xato yoki bekor qilishda <b>hech narsa o'zgarmaydi</b> — eski jadval joyida qoladi.
    /// </summary>
    Task<ScheduleGenerationReport> GenerateAsync(
        ScheduleGenerationOptions options,
        IProgress<ScheduleGenerationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Mavjud jadvalni Application darajasidagi qoidalar bo'yicha tekshiradi
    /// (hozircha <c>GROUP_DIVISION_OVERLAP</c>).
    /// </summary>
    Task<IReadOnlyList<Conflict>> ValidateAsync(int? scheduleId = null, CancellationToken ct = default);
}
