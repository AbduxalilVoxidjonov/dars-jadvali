using System.Collections.Concurrent;
using System.Diagnostics;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Services;

/// <summary>Generatsiya jarayonining holati.</summary>
public enum GenerationJobState
{
    /// <summary>Ishlamoqda.</summary>
    Running = 0,

    /// <summary>Tugadi (hisobot bor).</summary>
    Completed = 1,

    /// <summary>Bekor qilindi.</summary>
    Cancelled = 2,

    /// <summary>Xato bilan tugadi.</summary>
    Failed = 3,
}

/// <summary>
/// Fon rejimida ishlayotgan bitta generatsiya.
/// </summary>
/// <remarks>
/// Generatsiya bir necha soniya (murakkab maktabda — daqiqalar) davom etadi, shuning
/// uchun HTTP so'rovi uni KUTMAYDI: <c>POST /api/board/generate</c> darhol <c>jobId</c>
/// qaytaradi, sahifa esa <c>GET /api/board/generate/{jobId}</c> bilan holatni kuzatadi.
/// </remarks>
public sealed class GenerationJob
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    internal GenerationJob(string id, CancellationTokenSource cts)
    {
        Id = id;
        Cts = cts;
    }

    /// <summary>Jarayon identifikatori.</summary>
    public string Id { get; }

    internal CancellationTokenSource Cts { get; }

    /// <summary>Joriy holat.</summary>
    public GenerationJobState State { get; internal set; } = GenerationJobState.Running;

    /// <summary>Oxirgi bosqich nomi.</summary>
    public string Phase { get; internal set; } = "Boshlanmoqda";

    /// <summary>Bajarilgan foiz.</summary>
    public double Percent { get; internal set; }

    /// <summary>Joylashtirilgan kartochkalar.</summary>
    public int PlacedCards { get; internal set; }

    /// <summary>Jami kartochkalar.</summary>
    public int TotalCards { get; internal set; }

    /// <summary>Joriy jarima.</summary>
    public long SoftCost { get; internal set; }

    /// <summary>Xato matni (faqat <see cref="GenerationJobState.Failed"/> da).</summary>
    public string? Error { get; internal set; }

    /// <summary>Tayyor hisobot.</summary>
    public ScheduleGenerationReport? Report { get; internal set; }

    /// <summary>Boshlangandan beri o'tgan vaqt.</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    internal void Stop() => _clock.Stop();

    /// <summary>Holatni DTO ga o'giradi.</summary>
    public BoardGenerationStatusDto ToDto() => new(
        Id,
        State switch
        {
            GenerationJobState.Running => "running",
            GenerationJobState.Completed => "completed",
            GenerationJobState.Cancelled => "cancelled",
            _ => "failed",
        },
        Phase,
        Math.Round(Percent, 1),
        PlacedCards,
        TotalCards,
        SoftCost,
        Math.Round(Elapsed.TotalSeconds, 2),
        Error,
        Report?.ToDto());
}

/// <summary>
/// Fon generatsiyalarining ro'yxati (singleton).
/// </summary>
/// <remarks>
/// Servis singleton, generatsiyaning O'ZI esa har safar YANGI DI qamrovida (scope)
/// ishlaydi: <c>DbContext</c> scoped bo'lgani uchun uni singleton ichida ushlab
/// turish mumkin emas.
/// </remarks>
public sealed class GenerationJobs
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<GenerationJobs> _logger;
    private readonly ConcurrentDictionary<string, GenerationJob> _jobs = new(StringComparer.Ordinal);

    /// <summary>Yangi ro'yxat.</summary>
    /// <param name="scopes">DI qamrovi fabrikasi.</param>
    /// <param name="logger">Jurnal.</param>
    public GenerationJobs(IServiceScopeFactory scopes, ILogger<GenerationJobs> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Ayni damda ishlayotgan generatsiya bormi.</summary>
    public bool HasRunning => _jobs.Values.Any(j => j.State == GenerationJobState.Running);

    /// <summary>Jarayonni topadi.</summary>
    /// <param name="id">Jarayon Id.</param>
    public GenerationJob? Find(string id) =>
        string.IsNullOrWhiteSpace(id) ? null : _jobs.TryGetValue(id, out var job) ? job : null;

    /// <summary>Barcha jarayonlar (yangisi birinchi).</summary>
    public IReadOnlyList<GenerationJob> All() => _jobs.Values.ToList();

    /// <summary>Jarayonni bekor qiladi.</summary>
    /// <param name="id">Jarayon Id.</param>
    public bool Cancel(string id)
    {
        var job = Find(id);
        if (job is null || job.State != GenerationJobState.Running)
            return false;

        job.Cts.Cancel();
        return true;
    }

    /// <summary>Yangi generatsiyani fon rejimida boshlaydi.</summary>
    /// <param name="options">Generatsiya sozlamalari.</param>
    public GenerationJob Start(ScheduleGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var cts = new CancellationTokenSource();
        var job = new GenerationJob(Guid.NewGuid().ToString("N"), cts);
        _jobs[job.Id] = job;

        // Eskirgan yozuvlar to'planib qolmasligi uchun (lokal dastur — chegara kichik).
        Prune();

        _ = Task.Run(() => RunAsync(job, options), CancellationToken.None);
        return job;
    }

    private async Task RunAsync(GenerationJob job, ScheduleGenerationOptions options)
    {
        using var scope = _scopes.CreateScope();

        try
        {
            var service = scope.ServiceProvider.GetRequiredService<IScheduleGenerationService>();

            var progress = new Progress<ScheduleGenerationProgress>(p =>
            {
                job.Phase = p.PhaseName;
                job.Percent = p.Percent;
                job.PlacedCards = p.PlacedCards;
                job.TotalCards = p.TotalCards;
                job.SoftCost = p.SoftCost;
            });

            var report = await service.GenerateAsync(options, progress, job.Cts.Token)
                .ConfigureAwait(false);

            job.Report = report;
            job.PlacedCards = report.PlacedCards;
            job.TotalCards = report.TotalCards;
            job.SoftCost = report.SoftCost;
            job.Percent = 100;
            job.Phase = report.Cancelled ? "Bekor qilindi" : "Tayyor";
            job.State = report.Cancelled ? GenerationJobState.Cancelled : GenerationJobState.Completed;
        }
        catch (OperationCanceledException)
        {
            job.Phase = "Bekor qilindi";
            job.State = GenerationJobState.Cancelled;
        }
        catch (Exception ex)
        {
            // Foydalanuvchiga faqat qisqa xabar; to'liq tafsilot — server jurnalida.
            _logger.LogError(ex, "Generatsiya xato bilan tugadi: {JobId}", job.Id);
            job.Error = "Generatsiya bajarilmadi. Ma'lumotlarni tekshirib, qayta urinib ko'ring.";
            job.State = GenerationJobState.Failed;
        }
        finally
        {
            job.Stop();
            job.Cts.Dispose();
        }
    }

    /// <summary>Tugagan eski yozuvlarni olib tashlaydi (oxirgi 10 tasi qoladi).</summary>
    private void Prune()
    {
        var finished = _jobs.Values
            .Where(j => j.State != GenerationJobState.Running)
            .OrderByDescending(j => j.Elapsed)
            .Skip(10)
            .ToList();

        foreach (var job in finished)
            _jobs.TryRemove(job.Id, out _);
    }
}
