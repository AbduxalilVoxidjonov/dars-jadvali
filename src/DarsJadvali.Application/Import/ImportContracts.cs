using System.Globalization;
using System.Text;

namespace DarsJadvali.Application.Import;

/// <summary>Import xabarining ogohlantirish darajasi.</summary>
public enum ImportSeverity
{
    /// <summary>Ma'lumot uchun — hech narsa yo'qolmagan.</summary>
    Info = 0,

    /// <summary>Ogohlantirish — biror narsa o'tkazib yuborildi yoki taxminlab to'ldirildi.</summary>
    Warning = 1,

    /// <summary>Xato — import bajarilmadi.</summary>
    Error = 2
}

/// <summary>
/// Import hisobotida sanaladigan entity turlari. Desktop shu bo'yicha jadval chizadi.
/// </summary>
public enum ImportEntityKind
{
    /// <summary>Dars soatlari (aSc <c>periods</c>).</summary>
    Period = 1,

    /// <summary>Choraklar (aSc <c>termsdefs</c>).</summary>
    Term = 2,

    /// <summary>Jadval variantlari — har chorak uchun bittadan.</summary>
    Schedule = 3,

    /// <summary>Parallellar (aSc <c>grades</c>).</summary>
    Grade = 4,

    /// <summary>Fanlar (aSc <c>subjects</c>).</summary>
    Subject = 5,

    /// <summary>O'qituvchilar (aSc <c>teachers</c>).</summary>
    Teacher = 6,

    /// <summary>Xonalar (aSc <c>classrooms</c>).</summary>
    Classroom = 7,

    /// <summary>Sinflar (aSc <c>classes</c>).</summary>
    SchoolClass = 8,

    /// <summary>Sinf bo'linishlari (aSc <c>groups.divisiontag</c>).</summary>
    ClassDivision = 9,

    /// <summary>O'quvchilar guruhlari (aSc <c>groups</c>).</summary>
    StudentGroup = 10,

    /// <summary>Dars ta'riflari (aSc <c>lessons</c>).</summary>
    Lesson = 11,

    /// <summary>Kartochkalar (aSc <c>cards</c>).</summary>
    Card = 12,

    /// <summary>O'quvchilar (aSc <c>students</c>) — hozircha qo'llab-quvvatlanmaydi.</summary>
    Student = 13
}

/// <summary>
/// Import jarayonidagi bitta xabar.
/// </summary>
/// <param name="Severity">Daraja.</param>
/// <param name="Code">
/// Barqaror kod (masalan <c>ASC-UNKNOWN-TEACHER</c>) — UI shu bo'yicha guruhlaydi va
/// tarjima qiladi. Kod hech qachon o'zgarmaydi.
/// </param>
/// <param name="Text">O'zbekcha tushuntirish.</param>
/// <param name="Reference">Manba yozuv identifikatori (aSc <c>id</c>), agar mavjud bo'lsa.</param>
public sealed record ImportMessage(
    ImportSeverity Severity,
    string Code,
    string Text,
    string? Reference = null)
{
    /// <inheritdoc />
    public override string ToString() =>
        Reference is null ? $"[{Code}] {Text}" : $"[{Code}] {Text} ({Reference})";
}

/// <summary>
/// Bitta entity turi bo'yicha import statistikasi.
/// </summary>
/// <param name="Kind">Entity turi.</param>
/// <param name="Title">O'zbekcha nomi (hisobot uchun).</param>
/// <param name="Found">Manba XML'da topilgan yozuvlar soni.</param>
/// <param name="Created">Yangi yaratilgan.</param>
/// <param name="Updated">Mavjudi yangilangan.</param>
/// <param name="Skipped">O'tkazib yuborilgan (xato yoki qo'llab-quvvatlanmaydi).</param>
public sealed record ImportEntityStat(
    ImportEntityKind Kind,
    string Title,
    int Found,
    int Created,
    int Updated,
    int Skipped)
{
    /// <summary>Bu turda umuman biror harakat bo'lganmi.</summary>
    public bool HasAny => Found > 0 || Created > 0 || Updated > 0 || Skipped > 0;
}

/// <summary>
/// Manba XML'ning xom ko'rsatkichlari — hech qanday xaritalashsiz, faqat sanoq.
/// </summary>
/// <param name="FormatName">Aniqlangan format nomi (<c>asctt2012</c> / <c>asctt2008</c>).</param>
/// <param name="DaysPerWeek">Aniqlangan hafta kunlari soni.</param>
/// <param name="WeeksInCycle">Aniqlangan hafta sikli uzunligi.</param>
/// <param name="TermsCount">Aniqlangan choraklar soni.</param>
/// <param name="PeriodCount">Dars soatlari soni.</param>
/// <param name="SubjectCount">Fanlar soni.</param>
/// <param name="TeacherCount">O'qituvchilar soni.</param>
/// <param name="ClassroomCount">Xonalar soni.</param>
/// <param name="GradeCount">Parallellar soni.</param>
/// <param name="ClassCount">Sinflar soni.</param>
/// <param name="GroupCount">Guruhlar soni.</param>
/// <param name="LessonCount">Dars ta'riflari soni.</param>
/// <param name="CardCount">Kartochkalar soni.</param>
/// <param name="StudentCount">O'quvchilar soni.</param>
public sealed record AscSourceSummary(
    string FormatName,
    int DaysPerWeek,
    int WeeksInCycle,
    int TermsCount,
    int PeriodCount,
    int SubjectCount,
    int TeacherCount,
    int ClassroomCount,
    int GradeCount,
    int ClassCount,
    int GroupCount,
    int LessonCount,
    int CardCount,
    int StudentCount);

