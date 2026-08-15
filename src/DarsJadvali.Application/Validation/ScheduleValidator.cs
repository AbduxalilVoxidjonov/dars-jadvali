using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Scheduling;

namespace DarsJadvali.Application.Validation;

/// <summary>Jadval qoidalarini tekshiruvchi asosiy implementatsiya.</summary>
public sealed class ScheduleValidator : IScheduleValidator
{
    private readonly IUnitOfWork _uow;
    private readonly ISchedulingStore? _cards;

    /// <summary>Yangi validator yaratadi.</summary>
    /// <param name="uow">Ish birligi (eski <c>ScheduleEntry</c> modeli).</param>
    /// <param name="cards">
    /// Kartochka (v2) manbasi — <c>GROUP_DIVISION_OVERLAP</c> tekshiruvi uchun.
    /// <c>null</c> bo'lsa (masalan Infrastructure ro'yxatdan o'tkazilmagan holatda)
    /// qolgan qoidalar avvalgidek ishlaydi, faqat shu bitta tekshiruv o'tkazib
    /// yuboriladi. Ataylab IXTIYORIY: mavjud <c>new ScheduleValidator(uow)</c>
    /// chaqiruvlari buzilmasin.
    /// </param>
    public ScheduleValidator(IUnitOfWork uow, ISchedulingStore? cards = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _cards = cards;
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateAsync(ScheduleEntryDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var snapshot = await ScheduleSnapshot.LoadAsync(_uow, draft.ScheduleId, ct).ConfigureAwait(false);
        return ValidateAgainst(snapshot, draft);
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAllAsync(CancellationToken ct = default) =>
        ValidateAllAsync(null, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Eski <c>ScheduleEntry</c> qoidalaridan tashqari <b>kartochka (v2) modelidagi</b>
    /// <c>GROUP_DIVISION_OVERLAP</c> ham shu yerda tekshiriladi — u yagona qoida bo'lib,
    /// uni baza indeksi ushlay olmaydi (00 §2.7, §10.3). Ilgari bu tekshiruv faqat
    /// generatsiya yo'lida chaqirilardi, ya'ni QO'LDA tahrirlangan yoki import qilingan
    /// jadval umumiy tekshiruvdan bemalol o'tib ketardi.
    /// </remarks>
    public async Task<ValidationResult> ValidateAllAsync(int? scheduleId, CancellationToken ct = default)
    {
        var snapshot = await ScheduleSnapshot.LoadAsync(_uow, scheduleId, ct).ConfigureAwait(false);
        var result = ValidateAll(snapshot, ct);

        if (_cards is null) return result;

        var placed = await _cards.LoadPlacedCardsAsync(snapshot.ScheduleId, ct).ConfigureAwait(false);
        return Merge(result, GroupDivisionOverlapValidator.Check(placed));
    }

    /// <summary>
    /// Loyihani XOTIRADAGI nusxaga qarab baholaydi — bazaga umuman murojaat qilmaydi.
    /// </summary>
    /// <remarks>
    /// Prezentatsiya qatlami (drag-drop) uchun mo'ljallangan public kirish nuqtasi:
    /// nusxa <see cref="IScheduleSnapshotProvider"/> orqali bir marta yuklanadi, keyin
    /// har bir harakat shu metod bilan baholanadi. Natijada baholash qoidasi
    /// <see cref="ScheduleSnapshot.Validate"/> da — YAGONA manbada — qoladi.
    /// </remarks>
    /// <param name="draft">Baholanayotgan joylashtirish.</param>
    /// <param name="snapshot">Oldindan yuklangan nusxa.</param>
    public static ValidationResult Evaluate(ScheduleEntryDraft draft, ScheduleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(snapshot);

        var conflicts = snapshot.Validate(draft);
        return conflicts.Count == 0 ? ValidationResult.Success() : ValidationResult.From(conflicts);
    }

    /// <summary>Xotiradagi nusxaga qarab bitta loyihani tekshiradi.</summary>
    internal static ValidationResult ValidateAgainst(ScheduleSnapshot snapshot, ScheduleEntryDraft draft)
        => Evaluate(draft, snapshot);

    /// <summary>Xotiradagi nusxadagi butun jadvalni tekshiradi (bazaga murojaat qilmaydi).</summary>
    public static ValidationResult ValidateAll(ScheduleSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var all = new List<Conflict>();
        var seen = new HashSet<(string Code, string Message)>();

        // Ro'yxat tekshiruv davomida o'zgarmaydi, lekin nusxa olib yuramiz.
        foreach (var entry in snapshot.Entries.ToList())
        {
            ct.ThrowIfCancellationRequested();

            var draft = new ScheduleEntryDraft(
                entry.Id,
                entry.ClassGroupId,
                entry.SubjectId,
                entry.TeacherId,
                entry.DayOfWeek,
                entry.LessonNumber,
                entry.RoomNumber,
                snapshot.ScheduleId);

            foreach (var conflict in snapshot.Validate(draft))
            {
                if (seen.Add((conflict.Code, conflict.Message)))
                {
                    all.Add(conflict);
                }
            }
        }

        return all.Count == 0 ? ValidationResult.Success() : ValidationResult.From(all);
    }

    /// <summary>
    /// Xotiradagi nusxa + kartochka ko'rinishlari bo'yicha to'liq tekshiruv
    /// (bazaga murojaat qilmaydi).
    /// </summary>
    /// <remarks>
    /// Prezentatsiya qatlami uchun: nusxa va kartochkalar bir marta yuklanadi, keyin
    /// <c>GROUP_DIVISION_OVERLAP</c> ham shu yerda — takrorlanmagan holda — baholanadi.
    /// </remarks>
    /// <param name="snapshot">Oldindan yuklangan nusxa.</param>
    /// <param name="placedCards">Joylashtirilgan kartochkalar (v2).</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    public static ValidationResult ValidateAll(
        ScheduleSnapshot snapshot,
        IReadOnlyList<PlacedCardView> placedCards,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placedCards);
        return Merge(ValidateAll(snapshot, ct), GroupDivisionOverlapValidator.Check(placedCards));
    }

    /// <summary>Ikki natijani birlashtiradi (kod + xabar bo'yicha takrorsiz).</summary>
    private static ValidationResult Merge(ValidationResult result, IReadOnlyList<Conflict> extra)
    {
        if (extra.Count == 0) return result;

        var all = new List<Conflict>(result.Conflicts);
        var seen = all.Select(c => (c.Code, c.Message)).ToHashSet();

        foreach (var conflict in extra)
        {
            if (seen.Add((conflict.Code, conflict.Message))) all.Add(conflict);
        }

        return ValidationResult.From(all);
    }
}
