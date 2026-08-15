namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// Karta — atomik joylashtiriladigan birlik (02-asc-.., 4.1).
/// Bir <see cref="LessonDef"/> dan <c>PeriodsPerWeek / PeriodsPerCard</c> ta karta hosil bo'ladi.
/// Karta bitta kun ichida <see cref="Length"/> ta ketma-ket soatni egallaydi (C-DBL-01).
/// </summary>
public sealed class Card
{
    public int Id { get; internal set; } = -1;
    public int LessonId { get; internal set; } = -1;
    public int SubjectId { get; internal set; } = -1;

    /// <summary>1 = single, 2 = double, 3 = triple.</summary>
    public int Length { get; internal set; } = 1;

    public int[] TeacherIds { get; internal set; } = Array.Empty<int>();
    public int[] GroupIds { get; internal set; } = Array.Empty<int>();

    /// <summary>Kartaga tegishli sinflar (guruhlardan hosil qilingan, takrorsiz).</summary>
    public int[] ClassIds { get; internal set; } = Array.Empty<int>();

    /// <summary>
    /// <see cref="ClassIds"/> bilan parallel massiv: har sinfda kartaning bo'linish tag'i.
    /// Bir slotda bir sinfda faqat BIR XIL tag'li kartalar tura oladi (C-GBL-08).
    /// </summary>
    public int[] ClassDivisionTags { get; internal set; } = Array.Empty<int>();

    /// <summary>C-ROM-01 — ruxsat etilgan xonalar. Bo'sh = xona kerak emas.</summary>
    public int[] AllowedRoomIds { get; internal set; } = Array.Empty<int>();

    public bool NeedsRoom => AllowedRoomIds.Length > 0;

    public int StudentCount { get; internal set; }

    /// <summary>Boshlang'ich (statik) domain — preprocessingdan oldin.</summary>
    public SlotMask BaseDomain { get; internal set; }

    /// <summary>Propagation'dan keyingi ruxsat etilgan boshlang'ich slotlar.</summary>
    public SlotMask Domain { get; internal set; }

    /// <summary>C-AVL-06 — `?` belgilangan slotlar (ruxsat, lekin jarimali).</summary>
    public SlotMask QuestionMarked { get; internal set; }

    /// <summary>C-GBL-06 — qulflangan karta.</summary>
    public bool IsLocked { get; internal set; }

    public int LockedSlot { get; internal set; } = -1;
    public int LockedRoom { get; internal set; } = -1;

    /// <summary>Konflikt hisoblagichi (#3982 "hardest cards" — aging/breakout mexanizmi).</summary>
    public int ConflictCount { get; internal set; }

    /// <summary>MRV + degree kompozit bahosi.</summary>
    public double Difficulty { get; internal set; }

    /// <summary>Shu karta bilan resurs baham ko'radigan kartalar soni (degree evristikasi).</summary>
    public int Degree { get; internal set; }

    /// <summary>C-DST-04 — taqsimot tekshiruvidan chiqarilgan.</summary>
    public bool SkipDistribution { get; internal set; }

    public override string ToString() => $"Card#{Id} (lesson {LessonId}, len {Length})";
}
