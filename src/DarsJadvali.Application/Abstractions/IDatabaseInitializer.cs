namespace DarsJadvali.Application.Abstractions;

/// <summary>Ma'lumotlar bazasini yaratish/migratsiya va boshlang'ich to'ldirish.</summary>
public interface IDatabaseInitializer
{
    /// <summary>Bazani tayyorlaydi.</summary>
    Task InitializeAsync(CancellationToken ct = default);
}
