using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>
/// Dars jadvallari (variantlari) servisi — jadvalning <b>o'zini</b> boshqaradi.
/// Dars yozuvlari bilan <see cref="IScheduleService"/> ishlaydi, chalkashtirmang.
/// </summary>
public interface IScheduleSetService
{
    /// <summary>Bazadagi barcha jadvallar.</summary>
    Task<IReadOnlyList<Schedule>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Bitta o'quv yili ichidagi jadvallar (yaratilish tartibida).</summary>
    Task<IReadOnlyList<Schedule>> GetByAcademicYearAsync(int academicYearId, CancellationToken ct = default);

    /// <summary>Id bo'yicha jadval.</summary>
    Task<Schedule?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// O'quv yili ichida yangi (bo'sh) jadval yaratadi.
    /// Nom shu yil ichida takrorlansa <see cref="InvalidOperationException"/> tashlanadi.
    /// </summary>
    Task<Schedule> CreateAsync(int academicYearId, string name, CancellationToken ct = default);

    /// <summary>Jadval nomini o'zgartiradi.</summary>
    Task RenameAsync(int scheduleId, string name, CancellationToken ct = default);

    /// <summary>
    /// Jadvalni <b>barcha dars yozuvlari bilan birga</b> nusxalaydi (yangi variant yaratadi).
    /// Nusxa asl jadval bilan bir xil o'quv yilida bo'ladi va originalga tegilmaydi.
    /// </summary>
    /// <param name="scheduleId">Nusxalanadigan jadval.</param>
    /// <param name="newName">Yangi nom; <c>null</c> bo'lsa "&lt;nom&gt; (nusxa)" ko'rinishida avtomatik tanlanadi.</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    Task<Schedule> DuplicateAsync(int scheduleId, string? newName = null, CancellationToken ct = default);

    /// <summary>
    /// Jadvalni o'chiradi. <b>DIQQAT:</b> uning barcha dars yozuvlari ham o'chadi (kaskad).
    /// Bazadagi oxirgi jadvalni o'chirib bo'lmaydi — <see cref="InvalidOperationException"/>.
    /// Faol jadval o'chirilsa, boshqa jadval avtomatik faollashtiriladi.
    /// </summary>
    Task DeleteAsync(int scheduleId, CancellationToken ct = default);

    /// <summary>
    /// Faol jadvalni qaytaradi. Bazada birortasi bo'lmasa — o'quv yili va
    /// "Asosiy jadval" avtomatik yaratiladi (dastur hech qachon jadvalsiz qolmaydi).
    /// </summary>
    Task<Schedule> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Faol jadval Id si.</summary>
    Task<int> GetActiveIdAsync(CancellationToken ct = default);

    /// <summary>Ko'rsatilgan jadvalni faol qiladi (qolganlari nofaol bo'ladi).</summary>
    Task SetActiveAsync(int scheduleId, CancellationToken ct = default);

    /// <summary>Jadvaldagi dars yozuvlari soni.</summary>
    Task<int> GetEntryCountAsync(int scheduleId, CancellationToken ct = default);
}

/// <summary><see cref="IScheduleSetService"/> implementatsiyasi.</summary>
public sealed class ScheduleSetService : IScheduleSetService
{
    private readonly IUnitOfWork _uow;
    private readonly IScheduleCardCopier? _cards;

    /// <summary>Yangi servis yaratadi.</summary>
    /// <param name="uow">Ish birligi (eski <c>ScheduleEntry</c> modeli).</param>
    /// <param name="cards">
    /// Kartochka (v2) nusxalovchisi. <c>null</c> bo'lsa (masalan Infrastructure
    /// ro'yxatdan o'tkazilmagan holatda) faqat eski yozuvlar nusxalanadi — mavjud
    /// <c>new ScheduleSetService(uow)</c> chaqiruvlari buzilmasin uchun IXTIYORIY.
    /// </param>
    public ScheduleSetService(IUnitOfWork uow, IScheduleCardCopier? cards = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _cards = cards;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Schedule>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await _uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);
        return all.OrderBy(s => s.AcademicYearId).ThenBy(s => s.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Schedule>> GetByAcademicYearAsync(
        int academicYearId, CancellationToken ct = default)
    {
        var all = await _uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);
        return all.Where(s => s.AcademicYearId == academicYearId).OrderBy(s => s.Id).ToList();
    }

