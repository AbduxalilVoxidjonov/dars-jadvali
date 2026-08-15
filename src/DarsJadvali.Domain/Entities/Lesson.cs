using DarsJadvali.Domain.Common;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Dars TA'RIFI: <b>nima</b> o'qitiladi, <b>kimga</b>, <b>kim</b> tomonidan va
/// haftada <b>necha soat</b>. Joylashtirish (qaysi kun, qaysi soat) — bu yerda EMAS,
/// u <see cref="Card"/> da.
/// </summary>
/// <remarks>
/// Bu eski model yo'qotgan o'rta qatlam: <c>ScheduleEntry</c> reja va faktni bitta qatorga
/// siqib qo'ygan edi. Endi "5-A, Matematika: 5 soatdan 3 tasi qo'yildi" hisoblanadi
/// (<see cref="PeriodsPerWeek"/> vs <c>COUNT(Card)</c>).
/// <para>
/// Guruh bo'linishi ikkala stsenariyda ham shu yerda modellanadi:
/// <list type="bullet">
/// <item>(a) bir xil fan ikkiga bo'linadi — <b>ikkita</b> <see cref="Lesson"/>, bir xil
/// <c>SubjectId</c>, turli <c>LessonTeacher</c>, turli <c>LessonGroup</c>;</item>
/// <item>(b) turli fanlar parallel — ikkita <see cref="Lesson"/>, turli <c>SubjectId</c>,
/// turli <c>LessonGroup</c>.</item>
/// </list>
/// </para>
/// </remarks>
public class Lesson : BaseEntity, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Fan Id.</summary>
    public int SubjectId { get; set; }

    /// <summary>Fan.</summary>
    public Subject? Subject { get; set; }

    /// <summary>Haftalik soat (reja).</summary>
    public int PeriodsPerWeek { get; set; }

    /// <summary>
    /// Bitta kartochka necha soatni egallaydi: <c>1</c> — oddiy dars,
    /// <c>2</c> — <b>juft dars</b> (ketma-ket ikki soat), <c>3</c>, ...
    /// </summary>
    public int PeriodsPerCard { get; set; } = 1;

    /// <summary>Ruxsat etilgan kunlar bitmask'i. <c>0</c> = cheklov yo'q.</summary>
    public int AllowedDaysMask { get; set; }

    /// <summary>Ruxsat etilgan haftalar bitmask'i (A/B hafta). <c>0</c> = har hafta.</summary>
    public int AllowedWeeksMask { get; set; }

    /// <summary>Joylashtirish ustuvorligi (katta = avvalroq qo'yiladi).</summary>
    public int Priority { get; set; }

    /// <summary>Talab qilinadigan xona soni. <c>0</c> = xona kerak emas (P1 standarti).</summary>
    public int RequiredClassroomCount { get; set; }

    /// <summary>Tashqi tizim identifikatori.</summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Eski <see cref="TeacherAssignment"/> Id — ma'lumot ko'chirish izi.
    /// Backfill takror ishga tushsa dublikat yaratmasligi uchun ham kerak.
    /// </summary>
    public int? LegacyTeacherAssignmentId { get; set; }

    /// <summary>Shu darsni o'tadigan o'qituvchilar (1..N — birgalikda o'qitish).</summary>
    public ICollection<LessonTeacher> Teachers { get; set; } = new List<LessonTeacher>();

    /// <summary>Shu dars tegishli sinflar (1..N — birlashtirilgan sinflar).</summary>
    public ICollection<LessonClass> Classes { get; set; } = new List<LessonClass>();

    /// <summary>Shu dars o'tiladigan guruhlar.</summary>
    public ICollection<LessonGroup> Groups { get; set; } = new List<LessonGroup>();

    /// <summary>Ruxsat etilgan xonalar (P1).</summary>
    public ICollection<LessonClassroom> Classrooms { get; set; } = new List<LessonClassroom>();

    /// <summary>Shu darsning joylashtirilgan kartochkalari.</summary>
    public ICollection<Card> Cards { get; set; } = new List<Card>();
}
