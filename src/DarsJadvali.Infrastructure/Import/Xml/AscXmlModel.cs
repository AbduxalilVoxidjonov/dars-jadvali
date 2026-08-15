namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>Dars soati (aSc <c>periods</c>).</summary>
/// <param name="Number">Pozitsiya raqami; <c>0</c> — "nolinchi soat".</param>
/// <param name="Name">To'liq nomi.</param>
/// <param name="Short">Qisqartma.</param>
/// <param name="StartTime">Boshlanish vaqti.</param>
/// <param name="EndTime">Tugash vaqti.</param>
public sealed record AscPeriod(
    int Number, string? Name, string? Short, TimeOnly? StartTime, TimeOnly? EndTime);

/// <summary>
/// Bitmask ta'rifi — <c>daysdefs</c> / <c>weeksdefs</c> / <c>termsdefs</c> uchun umumiy shakl.
/// </summary>
/// <param name="Id">aSc identifikatori.</param>
/// <param name="Bits">Bit-satr, masalan <c>"10000"</c>.</param>
/// <param name="Name">Nomi.</param>
/// <param name="Short">Qisqartma.</param>
public sealed record AscBitDef(string Id, string Bits, string? Name, string? Short)
{
    /// <summary><see cref="Bits"/> ning <c>int</c> ko'rinishi.</summary>
    public int Mask => AscBitmask.ToMask(Bits);

    /// <summary>Ta'riflangan pozitsiyalar soni.</summary>
    public int Width => AscBitmask.Length(Bits);
}

/// <summary>Fan (aSc <c>subjects</c>).</summary>
public sealed record AscSubject(string Id, string Name, string? Short, string? PartnerId);

/// <summary>O'qituvchi (aSc <c>teachers</c>).</summary>
public sealed record AscTeacher(
    string Id,
    string Name,
    string? Short,
    string? FirstName,
    string? LastName,
    string? Gender,
    string? Email,
    string? Mobile);

/// <summary>Xona (aSc <c>classrooms</c>).</summary>
public sealed record AscClassroom(string Id, string Name, string? Short, int? Capacity);

/// <summary>Parallel (aSc <c>grades</c>).</summary>
/// <param name="Id">2008 sxemasidagi surrogat id (2012'da yo'q).</param>
/// <param name="GradeNo">Daraja raqami — 2012'da PK.</param>
/// <param name="Name">Nomi.</param>
/// <param name="Short">Qisqartma.</param>
public sealed record AscGrade(string? Id, int GradeNo, string Name, string? Short);

/// <summary>Sinf (aSc <c>classes</c>).</summary>
/// <param name="Id">aSc identifikatori.</param>
/// <param name="Name">Nomi.</param>
/// <param name="Short">Qisqartma.</param>
/// <param name="GradeKey">
/// <c>grade</c> atributi xom holda: 2012'da bu <c>grades.grade</c> soni,
/// 2008'da esa <c>grades.id</c> bo'lishi mumkin — ikkalasi ham qidiriladi.
/// </param>
/// <param name="TeacherId">Sinf rahbari.</param>
/// <param name="ClassroomIds">Uy xonalari ro'yxati (birinchisi asosiy deb olinadi).</param>
public sealed record AscClass(
    string Id,
    string Name,
    string? Short,
    string? GradeKey,
    string? TeacherId,
    IReadOnlyList<string> ClassroomIds);

/// <summary>Guruh (aSc <c>groups</c>).</summary>
/// <param name="Id">aSc identifikatori.</param>
/// <param name="ClassId">Qaysi sinfga tegishli.</param>
/// <param name="Name">Nomi.</param>
/// <param name="EntireClass">Bu guruh butun sinfni ifodalaydimi.</param>
/// <param name="DivisionTag">
/// <b>Eng muhim maydon.</b> Bir xil tegli guruhlar bitta bo'linishga tegishli va
/// bir vaqtda dars o'tishi mumkin.
/// </param>
/// <param name="StudentCount">O'quvchilar soni.</param>
public sealed record AscGroup(
    string Id,
    string ClassId,
    string Name,
    bool EntireClass,
    int DivisionTag,
    int? StudentCount);

