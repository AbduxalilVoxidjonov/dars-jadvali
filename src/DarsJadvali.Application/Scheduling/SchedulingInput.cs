using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Scheduling;

/// <summary>
/// Bitta jadval varianti uchun generatsiyaga kerak bo'ladigan BARCHA EF ma'lumoti.
/// </summary>
/// <remarks>
/// Bir marta, qamrovi aniq so'rovlar bilan o'qiladi (05-audit K-07: "har validatsiyada
/// butun bazani qayta o'qish" muammosi). Bu obyekt faqat o'qish uchun — hech bir
/// maydoni o'zgartirilmaydi.
/// </remarks>
public sealed record SchedulingInput
{
    /// <summary>Maqsad jadval varianti (chorak shu yerda).</summary>
    public required Schedule Schedule { get; init; }

    /// <summary>Jadval tegishli o'quv yili.</summary>
    public required AcademicYear Year { get; init; }

    /// <summary>Ish kunlari (faol/nofaol, kunlik chegaralar).</summary>
    public required IReadOnlyList<WorkDay> WorkDays { get; init; }

    /// <summary>Dars soatlari (qo'ng'iroq jadvali), <c>PeriodNo</c> smenalar bo'ylab uzluksiz.</summary>
    public required IReadOnlyList<Period> Periods { get; init; }

    /// <summary>Smenalar.</summary>
    public required IReadOnlyList<Shift> Shifts { get; init; }

    /// <summary>Sinflar.</summary>
    public required IReadOnlyList<SchoolClass> Classes { get; init; }

    /// <summary>Sinf bo'linishlari (<c>divisiontag</c>).</summary>
    public required IReadOnlyList<ClassDivision> Divisions { get; init; }

    /// <summary>O'quvchilar guruhlari.</summary>
    public required IReadOnlyList<StudentGroup> Groups { get; init; }

    /// <summary>O'qituvchilar.</summary>
    public required IReadOnlyList<Teacher> Teachers { get; init; }

    /// <summary>Fanlar.</summary>
    public required IReadOnlyList<Subject> Subjects { get; init; }

    /// <summary>Xonalar (P1 — bo'sh bo'lishi mumkin).</summary>
    public required IReadOnlyList<Classroom> Classrooms { get; init; }

    /// <summary>Dars ta'riflari (reja).</summary>
    public required IReadOnlyList<Lesson> Lessons { get; init; }

    /// <summary>Dars ↔ o'qituvchi.</summary>
    public required IReadOnlyList<LessonTeacher> LessonTeachers { get; init; }

    /// <summary>Dars ↔ sinf.</summary>
    public required IReadOnlyList<LessonClass> LessonClasses { get; init; }

    /// <summary>Dars ↔ guruh.</summary>
    public required IReadOnlyList<LessonGroup> LessonGroups { get; init; }

    /// <summary>Dars ↔ ruxsat etilgan xona.</summary>
    public required IReadOnlyList<LessonClassroom> LessonClassrooms { get; init; }

    /// <summary>Vaqt cheklovlari (3 holatli time-off).</summary>
    public required IReadOnlyList<TimeOff> TimeOffs { get; init; }

    /// <summary>Qulflangan kartochkalar — generator ularni qimirlatmaydi.</summary>
    public required IReadOnlyList<Card> LockedCards { get; init; }
}

/// <summary>Bazaga yoziladigan bitta kartochka (yadro natijasidan hosil qilinadi).</summary>
/// <param name="CoreCardId">Yadrodagi karta indeksi (izlanish/xatolar uchun).</param>
/// <param name="ScheduleId">Jadval varianti.</param>
/// <param name="LessonId">Dars ta'rifi.</param>
/// <param name="PeriodId">Boshlanish dars soati.</param>
/// <param name="DayNo">Kun raqami (0-based).</param>
/// <param name="WeeksMask">Qaysi haftalarda turadi (A/B hafta).</param>
/// <param name="IsLocked">Qulflangan kartochka nusxasi.</param>
/// <param name="ClassroomIds">Tayinlangan xonalar (P1 — odatda bo'sh).</param>
/// <param name="Length">
/// Kartochka egallaydigan ketma-ket soatlar soni. Yadro "2 + 2 + 1" kabi bo'linmaydigan
/// qoldiqni ham qaytaradi, shuning uchun uzunlik <b>har kartochkada alohida</b> yoziladi
/// (<c>Lesson.PeriodsPerCard</c> dan olinmaydi).
/// </param>
public sealed record CardWrite(
    int CoreCardId,
    int ScheduleId,
    int LessonId,
    int PeriodId,
    int DayNo,
    int WeeksMask,
    bool IsLocked,
    IReadOnlyList<int> ClassroomIds,
    int Length = 1)
{
    /// <summary>
    /// Xona nomi MATN sifatida (<c>Card.LegacyRoomNumber</c>). Xonalar ma'lumotnomasi
    /// to'ldirilmagan maktabda foydalanuvchi xonani qo'lda shu maydonga kiritadi va u
    /// kartochkada ko'rinadi. <c>null</c> — ustunga tegilmaydi.
    /// </summary>
    /// <remarks>
    /// Ataylab pozitsion parametr emas, <c>init</c> xossa: generator yozadigan mavjud
    /// <c>new CardWrite(...)</c> chaqiruvlari o'zgarishsiz qoladi.
    /// <para>
    /// <see cref="ClassroomIds"/> dan farqi: bu maydon bandlik tekshiruviga TUSHMAYDI —
    /// u shunchaki ko'rsatiladigan matn. Haqiqiy xona bandligi
    /// <see cref="ClassroomIds"/> orqali hisoblanadi.
    /// </para>
    /// </remarks>
    public string? RoomNumber { get; init; }
}