/// <summary>
/// Import natijasi — nima yaratildi, nima yangilandi, nima o'tkazib yuborildi.
/// </summary>
/// <remarks>
/// <see cref="ImportPreview"/> ham AYNAN shu ma'lumotni beradi: oldindan ko'rish
/// haqiqiy importni tranzaksiya ichida bajarib, so'ng uni qaytarish (rollback) orqali
/// olinadi. Shu sababli "oldindan ko'rish nima deb aytgan bo'lsa, import ham shuni
/// qiladi" kafolati bor.
/// </remarks>
public sealed record ImportResult
{
    /// <summary>Import muvaffaqiyatli tugadimi (xato darajasidagi xabar yo'qmi).</summary>
    public bool Success { get; init; }

    /// <summary>Bu faqat oldindan ko'rish (bazaga hech narsa yozilmagan) edimi.</summary>
    public bool DryRun { get; init; }

    /// <summary>Maqsad o'quv yili Id.</summary>
    public int AcademicYearId { get; init; }

    /// <summary>Entity turlari bo'yicha statistika.</summary>
    public IReadOnlyList<ImportEntityStat> Stats { get; init; } = Array.Empty<ImportEntityStat>();

    /// <summary>Barcha xabarlar (info + ogohlantirish + xato).</summary>
    public IReadOnlyList<ImportMessage> Messages { get; init; } = Array.Empty<ImportMessage>();

    /// <summary>Yaratilgan/ishlatilgan jadval variantlari Id lari (har chorak uchun bittadan).</summary>
    public IReadOnlyList<int> ScheduleIds { get; init; } = Array.Empty<int>();

    /// <summary>Yaratilgan/ishlatilgan jadval variantlari nomlari — <see cref="ScheduleIds"/> bilan bir tartibda.</summary>
    public IReadOnlyList<string> ScheduleNames { get; init; } = Array.Empty<string>();

    /// <summary>Manba XML ko'rsatkichlari.</summary>
    public AscSourceSummary? Source { get; init; }

    /// <summary>Jami yaratilgan yozuvlar.</summary>
    public int TotalCreated => Stats.Sum(s => s.Created);

    /// <summary>Jami yangilangan yozuvlar.</summary>
    public int TotalUpdated => Stats.Sum(s => s.Updated);

    /// <summary>Jami o'tkazib yuborilgan yozuvlar.</summary>
    public int TotalSkipped => Stats.Sum(s => s.Skipped);

    /// <summary>Faqat ogohlantirishlar.</summary>
    public IReadOnlyList<ImportMessage> Warnings =>
        Messages.Where(m => m.Severity == ImportSeverity.Warning).ToList();

    /// <summary>Faqat xatolar.</summary>
    public IReadOnlyList<ImportMessage> Errors =>
        Messages.Where(m => m.Severity == ImportSeverity.Error).ToList();

    /// <summary>Hisobotni o'zbekcha matn ko'rinishida qaytaradi (jurnal va UI uchun).</summary>
    public string ToReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine(DryRun
            ? "aSc XML importi — OLDINDAN KO'RISH (bazaga hech narsa yozilmadi)"
            : "aSc XML importi — NATIJA");
        sb.AppendLine(new string('-', 62));

        if (Source is { } src)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"Format: {src.FormatName}; kunlar: {src.DaysPerWeek}, haftalar: {src.WeeksInCycle}, choraklar: {src.TermsCount}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"Bo'lim",-22}{"topildi",8}{"yaratildi",11}{"yangilandi",12}{"o'tkazildi",12}");

        foreach (var stat in Stats.Where(s => s.HasAny))
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{stat.Title,-22}{stat.Found,8}{stat.Created,11}{stat.Updated,12}{stat.Skipped,12}");
        }

        sb.AppendLine(new string('-', 62));
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"JAMI: yaratildi {TotalCreated}, yangilandi {TotalUpdated}, o'tkazib yuborildi {TotalSkipped}");

        if (ScheduleNames.Count > 0)
        {
            sb.AppendLine("Jadval variantlari: " + string.Join(", ", ScheduleNames));
        }

        var errors = Errors;
        if (errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"XATOLAR ({errors.Count}):");
            foreach (var message in errors) sb.AppendLine("  • " + message);
        }

        var warnings = Warnings;
        if (warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"OGOHLANTIRISHLAR ({warnings.Count}):");
            foreach (var message in warnings) sb.AppendLine("  • " + message);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Oldindan ko'rish natijasi — <see cref="ImportResult"/> ning o'zi, lekin
/// <see cref="ImportResult.DryRun"/> har doim <c>true</c>.
/// </summary>
/// <param name="Result">To'liq natija (bazaga yozilmagan).</param>
public sealed record ImportPreview(ImportResult Result)
{
    /// <summary>Import qilish mumkinmi (xato darajasidagi xabar yo'qmi).</summary>
    public bool IsValid => Result.Success;

    /// <summary>Entity turlari bo'yicha statistika.</summary>
    public IReadOnlyList<ImportEntityStat> Stats => Result.Stats;

    /// <summary>Barcha xabarlar.</summary>
    public IReadOnlyList<ImportMessage> Messages => Result.Messages;

    /// <summary>Yaratiladigan jadval variantlari nomlari.</summary>
    public IReadOnlyList<string> ScheduleNames => Result.ScheduleNames;

    /// <summary>Manba XML ko'rsatkichlari.</summary>
    public AscSourceSummary? Source => Result.Source;

    /// <summary>Hisobot matni.</summary>
    public string ToReport() => Result.ToReport();
}