/// <summary>Dars ta'rifi (aSc <c>lessons</c>).</summary>
/// <param name="Id">aSc identifikatori.</param>
/// <param name="SubjectId">Fan.</param>
/// <param name="ClassIds">Sinflar (bir nechta = birlashtirilgan dars).</param>
/// <param name="GroupIds">Guruhlar. Bo'sh bo'lsa — butun sinf(lar)ga.</param>
/// <param name="TeacherIds">O'qituvchilar (bir nechta = birgalikda o'qitish).</param>
/// <param name="ClassroomIds">
/// <b>RUXSAT ETILGAN</b> xonalar to'plami (cheklov) — <see cref="AscCard.ClassroomIds"/>
/// bilan adashtirmaslik kerak, u yerda TAYINLANGAN xona.
/// </param>
/// <param name="PeriodsPerCard">Bitta kartochka necha soat (1 = yakka, 2 = juft).</param>
/// <param name="PeriodsPerWeek">Haftalik jami soat (kasr bo'lishi mumkin).</param>
/// <param name="DaysDefId">Ruxsat etilgan kunlar ta'rifi.</param>
/// <param name="WeeksDefId">Ruxsat etilgan haftalar ta'rifi.</param>
/// <param name="TermsDefId">Amal qiladigan choraklar ta'rifi.</param>
public sealed record AscLesson(
    string Id,
    string? SubjectId,
    IReadOnlyList<string> ClassIds,
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> TeacherIds,
    IReadOnlyList<string> ClassroomIds,
    int PeriodsPerCard,
    decimal PeriodsPerWeek,
    string? DaysDefId,
    string? WeeksDefId,
    string? TermsDefId);

/// <summary>Joylashtirilgan kartochka (aSc <c>cards</c>).</summary>
/// <param name="LessonId">Qaysi darsga tegishli.</param>
/// <param name="Period">Boshlanish dars soati raqami.</param>
/// <param name="Days">Kunlar bit-satri (2012). Odatda aynan bitta <c>1</c>.</param>
/// <param name="Day">Kun raqami (faqat 2008).</param>
/// <param name="Weeks">Haftalar bit-satri.</param>
/// <param name="Terms">Choraklar bit-satri.</param>
/// <param name="ClassroomIds"><b>TAYINLANGAN</b> xona(lar).</param>
public sealed record AscCard(
    string? LessonId,
    int Period,
    string? Days,
    int? Day,
    string? Weeks,
    string? Terms,
    IReadOnlyList<string> ClassroomIds);

/// <summary>O'quvchi (aSc <c>students</c>) — hozircha faqat sanaladi (P2).</summary>
public sealed record AscStudent(string Id, string? ClassId, string Name);

/// <summary>
/// Butun aSc XML hujjati — xom, xaritalanmagan holda.
/// </summary>
public sealed class AscDocument
{
    /// <summary><c>&lt;timetable options="..."&gt;</c> atributi.</summary>
    public string Options { get; init; } = string.Empty;

    /// <summary><c>&lt;timetable displayname="..."&gt;</c> atributi.</summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// <c>daynumbering1</c> opsiyasi — 2008 sxemasidagi <c>cards.day</c> 1'dan
    /// raqamlanganini bildiradi.
    /// </summary>
    public bool DayNumberingFromOne { get; init; }

    /// <summary>Aniqlangan sxema nomi: <c>asctt2012</c> yoki <c>asctt2008</c>.</summary>
    public string FormatName { get; init; } = "asctt2012";

    /// <summary>Dars soatlari.</summary>
    public IReadOnlyList<AscPeriod> Periods { get; init; } = Array.Empty<AscPeriod>();

    /// <summary>Kun to'plamlari ta'riflari (2012).</summary>
    public IReadOnlyList<AscBitDef> DaysDefs { get; init; } = Array.Empty<AscBitDef>();

