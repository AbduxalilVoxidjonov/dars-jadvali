using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Services;

/// <summary>Joylashtirish natijasi.</summary>
/// <param name="Placed">Qo'yildimi.</param>
/// <param name="Entry">Qo'yilgan yozuv (qo'yilmasa null).</param>
/// <param name="Validation">Validatsiya natijasi.</param>
public sealed record PlacementResult(bool Placed, ScheduleEntry? Entry, ValidationResult Validation);

/// <summary>Ommaviy joylashtirish natijasi ("hammasi yoki hech narsa").</summary>
/// <param name="Applied">Tranzaksiya commit bo'ldimi (barchasi yozildimi).</param>
/// <param name="Results">Har bir loyiha uchun natija — kirish bilan bir xil tartibda.</param>
public sealed record BulkPlacementResult(bool Applied, IReadOnlyList<PlacementResult> Results)
{
    /// <summary>Rad etilgan loyihalarning barcha konfliktlari.</summary>
    public IReadOnlyList<Conflict> Rejections => Results
        .Where(r => !r.Placed)
        .SelectMany(r => r.Validation.Conflicts)
        .ToList();
}

/// <summary>
/// Dars yozuvlari bilan ishlash servisi.
/// Barcha amallar bitta dars jadvali (varianti) doirasida bajariladi:
/// <c>scheduleId</c> berilmasa — faol jadval ishlatiladi.
/// Jadvalning o'zini (o'quv yili, variantlar) <see cref="IScheduleSetService"/> boshqaradi.
/// </summary>
public interface IScheduleService
{
    /// <summary>Butun jadval (faol yoki ko'rsatilgan jadval).</summary>
    Task<IReadOnlyList<ScheduleEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Butun jadval — aniq jadval varianti bo'yicha.</summary>
    Task<IReadOnlyList<ScheduleEntry>> GetAllAsync(int? scheduleId, CancellationToken ct = default);

    /// <summary>Sinf jadvali (faol jadval).</summary>
    Task<IReadOnlyList<ScheduleEntry>> GetByClassGroupAsync(int classGroupId, CancellationToken ct = default);

    /// <summary>Sinf jadvali — aniq jadval varianti bo'yicha.</summary>
    Task<IReadOnlyList<ScheduleEntry>> GetByClassGroupAsync(
        int classGroupId, int? scheduleId, CancellationToken ct = default);

    /// <summary>O'qituvchi jadvali (faol jadval).</summary>
    Task<IReadOnlyList<ScheduleEntry>> GetByTeacherAsync(int teacherId, CancellationToken ct = default);

    /// <summary>O'qituvchi jadvali — aniq jadval varianti bo'yicha.</summary>
    Task<IReadOnlyList<ScheduleEntry>> GetByTeacherAsync(
        int teacherId, int? scheduleId, CancellationToken ct = default);

    /// <summary>
    /// Validatsiyadan o'tkazadi; Error bo'lsa saqlamaydi.
    /// force=true bo'lsa Warning'larni e'tiborsiz qoldiradi (Error'ni emas).
    /// </summary>
    Task<PlacementResult> PlaceAsync(ScheduleEntryDraft draft, bool force = false, CancellationToken ct = default);

    /// <summary>
    /// Bir nechta darsni <b>bitta tranzaksiyada</b> joylashtiradi: bittasi ham qabul
    /// qilinmasa hech narsa yozilmaydi ("hammasi yoki hech narsa").
    /// </summary>
    /// <remarks>
    /// <b>Nima uchun kerak.</b> Undo/redo dagi <c>CompositeCommand</c> N ta harakatni
    /// qaytarayotganda <see cref="PlaceAsync"/> ni N marta chaqirardi — N ta alohida
    /// <c>SaveChanges</c> va N ta to'liq validatsiya. O'rtada xato chiqsa jadval
    /// <b>yarim qaytarilgan</b> holatda qolardi. Bu yerda hammasi
    /// <c>IUnitOfWork.ExecuteInTransactionAsync</c> ichida bajariladi.
    /// <para>
    /// Natija ro'yxati kirish bilan <b>bir xil tartibda</b> qaytadi. Biror loyiha rad
    /// etilsa tranzaksiya qaytariladi, lekin natijada har bir loyihaning sababi ko'rinadi.
    /// </para>
    /// </remarks>
    /// <param name="drafts">Joylashtiriladigan loyihalar.</param>
    /// <param name="force">Ogohlantirishlarni e'tiborsiz qoldirish (Error'ni emas).</param>
    Task<BulkPlacementResult> PlaceManyAsync(
        IReadOnlyList<ScheduleEntryDraft> drafts, bool force = false, CancellationToken ct = default);

