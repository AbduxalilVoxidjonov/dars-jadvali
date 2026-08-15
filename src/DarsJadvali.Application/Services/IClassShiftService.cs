using DarsJadvali.Application.Abstractions;

namespace DarsJadvali.Application.Services;

/// <summary>Smena tanlagichidagi bitta variant.</summary>
/// <param name="ShiftId">Smena Id. <c>null</c> — "smena tayinlanmagan".</param>
/// <param name="ShiftNo">Smena raqami (1, 2, ...). Tayinlanmaganda 0.</param>
/// <param name="Name">Ko'rinadigan nom, masalan "1-smena".</param>
public sealed record ClassShiftOption(int? ShiftId, int ShiftNo, string Name)
{
    /// <summary>"Smena tayinlanmagan" varianti.</summary>
    public static ClassShiftOption None { get; } = new(null, 0, "Smena tayinlanmagan");
}

/// <summary>Bitta sinf va uning joriy smenasi.</summary>
/// <param name="SchoolClassId">Yangi (v2) sinf Id — <c>SchoolClass.Id</c>.</param>
/// <param name="LegacyClassGroupId">
/// Eski <c>ClassGroup.Id</c>. Sinflar ekrani hamon eski modelda ishlagani uchun
/// ikkalasini bog'laydigan yagona ko'prik shu maydon.
/// </param>
/// <param name="ClassName">Sinf nomi ("5-A").</param>
/// <param name="ShiftId">Joriy smena Id (<c>null</c> — tayinlanmagan).</param>
/// <param name="ShiftName">Joriy smena nomi (tayinlanmaganda bo'sh satr).</param>
public sealed record ClassShiftView(
    int SchoolClassId,
    int? LegacyClassGroupId,
    string ClassName,
    int? ShiftId,
    string ShiftName);

/// <summary>Smenani o'zgartirish natijasi.</summary>
/// <param name="Changed">Yozildimi.</param>
/// <param name="Message">Foydalanuvchiga ko'rsatiladigan o'zbekcha xabar.</param>
public sealed record ClassShiftChangeResult(bool Changed, string Message)
{
    /// <summary>Muvaffaqiyatli natija.</summary>
    public static ClassShiftChangeResult Ok(string message) => new(true, message);

    /// <summary>Rad etilgan natija.</summary>
    public static ClassShiftChangeResult Fail(string message) => new(false, message);
}

/// <summary>
/// Sinfning smenasini o'qiydigan va o'zgartiradigan servis.
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> <see cref="ISchedulingStore.SetClassShiftAsync"/> bazada
/// ancha vaqtdan beri bor va testlangan, lekin uni chaqiradigan Application servisi ham,
/// UI iste'molchisi ham yo'q edi. Natijada eski modeldan ko'chirish (backfill) barcha
/// sinfni 1-smenaga qo'ygan holicha qolib ketardi va foydalanuvchi buni o'zgartira
/// olmasdi — 2-smenada o'qiydigan sinflarning darslari noto'g'ri soatlarga tushardi.
/// <para>
/// O'qish <see cref="ISchedulingStore.LoadAsync"/> orqali: sinflar va smenalar shu yerda
/// bir marta, jadval qamrovida o'qiladi (alohida repozitoriy Application'da yo'q).
/// </para>
/// </remarks>
public interface IClassShiftService
{
    /// <summary>Jadval tegishli o'quv yilidagi barcha smenalar (+ "tayinlanmagan").</summary>
    Task<IReadOnlyList<ClassShiftOption>> GetShiftsAsync(
        int? scheduleId = null, CancellationToken ct = default);

    /// <summary>Barcha sinflar va ularning joriy smenasi.</summary>
    Task<IReadOnlyList<ClassShiftView>> GetClassShiftsAsync(
        int? scheduleId = null, CancellationToken ct = default);

    /// <summary>
    /// Sinfning smenasini o'zgartiradi. Begona o'quv yili smenasi tanlansa yoki sinf
    /// topilmasa — <c>Changed = false</c> va tushunarli o'zbekcha sabab qaytadi.
    /// </summary>
    Task<ClassShiftChangeResult> SetShiftAsync(
        int schoolClassId, int? shiftId, CancellationToken ct = default);
}

/// <summary><see cref="IClassShiftService"/> implementatsiyasi.</summary>
public sealed class ClassShiftService : IClassShiftService
{
    private readonly IUnitOfWork _uow;
    private readonly ISchedulingStore _store;

    /// <summary>Yangi servis yaratadi.</summary>
    public ClassShiftService(IUnitOfWork uow, ISchedulingStore store)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassShiftOption>> GetShiftsAsync(
        int? scheduleId = null, CancellationToken ct = default)
    {
        var input = await LoadAsync(scheduleId, ct).ConfigureAwait(false);

        var options = new List<ClassShiftOption> { ClassShiftOption.None };
        options.AddRange(input.Shifts
            .OrderBy(s => s.ShiftNo)
            .ThenBy(s => s.Id)
            .Select(s => new ClassShiftOption(
                s.Id,
                s.ShiftNo,
                string.IsNullOrWhiteSpace(s.Name) ? $"{s.ShiftNo}-smena" : s.Name)));

        return options;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassShiftView>> GetClassShiftsAsync(
        int? scheduleId = null, CancellationToken ct = default)
    {
        var input = await LoadAsync(scheduleId, ct).ConfigureAwait(false);
        var shiftNames = input.Shifts.ToDictionary(
            s => s.Id,
            s => string.IsNullOrWhiteSpace(s.Name) ? $"{s.ShiftNo}-smena" : s.Name);

        return input.Classes
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .Select(c => new ClassShiftView(
                c.Id,
                c.LegacyClassGroupId,
                c.Name,
                c.ShiftId,
                c.ShiftId is int id && shiftNames.TryGetValue(id, out var name) ? name : string.Empty))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ClassShiftChangeResult> SetShiftAsync(
        int schoolClassId, int? shiftId, CancellationToken ct = default)
    {
        if (schoolClassId <= 0)
        {
            return ClassShiftChangeResult.Fail(
                "Sinf tanlanmagan — avval ro'yxatdan sinfni tanlang.");
        }

        var written = await _uow
            .ExecuteInTransactionAsync(token => _store.SetClassShiftAsync(schoolClassId, shiftId, token), ct)
            .ConfigureAwait(false);

        if (written)
        {
            return ClassShiftChangeResult.Ok(shiftId is null
                ? "Sinf smenadan chiqarildi."
                : "Sinf smenasi yangilandi.");
        }

        // Store faqat ikki holatda false qaytaradi: sinf yo'q yoki smena boshqa o'quv yiliga tegishli.
        return ClassShiftChangeResult.Fail(
            "Smenani o'zgartirib bo'lmadi: tanlangan smena shu sinfning o'quv yiliga tegishli emas " +
            "yoki sinf topilmadi. Smenalarni o'quv yili sozlamalaridan tekshiring.");
    }

    private async Task<Scheduling.SchedulingInput> LoadAsync(int? scheduleId, CancellationToken ct)
    {
        var id = await ActiveScheduleResolver.ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);
        return await _store.LoadAsync(id, ct).ConfigureAwait(false);
    }
}
