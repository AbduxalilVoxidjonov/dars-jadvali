using DarsJadvali.Application.Abstractions;

namespace DarsJadvali.Application.Validation;

/// <summary>
/// Jadvalning xotiradagi nusxasini (<see cref="ScheduleSnapshot"/>) bir marta yuklab
/// beruvchi servis — prezentatsiya qatlami uchun YAGONA kirish nuqtasi.
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> <c>IScheduleValidator.ValidateAsync</c> har chaqiruvda butun
/// nusxani qaytadan o'qiydi. Drag paytida (kursor har siljiganda) buni chaqirib bo'lmaydi:
/// baholash &lt;16 ms bo'lishi shart. Shu sababli UI nusxani <b>bir marta</b> yuklaydi
/// (<see cref="LoadAsync"/>), so'ng har bir harakatni <see cref="ScheduleValidator.Evaluate"/>
/// bilan XOTIRADA baholaydi. Qoida shu bilan yagona manbada qoladi — ilgari u
/// Desktop'dagi <c>TimetableBoard.Evaluate</c> da takrorlangan edi.
/// </remarks>
public interface IScheduleSnapshotProvider
{
    /// <summary>
    /// Jadval varianti uchun nusxani yuklaydi.
    /// <paramref name="scheduleId"/> <c>null</c> bo'lsa — faol jadval.
    /// </summary>
    Task<ScheduleSnapshot> LoadAsync(int? scheduleId = null, CancellationToken ct = default);
}

/// <summary><see cref="IScheduleSnapshotProvider"/> implementatsiyasi.</summary>
public sealed class ScheduleSnapshotProvider : IScheduleSnapshotProvider
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi provayder yaratadi.</summary>
    public ScheduleSnapshotProvider(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public Task<ScheduleSnapshot> LoadAsync(int? scheduleId = null, CancellationToken ct = default)
        => ScheduleSnapshot.LoadAsync(_uow, scheduleId, ct);
}
