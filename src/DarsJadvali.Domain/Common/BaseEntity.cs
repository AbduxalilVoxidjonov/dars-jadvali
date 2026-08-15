namespace DarsJadvali.Domain.Common;

/// <summary>Barcha entity'lar uchun asosiy sinf.</summary>
public abstract class BaseEntity
{
    /// <summary>Yagona identifikator (ichki, avtoinkrement).</summary>
    public int Id { get; set; }

    /// <summary>
    /// Barqaror tashqi kalit — import/eksport, Desktop↔Web sinxronizatsiyasi va
    /// aSc XML moslashtirish uchun. Ichki <see cref="Id"/> o'zgarsa ham o'zgarmaydi.
    /// </summary>
    public Guid Uid { get; set; } = Guid.NewGuid();

    /// <summary>Yaratilgan payti (UTC). <c>AppDbContext.SaveChanges</c> avtomatik to'ldiradi.</summary>
    /// <remarks>
    /// Nomi ataylab <c>CreatedAt</c> emas: mavjud <c>Schedule</c> entity'sida
    /// <c>DateTime CreatedAt</c> ustuni bor va uni buzmaslik shart (1-bosqich additiv).
    /// </remarks>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Oxirgi o'zgartirilgan payti (UTC). Hech qachon o'zgarmagan bo'lsa <c>null</c>.</summary>
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Konkurentlik tokeni. SQLite'da haqiqiy <c>rowversion</c> yo'q — har saqlashda
    /// yangi <see cref="Guid"/> yoziladi. PostgreSQL'ga ko'chganda <c>xmin</c> ga almashtiriladi.
    /// </summary>
    /// <remarks>
    /// <c>IsConcurrencyToken()</c> faqat <see cref="IConcurrencyAware"/> ni implement qilgan
    /// (ya'ni sxema v2) entity'larda yoqiladi — eski entity'larning yangilash yo'llari
    /// (detached <c>Update</c>) buzilmasligi uchun.
    /// </remarks>
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}

/// <summary>
/// "Yumshoq o'chirish" qo'llab-quvvatlaydigan ma'lumotnomalar uchun.
/// <c>IsDeleted</c> ataylab <see cref="BaseEntity"/> da emas — aks holda
/// <c>Card</c>/<c>CardOccurrence</c> indekslariga ortiqcha ustun tushardi.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Yozuv o'chirilganmi.</summary>
    bool IsDeleted { get; set; }
}

/// <summary>
/// <see cref="BaseEntity.RowVersion"/> ni konkurentlik tokeni sifatida ishlatadigan
/// entity'lar uchun marker. Sxema v2 entity'lari shuni implement qiladi.
/// </summary>
public interface IConcurrencyAware
{
}
