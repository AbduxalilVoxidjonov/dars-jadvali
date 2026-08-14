namespace DarsJadvali.Application.Validation;

/// <summary>Jadval validatori.</summary>
public interface IScheduleValidator
{
    /// <summary>
    /// Bitta loyihani tekshiradi. Konfliktlar faqat loyihaning jadvali ichida qidiriladi
    /// (<c>draft.ScheduleId</c>, u <c>null</c> bo'lsa — faol jadval).
    /// </summary>
    Task<ValidationResult> ValidateAsync(ScheduleEntryDraft draft, CancellationToken ct = default);

    /// <summary>Mavjud butun jadvalni tekshiradi (faol jadval).</summary>
    Task<ValidationResult> ValidateAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Mavjud butun jadvalni tekshiradi — aniq jadval varianti bo'yicha
    /// (<paramref name="scheduleId"/> — <c>null</c> bo'lsa faol jadval).
    /// </summary>
    Task<ValidationResult> ValidateAllAsync(int? scheduleId, CancellationToken ct = default);
}
