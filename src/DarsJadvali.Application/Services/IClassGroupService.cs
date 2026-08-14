using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>Sinflar bilan ishlash servisi.</summary>
public interface IClassGroupService
{
    /// <summary>Barcha sinflar.</summary>
    Task<IReadOnlyList<ClassGroup>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Id bo'yicha sinf.</summary>
    Task<ClassGroup?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Yangi sinf qo'shadi.</summary>
    Task<ClassGroup> CreateAsync(ClassGroup classGroup, CancellationToken ct = default);

    /// <summary>Sinf ma'lumotini yangilaydi.</summary>
    Task UpdateAsync(ClassGroup classGroup, CancellationToken ct = default);

    /// <summary>Sinfni o'chiradi.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary><see cref="IClassGroupService"/> implementatsiyasi.</summary>
public sealed class ClassGroupService : IClassGroupService
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi servis yaratadi.</summary>
    public ClassGroupService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ClassGroup>> GetAllAsync(CancellationToken ct = default) =>
        _uow.ClassGroups.GetAllAsync(ct);

    /// <inheritdoc />
    public Task<ClassGroup?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _uow.ClassGroups.GetByIdAsync(id, ct);

    /// <inheritdoc />
    public async Task<ClassGroup> CreateAsync(ClassGroup classGroup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(classGroup);
        var created = await _uow.ClassGroups.AddAsync(classGroup, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ClassGroup classGroup, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(classGroup);
        await _uow.ClassGroups.UpdateAsync(classGroup, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _uow.ClassGroups.DeleteAsync(id, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
