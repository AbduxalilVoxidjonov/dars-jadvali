using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Xona (sinfxona, laboratoriya, sport zali, ...).
/// </summary>
/// <remarks>
/// <b>Xona moduli hozir P1:</b> maktabda xona ishlatilmaydi. Entity va FK'lar tayyor,
/// lekin hech qayerda MAJBURIY emas — xona ro'yxati butunlay bo'sh bo'lsa ham
/// dars jadvali to'liq ishlaydi (<c>Lesson.RequiredClassroomCount = 0</c>).
/// </remarks>
public class Classroom : BaseEntity, ISoftDeletable, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Xona nomi, masalan "101-xona".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Qisqartma, masalan "101".</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Sig'imi (o'quvchi soni).</summary>
    public int? Capacity { get; set; }

    /// <summary>Xona turi.</summary>
    public ClassroomKind Kind { get; set; } = ClassroomKind.Regular;

    /// <summary>Bir vaqtda bir nechta sinf ishlata oladimi.</summary>
    public bool IsShared { get; set; }

    /// <summary>Tashqi tizim identifikatori.</summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Eski <c>ScheduleEntry.RoomNumber</c> / <see cref="Card.LegacyRoomNumber"/> matni —
    /// <c>V2_07</c> ko'chirish izi.
    /// </summary>
    /// <remarks>
    /// Aynan shu ustundagi filtrlangan unikal indeks
    /// (<c>UX_Classrooms_AcademicYearId_LegacySourceName</c>) ko'chirishni
    /// <b>idempotent</b> qiladi: bir xil matndan ikkinchi xona hech qachon yaratilmaydi.
    /// Qisqartma (<see cref="ShortName"/>) bo'yicha solishtirish yetarli emas edi —
    /// u 24 belgigacha kesiladi va dublikatda raqam qo'shiladi.
    /// </remarks>
    public string? LegacySourceName { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }
}
