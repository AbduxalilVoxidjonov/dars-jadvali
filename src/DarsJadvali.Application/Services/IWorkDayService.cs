using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>Ish kunlari va dars soatlari servisi.</summary>
public interface IWorkDayService
{
    /// <summary>Barcha kunlar.</summary>
    Task<IReadOnlyList<WorkDay>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Faqat faol kunlar.</summary>
    Task<IReadOnlyList<WorkDay>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Kunlar sozlamasini saqlaydi.</summary>
    Task SaveAllAsync(IEnumerable<WorkDay> days, CancellationToken ct = default);

    /// <summary>Faol kunlar ichidagi eng katta dars soati raqami.</summary>
    Task<int> GetMaxLessonNumberAsync(CancellationToken ct = default);

    /// <summary>Dars soatlari (vaqt oraliqlari).</summary>
    Task<IReadOnlyList<LessonSlot>> GetLessonSlotsAsync(CancellationToken ct = default);

    /// <summary>Dars soatlarini saqlaydi.</summary>
    Task SaveLessonSlotsAsync(IEnumerable<LessonSlot> slots, CancellationToken ct = default);
}

/// <summary><see cref="IWorkDayService"/> implementatsiyasi.</summary>
public sealed class WorkDayService : IWorkDayService
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi servis yaratadi.</summary>
    public WorkDayService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkDay>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await _uow.WorkDays.GetAllAsync(ct).ConfigureAwait(false);
        return all.OrderBy(d => (int)d.DayOfWeek).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkDay>> GetActiveAsync(CancellationToken ct = default)
    {
        var all = await _uow.WorkDays.GetAllAsync(ct).ConfigureAwait(false);
        return all.Where(d => d.IsActive).OrderBy(d => (int)d.DayOfWeek).ToList();
    }

    /// <inheritdoc />
    public async Task SaveAllAsync(IEnumerable<WorkDay> days, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(days);

        var existing = await _uow.WorkDays.GetAllAsync(ct).ConfigureAwait(false);
        var byDay = existing.ToDictionary(d => d.DayOfWeek);

        foreach (var day in days)
        {
            if (byDay.TryGetValue(day.DayOfWeek, out var current))
            {
                current.IsActive = day.IsActive;
                current.MaxLessonsPerDay = day.MaxLessonsPerDay;
                await _uow.WorkDays.UpdateAsync(current, ct).ConfigureAwait(false);
            }
            else
            {
                await _uow.WorkDays.AddAsync(day, ct).ConfigureAwait(false);
            }
        }

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetMaxLessonNumberAsync(CancellationToken ct = default)
    {
        var active = await GetActiveAsync(ct).ConfigureAwait(false);
        return active.Count == 0 ? 0 : active.Max(d => d.MaxLessonsPerDay);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LessonSlot>> GetLessonSlotsAsync(CancellationToken ct = default)
    {
        var all = await _uow.LessonSlots.GetAllAsync(ct).ConfigureAwait(false);
        return all.OrderBy(s => s.LessonNumber).ToList();
    }

    /// <inheritdoc />
    public async Task SaveLessonSlotsAsync(IEnumerable<LessonSlot> slots, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var incoming = slots.ToList();
        var existing = await _uow.LessonSlots.GetAllAsync(ct).ConfigureAwait(false);
        var byNumber = existing.ToDictionary(s => s.LessonNumber);

        foreach (var slot in incoming)
        {
            if (byNumber.TryGetValue(slot.LessonNumber, out var current))
            {
                current.StartTime = slot.StartTime;
                current.EndTime = slot.EndTime;
                await _uow.LessonSlots.UpdateAsync(current, ct).ConfigureAwait(false);
            }
            else
            {
                await _uow.LessonSlots.AddAsync(slot, ct).ConfigureAwait(false);
            }
        }

        // Ro'yxatda qolmagan soatlar o'chiriladi.
        var keep = incoming.Select(s => s.LessonNumber).ToHashSet();
        foreach (var old in existing.Where(s => !keep.Contains(s.LessonNumber)))
        {
            await _uow.LessonSlots.DeleteAsync(old.Id, ct).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
