using DarsJadvali.Application.Abstractions;

namespace DarsJadvali.Application.Validation;

/// <summary>Jadval qoidalarini tekshiruvchi asosiy implementatsiya.</summary>
public sealed class ScheduleValidator : IScheduleValidator
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi validator yaratadi.</summary>
    public ScheduleValidator(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateAsync(ScheduleEntryDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var snapshot = await ScheduleSnapshot.LoadAsync(_uow, draft.ScheduleId, ct).ConfigureAwait(false);
        return ValidateAgainst(snapshot, draft);
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAllAsync(CancellationToken ct = default) =>
        ValidateAllAsync(null, ct);

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateAllAsync(int? scheduleId, CancellationToken ct = default)
    {
        var snapshot = await ScheduleSnapshot.LoadAsync(_uow, scheduleId, ct).ConfigureAwait(false);
        return ValidateAll(snapshot, ct);
    }

    /// <summary>Xotiradagi nusxaga qarab bitta loyihani tekshiradi.</summary>
    internal static ValidationResult ValidateAgainst(ScheduleSnapshot snapshot, ScheduleEntryDraft draft)
    {
        var conflicts = snapshot.Validate(draft);
        return conflicts.Count == 0 ? ValidationResult.Success() : ValidationResult.From(conflicts);
    }

    /// <summary>Xotiradagi nusxadagi butun jadvalni tekshiradi.</summary>
    internal static ValidationResult ValidateAll(ScheduleSnapshot snapshot, CancellationToken ct = default)
    {
        var all = new List<Conflict>();
        var seen = new HashSet<(string Code, string Message)>();

        // Ro'yxat tekshiruv davomida o'zgarmaydi, lekin nusxa olib yuramiz.
        foreach (var entry in snapshot.Entries.ToList())
        {
            ct.ThrowIfCancellationRequested();

            var draft = new ScheduleEntryDraft(
                entry.Id,
                entry.ClassGroupId,
                entry.SubjectId,
                entry.TeacherId,
                entry.DayOfWeek,
                entry.LessonNumber,
                entry.RoomNumber,
                snapshot.ScheduleId);

            foreach (var conflict in snapshot.Validate(draft))
            {
                if (seen.Add((conflict.Code, conflict.Message)))
                {
                    all.Add(conflict);
                }
            }
        }

        return all.Count == 0 ? ValidationResult.Success() : ValidationResult.From(all);
    }
}
