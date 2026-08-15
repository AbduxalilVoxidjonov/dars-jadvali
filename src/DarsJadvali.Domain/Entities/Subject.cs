using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>Fan.</summary>
public class Subject : BaseEntity, ISoftDeletable
{
    /// <summary>Fan nomi.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Qisqa kodi.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Jadvalda ko'rsatiladigan rang (HEX).</summary>
    public string ColorCode { get; set; } = "#455A64";

    /// <summary>Biriktirmalar.</summary>
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();

    /// <summary>Jadval yozuvlari.</summary>
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();

    // ---------------------------------------------------------------------
    // Sxema v2 kengaytmalari. Eski `Code` SAQLANADI (Application/Desktop/Web uni
    // ishlatadi), v2 kodi esa `ShortName` ni afzal ko'radi.
    // ---------------------------------------------------------------------

    /// <summary>O'quv yili Id. <c>null</c> — eski, yilga bog'lanmagan yozuv.</summary>
    public int? AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Qisqartma (v2). Bo'sh bo'lsa <see cref="Code"/> ishlatiladi.</summary>
    public string? ShortName { get; set; }

    /// <summary>Hafta bo'ylab taqsimlanish talabi.</summary>
    public SubjectDistribution Distribution { get; set; } = SubjectDistribution.None;

    /// <summary>Uyga vazifa beriladimi (ketma-ket kunlar cheklovi uchun).</summary>
    public bool NeedsHomework { get; set; }

    /// <summary>Guruhdagi maksimal o'quvchi soni.</summary>
    public int? MaxStudents { get; set; }

    /// <summary>Maxsus xona talab qiladimi (P1 — xona moduli hozir ishlatilmaydi).</summary>
    public bool RequiresSpecialClassroom { get; set; }

    /// <summary>Tashqi tizim identifikatori (aSc import/eksport uchun).</summary>
    public string? ExternalId { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }
}