    /// <summary>Hafta to'plamlari ta'riflari (2012).</summary>
    public IReadOnlyList<AscBitDef> WeeksDefs { get; init; } = Array.Empty<AscBitDef>();

    /// <summary>Chorak to'plamlari ta'riflari (2012).</summary>
    public IReadOnlyList<AscBitDef> TermsDefs { get; init; } = Array.Empty<AscBitDef>();

    /// <summary>2008 sxemasidagi kunlar ro'yxati (nomlar uchun).</summary>
    public IReadOnlyList<string> DayNames { get; init; } = Array.Empty<string>();

    /// <summary>Fanlar.</summary>
    public IReadOnlyList<AscSubject> Subjects { get; init; } = Array.Empty<AscSubject>();

    /// <summary>O'qituvchilar.</summary>
    public IReadOnlyList<AscTeacher> Teachers { get; init; } = Array.Empty<AscTeacher>();

    /// <summary>Xonalar.</summary>
    public IReadOnlyList<AscClassroom> Classrooms { get; init; } = Array.Empty<AscClassroom>();

    /// <summary>Parallellar.</summary>
    public IReadOnlyList<AscGrade> Grades { get; init; } = Array.Empty<AscGrade>();

    /// <summary>Sinflar.</summary>
    public IReadOnlyList<AscClass> Classes { get; init; } = Array.Empty<AscClass>();

    /// <summary>Guruhlar.</summary>
    public IReadOnlyList<AscGroup> Groups { get; init; } = Array.Empty<AscGroup>();

    /// <summary>Dars ta'riflari.</summary>
    public IReadOnlyList<AscLesson> Lessons { get; init; } = Array.Empty<AscLesson>();

    /// <summary>Kartochkalar.</summary>
    public IReadOnlyList<AscCard> Cards { get; init; } = Array.Empty<AscCard>();

    /// <summary>O'quvchilar.</summary>
    public IReadOnlyList<AscStudent> Students { get; init; } = Array.Empty<AscStudent>();

    /// <summary>
    /// <c>studentsubjects</c> yozuvlari soni — qo'llab-quvvatlanmaganini bildirish uchun.
    /// </summary>
    public int StudentSubjectCount { get; init; }

    /// <summary>
    /// Qo'llab-quvvatlanmaydigan, lekin XML'da uchragan konteynerlar
    /// (masalan <c>classsubjects</c>, <c>classtimetables</c>) va ulardagi yozuvlar soni.
    /// </summary>
    public IReadOnlyDictionary<string, int> UnsupportedSections { get; init; } =
        new Dictionary<string, int>();

    // -------------------------------------------------------------------------

    /// <summary>
    /// Hafta kunlari soni — <c>daysdefs</c> va kartochkalardagi eng uzun bit-satrdan.
    /// Aniqlanmasa <c>0</c>.
    /// </summary>
    public int DetectedDaysPerWeek
    {
        get
        {
            var width = DaysDefs.Count == 0 ? 0 : DaysDefs.Max(d => d.Width);
            foreach (var card in Cards)
            {
                width = Math.Max(width, AscBitmask.Length(card.Days));
            }

            return Math.Max(width, DayNames.Count);
        }
    }

    /// <summary>Hafta sikli uzunligi — <c>weeksdefs</c> dagi eng uzun bit-satrdan.</summary>
    public int DetectedWeeksInCycle
    {
        get
        {
            var width = WeeksDefs.Count == 0 ? 0 : WeeksDefs.Max(d => d.Width);
            foreach (var card in Cards)
            {
                width = Math.Max(width, AscBitmask.Length(card.Weeks));
            }

            return width;
        }
    }

    /// <summary>Choraklar soni — <c>termsdefs</c> dagi eng uzun bit-satrdan.</summary>
    public int DetectedTermsCount
    {
        get
        {
            var width = TermsDefs.Count == 0 ? 0 : TermsDefs.Max(d => d.Width);
            foreach (var card in Cards)
            {
                width = Math.Max(width, AscBitmask.Length(card.Terms));
            }

            return width;
        }
    }
}
