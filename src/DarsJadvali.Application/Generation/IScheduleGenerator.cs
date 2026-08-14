namespace DarsJadvali.Application.Generation;

/// <summary>Jadval generatori. Kelajakda genetik algoritm ham shu interfeysni implement qiladi.</summary>
public interface IScheduleGenerator
{
    /// <summary>Algoritm nomi.</summary>
    string Name { get; }

    /// <summary>Algoritm haqida qisqacha.</summary>
    string Description { get; }

    /// <summary>Jadvalni generatsiya qiladi.</summary>
    Task<GenerationResult> GenerateAsync(
        GenerationOptions options,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken ct = default);
}
