namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Dars ↔ o'qituvchi bog'lanishi (kompozit PK: <c>LessonId</c> + <c>TeacherId</c>).
/// Bir darsda bir nechta o'qituvchi bo'lishi mumkin (birgalikda o'qitish).
/// </summary>
/// <remarks>
/// <see cref="Common.BaseEntity"/> dan meros olmaydi — kompozit kalitli join jadvali,
/// unga <c>Id</c>/<c>Uid</c> ustunlari ortiqcha.
/// </remarks>
public class LessonTeacher
{
    /// <summary>Dars Id.</summary>
    public int LessonId { get; set; }

    /// <summary>Dars.</summary>
    public Lesson? Lesson { get; set; }

    /// <summary>O'qituvchi Id.</summary>
    public int TeacherId { get; set; }

    /// <summary>O'qituvchi.</summary>
    public Teacher? Teacher { get; set; }
}

/// <summary>
/// Dars ↔ sinf bog'lanishi (kompozit PK). Bir nechta sinf = birlashtirilgan dars.
/// </summary>
public class LessonClass
{
    /// <summary>Dars Id.</summary>
    public int LessonId { get; set; }

    /// <summary>Dars.</summary>
    public Lesson? Lesson { get; set; }

    /// <summary>Sinf Id.</summary>
    public int SchoolClassId { get; set; }

    /// <summary>Sinf.</summary>
    public SchoolClass? SchoolClass { get; set; }
}

/// <summary>
/// Dars ↔ guruh bog'lanishi (kompozit PK). <b>Bandlik aynan shu bog'lanish orqali
/// hisoblanadi</b> — <c>CardOccurrence</c> guruh aniqligida yoziladi.
/// </summary>
public class LessonGroup
{
    /// <summary>Dars Id.</summary>
    public int LessonId { get; set; }

    /// <summary>Dars.</summary>
    public Lesson? Lesson { get; set; }

    /// <summary>Guruh Id.</summary>
    public int StudentGroupId { get; set; }

    /// <summary>Guruh.</summary>
    public StudentGroup? StudentGroup { get; set; }
}

/// <summary>
/// Dars ↔ <b>ruxsat etilgan</b> xonalar to'plami (kompozit PK). P1 — bo'sh bo'lishi mumkin.
/// Tayinlangan xona esa <see cref="CardClassroom"/> da.
/// </summary>
public class LessonClassroom
{
    /// <summary>Dars Id.</summary>
    public int LessonId { get; set; }

    /// <summary>Dars.</summary>
    public Lesson? Lesson { get; set; }

    /// <summary>Xona Id.</summary>
    public int ClassroomId { get; set; }

    /// <summary>Xona.</summary>
    public Classroom? Classroom { get; set; }

    /// <summary>Tanlash ustuvorligi (kichik = afzalroq).</summary>
    public int Priority { get; set; }
}

/// <summary>
/// Kartochka ↔ <b>tayinlangan</b> xona (kompozit PK). P1 — bo'sh bo'lishi mumkin.
/// </summary>
public class CardClassroom
{
    /// <summary>Kartochka Id.</summary>
    public int CardId { get; set; }

    /// <summary>Kartochka.</summary>
    public Card? Card { get; set; }

    /// <summary>Xona Id.</summary>
    public int ClassroomId { get; set; }

    /// <summary>Xona.</summary>
    public Classroom? Classroom { get; set; }
}
