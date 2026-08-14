using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>O'qituvchilar bilan ishlash servisi.</summary>
public interface ITeacherService
{
    /// <summary>Barcha o'qituvchilar.</summary>
    Task<IReadOnlyList<Teacher>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Id bo'yicha o'qituvchi.</summary>
    Task<Teacher?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Yangi o'qituvchi qo'shadi.</summary>
    Task<Teacher> CreateAsync(Teacher teacher, CancellationToken ct = default);

    /// <summary>O'qituvchi ma'lumotini yangilaydi.</summary>
    Task UpdateAsync(Teacher teacher, CancellationToken ct = default);

    /// <summary>O'qituvchini o'chiradi.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary><see cref="ITeacherService"/> implementatsiyasi.</summary>
public sealed class TeacherService : ITeacherService
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi servis yaratadi.</summary>
    public TeacherService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Teacher>> GetAllAsync(CancellationToken ct = default) =>
        _uow.Teachers.GetAllAsync(ct);

    /// <inheritdoc />
    public Task<Teacher?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _uow.Teachers.GetByIdAsync(id, ct);

    /// <inheritdoc />
    public async Task<Teacher> CreateAsync(Teacher teacher, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(teacher);
        var created = await _uow.Teachers.AddAsync(teacher, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Teacher teacher, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(teacher);
        await _uow.Teachers.UpdateAsync(teacher, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _uow.Teachers.DeleteAsync(id, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
