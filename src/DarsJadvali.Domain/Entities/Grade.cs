using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>Parallel (sinf darajasi), masalan "5-sinflar".</summary>
public class Grade : BaseEntity, ISoftDeletable, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Sinf darajasi raqami (1..11).</summary>
    public int GradeNo { get; set; }

    /// <summary>Nomi, masalan "5-sinflar".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Qisqartma, masalan "5".</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <summary>Shu paralleldagi sinflar.</summary>
    public ICollection<SchoolClass> SchoolClasses { get; set; } = new List<SchoolClass>();
}
