namespace DarsJadvali.Application.Validation;

/// <summary>Konflikt darajasi.</summary>
public enum ConflictSeverity
{
    /// <summary>Ogohlantirish — joylashtirishga to'sqinlik qilmaydi.</summary>
    Warning = 0,

    /// <summary>Xato — joylashtirish mumkin emas.</summary>
    Error = 1
}

/// <summary>Konflikt kodlari.</summary>
public static class ConflictCodes
{
    /// <summary>Kun ish kuni emas.</summary>
    public const string DayInactive = "DAY_INACTIVE";

    /// <summary>Dars raqami ruxsat etilgan oraliqdan tashqarida.</summary>
    public const string LessonOutOfRange = "LESSON_OUT_OF_RANGE";

    /// <summary>O'qituvchi band.</summary>
    public const string TeacherBusy = "TEACHER_BUSY";

    /// <summary>Sinf band.</summary>
    public const string ClassBusy = "CLASS_BUSY";

    /// <summary>Xona band.</summary>
    public const string RoomBusy = "ROOM_BUSY";

    /// <summary>O'qituvchi bu vaqtda ishlamaydi.</summary>
    public const string TeacherUnavailable = "TEACHER_UNAVAILABLE";

    /// <summary>Biriktirma yo'q.</summary>
    public const string NoAssignment = "NO_ASSIGNMENT";

    /// <summary>Haftalik soat me'yoridan oshib ketdi.</summary>
    public const string WeeklyHoursExceeded = "WEEKLY_HOURS_EXCEEDED";

    /// <summary>Fan shu kuni takrorlanmoqda.</summary>
    public const string SubjectRepeatedInDay = "SUBJECT_REPEATED_IN_DAY";

    /// <summary>O'qituvchi faol emas.</summary>
    public const string TeacherInactive = "TEACHER_INACTIVE";

    /// <summary>
    /// Bir sinfda TURLI bo'linishlarning guruhlari bir slotda ("1-guruh" + "O'g'illar").
    /// DB unikal indeksi buni ushlay olmaydi — faqat Application darajasida (00 §2.7, §10.3).
    /// </summary>
    public const string GroupDivisionOverlap = "GROUP_DIVISION_OVERLAP";
}

/// <summary>Bitta konflikt.</summary>
/// <param name="Severity">Daraja.</param>
/// <param name="Code">Kod.</param>
/// <param name="Message">O'zbekcha tushuntirish.</param>
public sealed record Conflict(ConflictSeverity Severity, string Code, string Message);