    /// <inheritdoc />
    public Task<Schedule?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _uow.Schedules.GetByIdAsync(id, ct);

    /// <inheritdoc />
    public async Task<Schedule> CreateAsync(int academicYearId, string name, CancellationToken ct = default)
    {
        var year = await _uow.AcademicYears.GetByIdAsync(academicYearId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"O'quv yili topilmadi (ID: {academicYearId}).");

        var trimmed = Normalize(name);
        await EnsureNameFreeAsync(year.Id, trimmed, null, ct).ConfigureAwait(false);

        var created = await _uow.Schedules.AddAsync(new Schedule
        {
            AcademicYearId = year.Id,
            Name = trimmed,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        }, ct).ConfigureAwait(false);

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc />
    public async Task RenameAsync(int scheduleId, string name, CancellationToken ct = default)
    {
        var schedule = await _uow.Schedules.GetByIdAsync(scheduleId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Jadval topilmadi (ID: {scheduleId}).");

        var trimmed = Normalize(name);
        await EnsureNameFreeAsync(schedule.AcademicYearId, trimmed, scheduleId, ct).ConfigureAwait(false);

        schedule.Name = trimmed;
        await _uow.Schedules.UpdateAsync(schedule, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Schedule> DuplicateAsync(
        int scheduleId, string? newName = null, CancellationToken ct = default)
    {
        var source = await _uow.Schedules.GetByIdAsync(scheduleId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Nusxalanadigan jadval topilmadi (ID: {scheduleId}).");

        var name = string.IsNullOrWhiteSpace(newName)
            ? await SuggestCopyNameAsync(source, ct).ConfigureAwait(false)
            : Normalize(newName);

        await EnsureNameFreeAsync(source.AcademicYearId, name, null, ct).ConfigureAwait(false);

        var copy = await _uow.Schedules.AddAsync(new Schedule
        {
            AcademicYearId = source.AcademicYearId,
            Name = name,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        }, ct).ConfigureAwait(false);

        // Asl jadvalning yozuvlariga tegilmaydi — ularning nusxasi qo'shiladi.
        var entries = await _uow.ScheduleEntries.GetAllAsync(ct).ConfigureAwait(false);
        var sourceEntries = entries.Where(e => e.ScheduleId == scheduleId).ToList();
        foreach (var entry in sourceEntries)
        {
            await _uow.ScheduleEntries.AddAsync(new ScheduleEntry
            {
                ScheduleId = copy.Id,
                ClassGroupId = entry.ClassGroupId,
                SubjectId = entry.SubjectId,
                TeacherId = entry.TeacherId,
                DayOfWeek = entry.DayOfWeek,
                LessonNumber = entry.LessonNumber,
                RoomNumber = entry.RoomNumber
            }, ct).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        // Kartochkalar (v2) ham ko'chadi. Busiz ko'chirish bajarilgan bazada nusxa
        // /api/board va Desktop taxtasida BO'SH ko'rinardi — eski yozuvlar nusxalangani
        // bilan yangi model ularni ko'rmaydi.
        if (_cards is not null)
        {
            await _cards.CopyCardsAsync(scheduleId, copy.Id, ct).ConfigureAwait(false);
        }

        return copy;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int scheduleId, CancellationToken ct = default)
    {
        var schedule = await _uow.Schedules.GetByIdAsync(scheduleId, ct).ConfigureAwait(false);
        if (schedule is null)
        {
            return;
        }

        var all = await _uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);
        if (all.Count <= 1)
        {
            throw new InvalidOperationException(
                "Bu oxirgi dars jadvali — uni o'chirib bo'lmaydi. " +
                "Avval yangi jadval yarating, keyin bunisini o'chiring.");
        }

        var wasActive = schedule.IsActive;

        // Dars yozuvlari kaskad bo'yicha o'chadi.
        await _uow.Schedules.DeleteAsync(scheduleId, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        if (wasActive)
        {
            await ActiveScheduleResolver.GetActiveAsync(_uow, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<Schedule> GetActiveAsync(CancellationToken ct = default) =>
        ActiveScheduleResolver.GetActiveAsync(_uow, ct);

    /// <inheritdoc />
    public async Task<int> GetActiveIdAsync(CancellationToken ct = default)
    {
        var active = await ActiveScheduleResolver.GetActiveAsync(_uow, ct).ConfigureAwait(false);
        return active.Id;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Tranzaksiya va tartib majburiy</b> (00 §10.8, 3-band). <c>Schedules(IsActive)</c>
    /// ustida filtrlangan UNIQUE indeks bor, ya'ni ayni paytda faqat BITTA faol jadval
    /// bo'lishi mumkin. Shu sababli:
    /// <list type="number">
    /// <item>avval barcha faol jadvallar o'chiriladi (oraliq holat — 0 ta faol, indeks buzilmaydi);</item>
    /// <item>keyin maqsad jadval yoqiladi.</item>
    /// </list>
    /// Ikkalasi bitta tranzaksiyada: xato bo'lsa eski faol jadval joyida qoladi.
    /// Ilgari har jadval alohida <c>SaveChanges</c> bilan yangilanardi va oraliqda
    /// 2 ta faol jadval bo'lib qolardi — aynan shu sabab indeks qo'yilmagan edi.
    /// </remarks>
    /// <inheritdoc />
    public async Task SetActiveAsync(int scheduleId, CancellationToken ct = default)
    {
        var all = await _uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);
        var target = all.FirstOrDefault(s => s.Id == scheduleId)
            ?? throw new InvalidOperationException($"Jadval topilmadi (ID: {scheduleId}).");

        if (target.IsActive && all.Count(s => s.IsActive) == 1)
        {
            return;
        }

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            // 1-qadam: hamma faollik o'chiriladi (maqsad jadval ham).
            foreach (var schedule in all.Where(s => s.IsActive))
            {
                schedule.IsActive = false;
                await _uow.Schedules.UpdateAsync(schedule, token).ConfigureAwait(false);
            }

            // 2-qadam: faqat bittasi yoqiladi.
            target.IsActive = true;
            await _uow.Schedules.UpdateAsync(target, token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetEntryCountAsync(int scheduleId, CancellationToken ct = default)
    {
        var entries = await _uow.ScheduleEntries.GetAllAsync(ct).ConfigureAwait(false);
        return entries.Count(e => e.ScheduleId == scheduleId);
    }

    private static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Jadval nomi bo'sh bo'lishi mumkin emas.");
        }

        return name.Trim();
    }

    private async Task EnsureNameFreeAsync(int academicYearId, string name, int? exceptId, CancellationToken ct)
    {
        var all = await _uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);
        var clash = all.Any(s =>
            s.AcademicYearId == academicYearId &&
            (!exceptId.HasValue || s.Id != exceptId.Value) &&
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        if (clash)
        {
            throw new InvalidOperationException(
                $"Bu o'quv yilida «{name}» nomli jadval allaqachon mavjud.");
        }
    }

    /// <summary>"Asosiy jadval" → "Asosiy jadval (nusxa)", band bo'lsa "(nusxa 2)" va h.k.</summary>
    private async Task<string> SuggestCopyNameAsync(Schedule source, CancellationToken ct)
    {
        var all = await _uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);
        var taken = all
            .Where(s => s.AcademicYearId == source.AcademicYearId)
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidate = $"{source.Name} (nusxa)";
        var counter = 2;
        while (taken.Contains(candidate))
        {
            candidate = $"{source.Name} (nusxa {counter})";
            counter++;
        }

        return candidate;
    }
}
