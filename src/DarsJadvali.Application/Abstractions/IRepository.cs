using DarsJadvali.Domain.Common;

namespace DarsJadvali.Application.Abstractions;

/// <summary>Umumiy repozitoriy abstraksiyasi.</summary>
/// <typeparam name="T">Entity turi.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Barcha yozuvlarni (navigatsiyalari bilan) qaytaradi.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Id bo'yicha yozuvni qaytaradi.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Yangi yozuv qo'shadi.</summary>
    Task<T> AddAsync(T entity, CancellationToken ct = default);

    /// <summary>Yozuvni yangilaydi.</summary>
    Task UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>Yozuvni o'chiradi.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Yozuv mavjudligini tekshiradi.</summary>
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}
