using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Sinf, masalan "5-A". Eski <see cref="ClassGroup"/> ning v2 varianti.
/// </summary>
/// <remarks>
/// Nom ataylab o'zgartirildi: eski <c>ClassGroup</c> "sinf" ham, "guruh" ham degandek
/// tushunilardi. Endi <see cref="SchoolClass"/> = sinf, <see cref="StudentGroup"/> = guruh.
/// <para>
/// 1-bosqich additiv: <see cref="ClassGroup"/> o'chirilmaydi, ikkalasi
/// <see cref="LegacyClassGroupId"/> orqali bog'lanadi.
/// </para>
/// </remarks>
public class SchoolClass : BaseEntity, ISoftDeletable, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Sinf nomi, masalan "5-A".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Qisqartma, masalan "5A".</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>Parallel Id.</summary>
    public int? GradeId { get; set; }

    /// <summary>Parallel.</summary>
    public Grade? Grade { get; set; }

    /// <summary>
    /// Smena Id. Har sinf o'z smenasiga tegishli (tasdiqlangan qaror: maktabda 2 smena).
    /// </summary>
    public int? ShiftId { get; set; }

    /// <summary>Smena.</summary>
    public Shift? Shift { get; set; }

    /// <summary>Sinf rahbari Id (chop etishdagi <c>{class_teacher}</c> tokeni uchun).</summary>
    public int? ClassTeacherId { get; set; }

    /// <summary>Sinf rahbari.</summary>
    public Teacher? ClassTeacher { get; set; }

    /// <summary>Asosiy (biriktirilgan) xona Id. Xona moduli P1 — bo'sh bo'lishi mumkin.</summary>
    public int? HomeClassroomId { get; set; }

    /// <summary>Asosiy xona.</summary>
    public Classroom? HomeClassroom { get; set; }

    /// <summary>O'qitish tili.</summary>
    public string? Language { get; set; }

    /// <summary>O'quvchilar soni.</summary>
    public int StudentCount { get; set; }

    /// <summary>Tashqi tizim identifikatori (aSc import/eksport uchun).</summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Eski <see cref="ClassGroup"/> Id — ma'lumot ko'chirish (backfill) izi.
    /// Keyingi bosqichda eski model o'chirilgach bu ustun ham o'chadi.
    /// </summary>
    public int? LegacyClassGroupId { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <summary>Shu sinfning bo'linishlari (butun sinf, 1/2 guruh, o'g'il/qiz, ...).</summary>
    public ICollection<ClassDivision> Divisions { get; set; } = new List<ClassDivision>();

    /// <summary>Shu sinfning barcha guruhlari (barcha bo'linishlar bo'ylab).</summary>
    public ICollection<StudentGroup> StudentGroups { get; set; } = new List<StudentGroup>();
}
