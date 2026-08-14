using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>O'quv yillari servisi. Eski o'quv yillari o'chirilmasdan saqlanib qoladi.</summary>
public interface IAcademicYearService
{
    /// <summary>Barcha o'quv yillari (yangisidan eskisiga qarab).</summary>
    Task<IReadOnlyList<AcademicYear>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Id bo'yicha o'quv yili.</summary>
    Task<AcademicYear?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Yangi o'quv yili qo'shadi. Nomi bo'sh yoki takrorlangan bo'lsa
    /// <see cref="InvalidOperationException"/> tashlanadi.
    /// </summary>
    Task<AcademicYear> CreateAsync(string name, int startYear, string? note = null, CancellationToken ct = default);

    /// <summary>O'quv yili nomini (va ixtiyoriy ravishda boshlanish yili, izohini) o'zgartiradi.</summary>
    Task RenameAsync(int id, string name, int? startYear = null, string? note = null, CancellationToken ct = default);

    /// <summary>
    /// O'quv yilini o'chiradi. <b>DIQQAT:</b> uning ichidagi barcha dars jadvallari va
    /// ularning barcha dars yozuvlari ham o'chib ketadi (kaskad).
    /// Bazada bitta ham jadval qolmaydigan bo'lsa — o'chirishga ruxsat berilmaydi.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Joriy sanaga mos o'quv yilini qaytaradi; bazada birortasi bo'lmasa yaratadi.
    /// </summary>
    Task<AcademicYear> GetOrCreateCurrentAsync(CancellationToken ct = default);
}

/// <summary><see cref="IAcademicYearService"/> implementatsiyasi.</summary>
public sealed class AcademicYearService : IAcademicYearService
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi servis yaratadi.</summary>
    public AcademicYearService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AcademicYear>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await _uow.AcademicYears.GetAllAsync(ct).ConfigureAwait(false);
        return all.OrderByDescending(y => y.StartYear).ThenByDescending(y => y.Id).ToList();
    }

    /// <inheritdoc />
    public Task<AcademicYear?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _uow.AcademicYears.GetByIdAsync(id, ct);

    /// <inheritdoc />
    public async Task<AcademicYear> CreateAsync(
        string name, int startYear, string? note = null, CancellationToken ct = default)
    {
        var trimmed = Normalize(name);
        await EnsureNameFreeAsync(trimmed, null, ct).ConfigureAwait(false);

        var created = await _uow.AcademicYears.AddAsync(new AcademicYear
        {
            Name = trimmed,
            StartYear = startYear,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        }, ct).ConfigureAwait(false);

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc />
    public async Task RenameAsync(
        int id, string name, int? startYear = null, string? note = null, CancellationToken ct = default)
    {
        var year = await _uow.AcademicYears.GetByIdAsync(id, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"O'quv yili topilmadi (ID: {id}).");

        var trimmed = Normalize(name);
        await EnsureNameFreeAsync(trimmed, id, ct).ConfigureAwait(false);

        year.Name = trimmed;
        if (startYear.HasValue)
        {
            year.StartYear = startYear.Value;
        }

        if (note is not null)
        {
            year.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }

        await _uow.AcademicYears.UpdateAsync(year, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var year = await _uow.AcademicYears.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (year is null)
        {
            return;
        }

        var schedules = await _uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);
        var inside = schedules.Where(s => s.AcademicYearId == id).ToList();

        if (schedules.Count - inside.Count <= 0)
        {
            throw new InvalidOperationException(
                "Bu yagona o'quv yili — uni o'chirsangiz dastur jadvalsiz qoladi. " +
                "Avval yangi o'quv yili va jadval yarating.");
        }

        var wasActive = inside.Any(s => s.IsActive);

        // Jadvallar va ularning yozuvlari kaskad bo'yicha o'chadi.
        await _uow.AcademicYears.DeleteAsync(id, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        if (wasActive)
        {
            // Faol jadval o'chib ketdi — dastur jadvalsiz qolmasligi uchun boshqasi faollashtiriladi.
            await ActiveScheduleResolver.GetActiveAsync(_uow, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<AcademicYear> GetOrCreateCurrentAsync(CancellationToken ct = default)
    {
        var (name, startYear) = ActiveScheduleResolver.CurrentAcademicYearName(DateTime.Now);

        var all = await _uow.AcademicYears.GetAllAsync(ct).ConfigureAwait(false);
        var existing = all.FirstOrDefault(y =>
            string.Equals(y.Name, name, StringComparison.OrdinalIgnoreCase) || y.StartYear == startYear);
        if (existing is not null)
        {
            return existing;
        }

        if (all.Count > 0)
        {
            return all.OrderByDescending(y => y.StartYear).ThenByDescending(y => y.Id).First();
        }

        return await CreateAsync(name, startYear, null, ct).ConfigureAwait(false);
    }

    private static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("O'quv yili nomi bo'sh bo'lishi mumkin emas.");
        }

        return name.Trim();
    }

    private async Task EnsureNameFreeAsync(string name, int? exceptId, CancellationToken ct)
    {
        var all = await _uow.AcademicYears.GetAllAsync(ct).ConfigureAwait(false);
        var clash = all.Any(y =>
            (!exceptId.HasValue || y.Id != exceptId.Value) &&
            string.Equals(y.Name, name, StringComparison.OrdinalIgnoreCase));

        if (clash)
        {
            throw new InvalidOperationException($"«{name}» nomli o'quv yili allaqachon mavjud.");
        }
    }
}
