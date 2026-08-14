using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core asosidagi umumiy repozitoriy.
/// Navigatsiyalar konfiguratsiyadagi <c>AutoInclude()</c> orqali avtomatik yuklanadi,
/// shuning uchun <c>Include</c> chaqiruvlari kerak emas.
/// Tracking o'chirilmagan (<c>AsNoTracking</c> ISHLATILMAYDI) — <c>UpdateAsync</c> to'g'ri ishlashi uchun.
/// </summary>
public class EfRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> Set;

    public EfRepository(AppDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.OrderBy(x => x.Id).ToListAsync(ct);

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await Set.FirstOrDefaultAsync(x => x.Id == id, ct);

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await Set.AddAsync(entity, ct);
        await Context.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        DetachIfDuplicate(entity);
        Set.Update(entity);
        await Context.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await Set.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return;

        Set.Remove(entity);
        await Context.SaveChangesAsync(ct);
    }

    public virtual Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => Set.AnyAsync(x => x.Id == id, ct);

    /// <summary>
    /// Agar shu Id bilan boshqa (kuzatilayotgan) nusxa bo'lsa — uni ajratib qo'yamiz,
    /// aks holda EF Core "already being tracked" xatosini beradi.
    /// </summary>
    private void DetachIfDuplicate(T entity)
    {
        var tracked = Set.Local.FirstOrDefault(e => e.Id == entity.Id);
        if (tracked is not null && !ReferenceEquals(tracked, entity))
            Context.Entry(tracked).State = EntityState.Detached;
    }
}
