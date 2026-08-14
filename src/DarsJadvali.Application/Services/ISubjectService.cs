using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>Fanlar bilan ishlash servisi.</summary>
public interface ISubjectService
{
    /// <summary>Barcha fanlar.</summary>
    Task<IReadOnlyList<Subject>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Id bo'yicha fan.</summary>
    Task<Subject?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Yangi fan qo'shadi.</summary>
    Task<Subject> CreateAsync(Subject subject, CancellationToken ct = default);

    /// <summary>Fan ma'lumotini yangilaydi.</summary>
    Task UpdateAsync(Subject subject, CancellationToken ct = default);

    /// <summary>Fanni o'chiradi.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary><see cref="ISubjectService"/> implementatsiyasi.</summary>
public sealed class SubjectService : ISubjectService
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi servis yaratadi.</summary>
    public SubjectService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Subject>> GetAllAsync(CancellationToken ct = default) =>
        _uow.Subjects.GetAllAsync(ct);

    /// <inheritdoc />
    public Task<Subject?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _uow.Subjects.GetByIdAsync(id, ct);

    /// <inheritdoc />
    public async Task<Subject> CreateAsync(Subject subject, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var created = await _uow.Subjects.AddAsync(subject, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Subject subject, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        await _uow.Subjects.UpdateAsync(subject, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _uow.Subjects.DeleteAsync(id, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
