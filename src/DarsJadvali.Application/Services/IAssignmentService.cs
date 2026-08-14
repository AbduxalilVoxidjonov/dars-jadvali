using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>O'qituvchi–fan–sinf biriktirmalari servisi.</summary>
public interface IAssignmentService
{
    /// <summary>Barcha biriktirmalar.</summary>
    Task<IReadOnlyList<TeacherAssignment>> GetAllAsync(CancellationToken ct = default);

    /// <summary>O'qituvchi bo'yicha biriktirmalar.</summary>
    Task<IReadOnlyList<TeacherAssignment>> GetByTeacherAsync(int teacherId, CancellationToken ct = default);

    /// <summary>Sinf bo'yicha biriktirmalar.</summary>
    Task<IReadOnlyList<TeacherAssignment>> GetByClassGroupAsync(int classGroupId, CancellationToken ct = default);

    /// <summary>Yangi biriktirma qo'shadi.</summary>
    Task<TeacherAssignment> CreateAsync(TeacherAssignment a, CancellationToken ct = default);

    /// <summary>Biriktirmani yangilaydi.</summary>
    Task UpdateAsync(TeacherAssignment a, CancellationToken ct = default);

    /// <summary>Biriktirmani o'chiradi.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Biriktirma bo'yicha: jami soat, qo'yilgan soat, qolgan soat.
    /// "Qo'yilgan soat" faqat FAOL jadval ichida sanaladi.
    /// </summary>
    Task<(int Weekly, int Placed, int Remaining)> GetHoursSummaryAsync(
        int assignmentId, CancellationToken ct = default);

    /// <summary>
    /// Biriktirma bo'yicha soatlar hisobi — aniq jadval varianti bo'yicha
    /// (<paramref name="scheduleId"/> — <c>null</c> bo'lsa faol jadval).
    /// </summary>
    Task<(int Weekly, int Placed, int Remaining)> GetHoursSummaryAsync(
        int assignmentId, int? scheduleId, CancellationToken ct = default);
}

/// <summary><see cref="IAssignmentService"/> implementatsiyasi.</summary>
public sealed class AssignmentService : IAssignmentService
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi servis yaratadi.</summary>
    public AssignmentService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TeacherAssignment>> GetAllAsync(CancellationToken ct = default) =>
        _uow.Assignments.GetAllAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherAssignment>> GetByTeacherAsync(int teacherId, CancellationToken ct = default)
    {
        var all = await _uow.Assignments.GetAllAsync(ct).ConfigureAwait(false);
        return all.Where(a => a.TeacherId == teacherId).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherAssignment>> GetByClassGroupAsync(int classGroupId, CancellationToken ct = default)
    {
        var all = await _uow.Assignments.GetAllAsync(ct).ConfigureAwait(false);
        return all.Where(a => a.ClassGroupId == classGroupId).ToList();
    }

    /// <inheritdoc />
    public async Task<TeacherAssignment> CreateAsync(TeacherAssignment a, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        var created = await _uow.Assignments.AddAsync(a, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TeacherAssignment a, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        await _uow.Assignments.UpdateAsync(a, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _uow.Assignments.DeleteAsync(id, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<(int Weekly, int Placed, int Remaining)> GetHoursSummaryAsync(
        int assignmentId, CancellationToken ct = default) =>
        GetHoursSummaryAsync(assignmentId, null, ct);

    /// <inheritdoc />
    public async Task<(int Weekly, int Placed, int Remaining)> GetHoursSummaryAsync(
        int assignmentId, int? scheduleId, CancellationToken ct = default)
    {
        var assignment = await _uow.Assignments.GetByIdAsync(assignmentId, ct).ConfigureAwait(false);
        if (assignment is null)
        {
            return (0, 0, 0);
        }

        var targetScheduleId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);

        var entries = await _uow.ScheduleEntries.GetAllAsync(ct).ConfigureAwait(false);
        var placed = entries.Count(e =>
            e.ScheduleId == targetScheduleId &&
            e.TeacherId == assignment.TeacherId &&
            e.SubjectId == assignment.SubjectId &&
            e.ClassGroupId == assignment.ClassGroupId);

        return (assignment.WeeklyHoursCount, placed, assignment.WeeklyHoursCount - placed);
    }
}
