using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>O'qituvchi.</summary>
public class Teacher : BaseEntity, ISoftDeletable
{
    /// <summary>To'liq ism-sharifi.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Telefon raqami.</summary>
    public string? Phone { get; set; }

    /// <summary>Jadvalda ko'rsatiladigan rang (HEX).</summary>
    public string ColorCode { get; set; } = "#1976D2";

    /// <summary>Faol yoki faol emas.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Biriktirmalar.</summary>
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();

    /// <summary>Ish vaqti oraliqlari.</summary>
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();

    /// <summary>Jadval yozuvlari.</summary>
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();

    // ---------------------------------------------------------------------
    // Sxema v2 kengaytmalari
    // ---------------------------------------------------------------------

    /// <summary>O'quv yili Id. <c>null</c> — eski, yilga bog'lanmagan yozuv.</summary>
    public int? AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Qisqartma, masalan "Aliyev V." — chop etishdagi <c>{teachers}</c> tokeni uchun.</summary>
    public string? ShortName { get; set; }

    /// <summary>Ismi.</summary>
    public string? FirstName { get; set; }

    /// <summary>Familiyasi.</summary>
    public string? LastName { get; set; }

    /// <summary>Elektron pochta.</summary>
    public string? Email { get; set; }

    /// <summary>Jinsi.</summary>
    public Gender? Gender { get; set; }

    // --- yuklama nazorati (tasdiqlangan qaror 4) --------------------------

    /// <summary>
    /// Shartnoma bo'yicha haftalik soat (stavka soati). Haqiqiy yuklama —
    /// <c>SUM(Card.PeriodsPerCard)</c> — shu son bilan solishtiriladi.
    /// </summary>
    public int? ContractPeriodsPerWeek { get; set; }

    /// <summary>
    /// Stavka ulushi: <c>1.0</c> = to'liq stavka, <c>0.5</c> = yarim.
    /// Hisobotda "shartnoma soati × stavka" ko'rinishida ishlatiladi.
    /// </summary>
    public decimal? ContractRate { get; set; }

    /// <summary>Kunlik maksimal dars soati (C-TCH-01).</summary>
    public int? MaxLessonsPerDay { get; set; }

    /// <summary>
    /// Kunlik maksimal "oyna" (bo'sh soat) soni (C-TCH-02).
    /// Oyna hisobi <b>smenalar bo'ylab yaxlit</b> ko'riladi — <c>Period.PeriodNo</c>
    /// smenalar bo'ylab uzluksiz raqamlangani uchun bu avtomatik ta'minlanadi.
    /// </summary>
    public int? MaxGapsPerDay { get; set; }

    /// <summary>Bo'sh o'rin (vakansiya) — hali o'qituvchi tayinlanmagan.</summary>
    public bool IsVacancy { get; set; }

    /// <summary>Tashqi tizim identifikatori (aSc import/eksport uchun).</summary>
    public string? ExternalId { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }
}
