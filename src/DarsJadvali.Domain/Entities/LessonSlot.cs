using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>Dars soati raqamini real vaqtga bog'laydi.</summary>
public class LessonSlot : BaseEntity
{
    /// <summary>Dars soati raqami (1..N, unikal).</summary>
    public int LessonNumber { get; set; }

    /// <summary>Boshlanish vaqti.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Tugash vaqti.</summary>
    public TimeSpan EndTime { get; set; }
}