    /// <summary>Mavjud darsni boshqa kun/soatga ko'chiradi (o'z jadvali ichida).</summary>
    Task<PlacementResult> MoveAsync(int entryId, WeekDay newDay, int newLessonNumber, bool force = false, CancellationToken ct = default);

    /// <summary>Darsni jadvaldan o'chiradi.</summary>
    Task RemoveAsync(int entryId, CancellationToken ct = default);

    /// <summary>Jadvalni tozalaydi (sinf ko'rsatilsa — faqat o'sha sinfnikini).</summary>
    Task ClearAsync(int? classGroupId = null, CancellationToken ct = default);

    /// <summary>Jadvalni tozalaydi — aniq jadval varianti bo'yicha.</summary>
    Task ClearAsync(int? classGroupId, int? scheduleId, CancellationToken ct = default);
}

/// <summary><see cref="IScheduleService"/> implementatsiyasi.</summary>
public sealed class ScheduleService : IScheduleService
{
    private readonly IUnitOfWork _uow;
    private readonly IScheduleValidator _validator;

    /// <summary>Yangi servis yaratadi.</summary>
    public ScheduleService(IUnitOfWork uow, IScheduleValidator validator)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduleEntry>> GetAllAsync(CancellationToken ct = default) =>
        GetAllAsync(null, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleEntry>> GetAllAsync(int? scheduleId, CancellationToken ct = default)
    {
        var entries = await LoadAsync(scheduleId, ct).ConfigureAwait(false);
        return Sort(entries);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduleEntry>> GetByClassGroupAsync(
        int classGroupId, CancellationToken ct = default) =>
        GetByClassGroupAsync(classGroupId, null, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleEntry>> GetByClassGroupAsync(
        int classGroupId, int? scheduleId, CancellationToken ct = default)
    {
        var entries = await LoadAsync(scheduleId, ct).ConfigureAwait(false);
        return Sort(entries.Where(e => e.ClassGroupId == classGroupId));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduleEntry>> GetByTeacherAsync(
        int teacherId, CancellationToken ct = default) =>
        GetByTeacherAsync(teacherId, null, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleEntry>> GetByTeacherAsync(
        int teacherId, int? scheduleId, CancellationToken ct = default)
    {
        var entries = await LoadAsync(scheduleId, ct).ConfigureAwait(false);
        return Sort(entries.Where(e => e.TeacherId == teacherId));
    }

    /// <inheritdoc />
    public async Task<PlacementResult> PlaceAsync(
        ScheduleEntryDraft draft, bool force = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var scheduleId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow, draft.ScheduleId, ct).ConfigureAwait(false);

        // Validatsiya ham aynan shu jadval ichida bajariladi.
        draft = draft with { ScheduleId = scheduleId };

        var validation = await _validator.ValidateAsync(draft, ct).ConfigureAwait(false);

        // Error har doim to'sadi; Warning faqat force=false bo'lganda to'sadi.
        // Qoida generator bilan AYNAN bir manbadan olinadi (05-audit K-06).
        if (!SchedulePlacementPolicy.IsAcceptable(validation, force))
        {
            return new PlacementResult(false, null, validation);
        }

        ScheduleEntry entry;
        if (draft.Id.HasValue)
        {
            var existing = await _uow.ScheduleEntries.GetByIdAsync(draft.Id.Value, ct).ConfigureAwait(false);
            if (existing is null)
            {
                var conflict = new Conflict(ConflictSeverity.Error, ConflictCodes.ClassBusy,
                    $"Ko'chiriladigan dars topilmadi (ID: {draft.Id.Value}).");
                return new PlacementResult(false, null, ValidationResult.From(new[] { conflict }));
            }

            existing.ScheduleId = scheduleId;
            existing.ClassGroupId = draft.ClassGroupId;
            existing.SubjectId = draft.SubjectId;
            existing.TeacherId = draft.TeacherId;
            existing.DayOfWeek = draft.DayOfWeek;
            existing.LessonNumber = draft.LessonNumber;
            existing.RoomNumber = draft.RoomNumber;

            await _uow.ScheduleEntries.UpdateAsync(existing, ct).ConfigureAwait(false);
            entry = existing;
        }
        else
        {
            entry = new ScheduleEntry
            {
                ScheduleId = scheduleId,
                ClassGroupId = draft.ClassGroupId,
                SubjectId = draft.SubjectId,
                TeacherId = draft.TeacherId,
                DayOfWeek = draft.DayOfWeek,
                LessonNumber = draft.LessonNumber,
                RoomNumber = draft.RoomNumber
            };

            entry = await _uow.ScheduleEntries.AddAsync(entry, ct).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return new PlacementResult(true, entry, validation);
    }

    /// <inheritdoc />
    public async Task<BulkPlacementResult> PlaceManyAsync(
        IReadOnlyList<ScheduleEntryDraft> drafts, bool force = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        if (drafts.Count == 0)
        {
            return new BulkPlacementResult(true, Array.Empty<PlacementResult>());
        }

        var scheduleId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow, drafts[0].ScheduleId, ct).ConfigureAwait(false);

        // Nusxa BIR MARTA yuklanadi: N ta loyiha uchun N ta to'liq o'qish qilinmaydi.
        var snapshot = await ScheduleSnapshot.LoadAsync(_uow, scheduleId, ct).ConfigureAwait(false);

        var results = new List<PlacementResult>(drafts.Count);
        var accepted = new List<ScheduleEntryDraft>(drafts.Count);

        foreach (var raw in drafts)
        {
            ct.ThrowIfCancellationRequested();

            var draft = raw with { ScheduleId = scheduleId };
            var validation = ScheduleValidator.Evaluate(draft, snapshot);

            if (!SchedulePlacementPolicy.IsAcceptable(validation, force))
            {
                results.Add(new PlacementResult(false, null, validation));
                continue;
            }

            // Keyingi loyihalar shu qarorni KO'RADI (aks holda ikkitasi bir slotga tushardi).
            snapshot.Apply(draft);
            accepted.Add(draft);
            results.Add(new PlacementResult(true, null, validation));
        }

        // Bittasi ham rad etilsa — hech narsa yozilmaydi ("hammasi yoki hech narsa").
        if (results.Any(r => !r.Placed))
        {
            return new BulkPlacementResult(false, results);
        }

        var entries = await _uow.ExecuteInTransactionAsync(async token =>
        {
            var written = new List<ScheduleEntry>(accepted.Count);
            foreach (var draft in accepted)
            {
                written.Add(await WriteAsync(draft, scheduleId, token).ConfigureAwait(false));
            }

            return written;
        }, ct).ConfigureAwait(false);

        for (var i = 0; i < results.Count; i++)
        {
            results[i] = results[i] with { Entry = entries[i] };
        }

        return new BulkPlacementResult(true, results);
    }

    /// <summary>
    /// Loyihani entity'ga yozadi (yangi yoki mavjudini yangilaydi). <c>SaveChanges</c>
    /// repozitoriy ichida bajariladi, lekin ochiq tranzaksiya bo'lsa u commit qilmaydi.
    /// </summary>
    private async Task<ScheduleEntry> WriteAsync(
        ScheduleEntryDraft draft, int scheduleId, CancellationToken ct)
    {
        if (draft.Id.HasValue)
        {
            var existing = await _uow.ScheduleEntries.GetByIdAsync(draft.Id.Value, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Ko'chiriladigan dars topilmadi (ID: {draft.Id.Value}).");

            existing.ScheduleId = scheduleId;
            existing.ClassGroupId = draft.ClassGroupId;
            existing.SubjectId = draft.SubjectId;
            existing.TeacherId = draft.TeacherId;
            existing.DayOfWeek = draft.DayOfWeek;
            existing.LessonNumber = draft.LessonNumber;
            existing.RoomNumber = draft.RoomNumber;

            await _uow.ScheduleEntries.UpdateAsync(existing, ct).ConfigureAwait(false);
            return existing;
        }

        return await _uow.ScheduleEntries.AddAsync(new ScheduleEntry
        {
            ScheduleId = scheduleId,
            ClassGroupId = draft.ClassGroupId,
            SubjectId = draft.SubjectId,
            TeacherId = draft.TeacherId,
            DayOfWeek = draft.DayOfWeek,
            LessonNumber = draft.LessonNumber,
            RoomNumber = draft.RoomNumber,
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PlacementResult> MoveAsync(
        int entryId, WeekDay newDay, int newLessonNumber, bool force = false, CancellationToken ct = default)
    {
        var existing = await _uow.ScheduleEntries.GetByIdAsync(entryId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            var conflict = new Conflict(ConflictSeverity.Error, ConflictCodes.ClassBusy,
                $"Ko'chiriladigan dars topilmadi (ID: {entryId}).");
            return new PlacementResult(false, null, ValidationResult.From(new[] { conflict }));
        }

        // Dars o'z jadvali ichida ko'chiriladi — boshqa variantga o'tib ketmaydi.
        var draft = new ScheduleEntryDraft(
            existing.Id,
            existing.ClassGroupId,
            existing.SubjectId,
            existing.TeacherId,
            newDay,
            newLessonNumber,
            existing.RoomNumber,
            existing.ScheduleId);

        return await PlaceAsync(draft, force, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(int entryId, CancellationToken ct = default)
    {
        await _uow.ScheduleEntries.DeleteAsync(entryId, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ClearAsync(int? classGroupId = null, CancellationToken ct = default) =>
        ClearAsync(classGroupId, null, ct);

    /// <inheritdoc />
    public async Task ClearAsync(int? classGroupId, int? scheduleId, CancellationToken ct = default)
    {
        // Faqat bitta jadval tozalanadi — boshqa yil/variant yozuvlariga tegilmaydi.
        var entries = await LoadAsync(scheduleId, ct).ConfigureAwait(false);
        var target = classGroupId.HasValue
            ? entries.Where(e => e.ClassGroupId == classGroupId.Value)
            : entries;

        var ids = target.Select(e => e.Id).ToList();
        foreach (var id in ids)
        {
            await _uow.ScheduleEntries.DeleteAsync(id, ct).ConfigureAwait(false);
        }

        if (ids.Count > 0)
        {
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>Bitta jadvalga tegishli yozuvlarni yuklaydi.</summary>
    private async Task<List<ScheduleEntry>> LoadAsync(int? scheduleId, CancellationToken ct)
    {
        var targetScheduleId = await ActiveScheduleResolver
            .ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);

        var all = await _uow.ScheduleEntries.GetAllAsync(ct).ConfigureAwait(false);
        return all.Where(e => e.ScheduleId == targetScheduleId).ToList();
    }

    private static IReadOnlyList<ScheduleEntry> Sort(IEnumerable<ScheduleEntry> entries) =>
        entries
            .OrderBy(e => (int)e.DayOfWeek)
            .ThenBy(e => e.LessonNumber)
            .ThenBy(e => e.ClassGroupId)
            .ToList();
}
