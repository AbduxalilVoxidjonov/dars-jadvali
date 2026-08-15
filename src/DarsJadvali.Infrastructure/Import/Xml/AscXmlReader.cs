using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DarsJadvali.Application.Import;

namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>
/// aSc XML oqimini <see cref="AscDocument"/> ga o'giradi. Hech qanday baza bilmaydi.
/// </summary>
/// <remarks>
/// <para><b>Nega element nomi bo'yicha qidiriladi.</b> aSc eksporti konteyner
/// (<c>&lt;subjects&gt;</c>) + bola (<c>&lt;subject&gt;</c>) sxemasida ishlaydi, lekin
/// vendor konfiguratsiyalarida konteyner nomi va joylashuvi farq qiladi. Shu sababli
/// o'quvchi <b>ildizning barcha avlodlari</b> orasidan kerakli birlik nomini qidiradi —
/// konteyner qanday atalganidan qat'i nazar ishlaydi.</para>
/// <para><b>Xatoga chidamlilik.</b> Yetishmayotgan atribut — <c>null</c>; parse
/// qilinmaydigan son — <c>null</c>; <c>-1</c> va bo'sh satr — "havola yo'q". Hech biri
/// istisno tashlamaydi. Faqat XML'ning o'zi buzuq bo'lsa
/// <see cref="AscImportException"/> chiqadi.</para>
/// </remarks>
public static class AscXmlReader
{
    /// <summary>
    /// Havola yo'qligini bildiradigan aSc sentinellari.
    /// </summary>
    /// <remarks>
    /// <c>"0"</c> ATAYLAB yo'q: aSc'da <c>id="0"</c> ham, <c>grade="0"</c> ham haqiqiy
    /// qiymat bo'lishi mumkin. Yagona sentinel — <c>-1</c> va bo'sh satr.
    /// </remarks>
    private static readonly string[] NullReferences = { "-1", string.Empty };

    /// <summary>Bizga tanish bo'lgan, lekin hali qo'llab-quvvatlanmaydigan bo'limlar.</summary>
    private static readonly string[] UnsupportedElements =
    {
        "studentsubject", "classsubject", "groupsubject", "classtimetable", "dayperiod"
    };

    /// <summary>Oqimni o'qib, hujjat modelini quradi.</summary>
    /// <exception cref="AscImportException">XML buzuq yoki ildiz elementi mos emas.</exception>
    public static AscDocument Read(Stream xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        // aSc ba'zi tillarda encoding="windows-1251" (kirill) bilan eksport qiladi.
        // .NET 8 bu kod sahifasini o'zi bilmaydi — provayder shu yerda ulanadi.
        LegacyEncodings.EnsureRegistered();

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = true,
                IgnoreComments = true
            };

            using var reader = XmlReader.Create(xml, settings);
            doc = XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception ex) when (ex is XmlException or NotSupportedException or ArgumentException)
        {
            throw new AscImportException(
                "aSc XML faylini o'qib bo'lmadi: fayl buzuq yoki kodirovkasi qo'llab-quvvatlanmaydi. " +
                $"Tafsilot: {ex.Message}", ex);
        }

        var root = doc.Root
                   ?? throw new AscImportException("aSc XML fayli bo'sh — ildiz elementi topilmadi.");

        if (!string.Equals(root.Name.LocalName, "timetable", StringComparison.OrdinalIgnoreCase))
        {
            throw new AscImportException(
                $"Bu aSc TimeTables XML eksporti emas: ildiz elementi <{root.Name.LocalName}>, " +
                "kutilgani <timetable>.");
        }

        var options = Attr(root, "options") ?? string.Empty;

        var periods = Rows(root, "period").Select(ReadPeriod).Where(p => p is not null).Select(p => p!).ToList();
        var daysDefs = ReadBitDefs(root, "daysdef", "days");
        var weeksDefs = ReadBitDefs(root, "weeksdef", "weeks");
        var termsDefs = ReadBitDefs(root, "termsdef", "terms");
        var dayNames = Rows(root, "day")
            .Select(e => Attr(e, "name") ?? Attr(e, "short") ?? string.Empty)
            .Where(n => n.Length > 0)
            .ToList();

        var subjects = Rows(root, "subject").Select(ReadSubject).Where(s => s is not null).Select(s => s!).ToList();
        var teachers = Rows(root, "teacher").Select(ReadTeacher).Where(t => t is not null).Select(t => t!).ToList();
        var classrooms = Rows(root, "classroom").Select(ReadClassroom).Where(c => c is not null).Select(c => c!).ToList();
        var grades = Rows(root, "grade").Select(ReadGrade).Where(g => g is not null).Select(g => g!).ToList();
        var classes = Rows(root, "class").Select(ReadClass).Where(c => c is not null).Select(c => c!).ToList();
        var groups = Rows(root, "group").Select(ReadGroup).Where(g => g is not null).Select(g => g!).ToList();
        var lessons = Rows(root, "lesson").Select(ReadLesson).Where(l => l is not null).Select(l => l!).ToList();
        var cards = Rows(root, "card").Select(ReadCard).Where(c => c is not null).Select(c => c!).ToList();
        var students = Rows(root, "student").Select(ReadStudent).Where(s => s is not null).Select(s => s!).ToList();

        var unsupported = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in UnsupportedElements)
        {
            var count = Rows(root, name).Count();
            if (count > 0) unsupported[name] = count;
        }

        // 2012 sxemasi daysdefs/weeksdefs/termsdefs bilan, 2008 esa days + cards.day bilan
        // aniqlanadi. Ikkalasi ham bo'lmasa 2012 deb hisoblanadi (yangi format standart).
        var looksLike2008 = daysDefs.Count == 0
                            && (dayNames.Count > 0 || cards.Any(c => c.Day is not null));

        return new AscDocument
        {
            Options = options,
            DisplayName = Attr(root, "displayname"),
            DayNumberingFromOne = options.Contains("daynumbering1", StringComparison.OrdinalIgnoreCase),
            FormatName = looksLike2008 ? "asctt2008" : "asctt2012",
            Periods = periods,
            DaysDefs = daysDefs,
            WeeksDefs = weeksDefs,
            TermsDefs = termsDefs,
            DayNames = dayNames,
            Subjects = subjects,
            Teachers = teachers,
            Classrooms = classrooms,
            Grades = grades,
            Classes = classes,
            Groups = groups,
            Lessons = lessons,
            Cards = cards,
            Students = students,
            StudentSubjectCount = unsupported.TryGetValue("studentsubject", out var ss) ? ss : 0,
            UnsupportedSections = unsupported
        };
    }

    // -------------------------------------------------------------------------
    // Yozuvlarni o'qish
    // -------------------------------------------------------------------------

    private static AscPeriod? ReadPeriod(XElement e)
    {
        // 2012'da PK "period", ba'zi vendor fayllarida "id" ham uchraydi.
        var number = Int(e, "period") ?? Int(e, "id") ?? Int(e, "short");
        if (number is null) return null;

        return new AscPeriod(
            number.Value,
            Attr(e, "name"),
            Attr(e, "short"),
            Time(e, "starttime"),
            Time(e, "endtime"));
    }

    private static List<AscBitDef> ReadBitDefs(XElement root, string element, string bitsAttribute)
    {
        var result = new List<AscBitDef>();

        foreach (var e in Rows(root, element))
        {
            var id = Attr(e, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            result.Add(new AscBitDef(
                id,
                Attr(e, bitsAttribute) ?? string.Empty,
                Attr(e, "name"),
                Attr(e, "short")));
        }

        return result;
    }

    private static AscSubject? ReadSubject(XElement e)
    {
        var id = Attr(e, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new AscSubject(id, Attr(e, "name") ?? string.Empty, Attr(e, "short"), Attr(e, "partner_id"));
    }

    private static AscTeacher? ReadTeacher(XElement e)
    {
        var id = Attr(e, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new AscTeacher(
            id,
            Attr(e, "name") ?? string.Empty,
            Attr(e, "short"),
            Attr(e, "firstname"),
            Attr(e, "lastname"),
            Attr(e, "gender"),
            Attr(e, "email"),
            Attr(e, "mobile"));
    }

    private static AscClassroom? ReadClassroom(XElement e)
    {
        var id = Attr(e, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new AscClassroom(id, Attr(e, "name") ?? string.Empty, Attr(e, "short"), Int(e, "capacity"));
    }

    private static AscGrade? ReadGrade(XElement e)
    {
        // 2012: PK = "grade" (int). 2008: PK = "id", daraja "grade" ustunida.
        var id = Attr(e, "id");
        var gradeNo = Int(e, "grade") ?? Int(e, "id");
        if (gradeNo is null) return null;

        return new AscGrade(id, gradeNo.Value, Attr(e, "name") ?? string.Empty, Attr(e, "short"));
    }

    private static AscClass? ReadClass(XElement e)
    {
        var id = Attr(e, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new AscClass(
            id,
            Attr(e, "name") ?? string.Empty,
            Attr(e, "short"),
            Reference(e, "grade") ?? Reference(e, "gradeid"),
            Reference(e, "teacherid"),
            References(e, "classroomids"));
    }

    private static AscGroup? ReadGroup(XElement e)
    {
        var id = Attr(e, "id");
        var classId = Reference(e, "classid");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(classId)) return null;

        return new AscGroup(
            id,
            classId,
            Attr(e, "name") ?? string.Empty,
            Bool(e, "entireclass"),
            Int(e, "divisiontag") ?? 0,
            Int(e, "studentcount"));
    }

    private static AscLesson? ReadLesson(XElement e)
    {
        var id = Attr(e, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new AscLesson(
            id,
            Reference(e, "subjectid"),
            References(e, "classids"),
            References(e, "groupids"),
            References(e, "teacherids"),
            References(e, "classroomids"),
            Int(e, "periodspercard") ?? 1,
            Decimal(e, "periodsperweek") ?? 0m,
            Reference(e, "daysdefid"),
            Reference(e, "weeksdefid"),
            Reference(e, "termsdefid"));
    }

    private static AscCard? ReadCard(XElement e)
    {
        var lessonId = Reference(e, "lessonid");
        var period = Int(e, "period");
        if (period is null) return null;

        // sakhr_om.xml kabi denormallashgan eksportlarda ustun nomi birlikda: "classroomid".
        var rooms = References(e, "classroomids");
        if (rooms.Count == 0) rooms = References(e, "classroomid");

        return new AscCard(
            lessonId,
            period.Value,
            Attr(e, "days"),
            Int(e, "day"),
            Attr(e, "weeks"),
            Attr(e, "terms"),
            rooms);
    }

    private static AscStudent? ReadStudent(XElement e)
    {
        var id = Attr(e, "id");
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new AscStudent(id, Reference(e, "classid"), Attr(e, "name") ?? string.Empty);
    }

    // -------------------------------------------------------------------------
    // Past darajali yordamchilar
    // -------------------------------------------------------------------------

    /// <summary>Ildizning barcha avlodlari orasidan berilgan nomli elementlarni tanlaydi.</summary>
    private static IEnumerable<XElement> Rows(XElement root, string localName) =>
        root.Descendants().Where(e =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Atributni katta-kichik harfga sezgir bo'lmagan holda o'qiydi.</summary>
    private static string? Attr(XElement e, string name)
    {
        foreach (var attribute in e.Attributes())
        {
            if (!string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)) continue;

            var value = attribute.Value.Trim();
            return value.Length == 0 ? null : value;
        }

        return null;
    }

    private static int? Int(XElement e, string name)
    {
        var raw = Attr(e, name);
        if (raw is null) return null;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static decimal? Decimal(XElement e, string name)
    {
        var raw = Attr(e, name);
        if (raw is null) return null;

        // aSc "decimalseparatordot" opsiyasisiz vergul ishlatishi mumkin.
        raw = raw.Replace(',', '.');

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static bool Bool(XElement e, string name)
    {
        var raw = Attr(e, name);
        if (raw is null) return false;

        return raw is "1" or "true" or "True" or "TRUE" or "yes";
    }

    private static TimeOnly? Time(XElement e, string name)
    {
        var raw = Attr(e, name);
        if (raw is null) return null;

        if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out var parsed)) return parsed;

        // "8:5" yoki "800" kabi nostandart shakllar.
        var parts = raw.Split(':', '.');
        if (parts.Length >= 2
            && int.TryParse(parts[0], out var hour)
            && int.TryParse(parts[1], out var minute)
            && hour is >= 0 and < 24
            && minute is >= 0 and < 60)
        {
            return new TimeOnly(hour, minute);
        }

        return null;
    }

    /// <summary>Bitta havola: bo'sh yoki <c>-1</c>/<c>0</c> sentineli bo'lsa <c>null</c>.</summary>
    private static string? Reference(XElement e, string name)
    {
        var raw = Attr(e, name);
        if (raw is null) return null;

        // Diqqat: "0" faqat aynan shu satr bo'lganda sentinel — "0" nomli haqiqiy id
        // aSc'da uchramaydi, chunki id'lar prefiksli bo'ladi (masalan "*1", "id_0").
        return NullReferences.Contains(raw, StringComparer.Ordinal) ? null : raw;
    }

    /// <summary>Vergul bilan ajratilgan havolalar ro'yxati.</summary>
    private static IReadOnlyList<string> References(XElement e, string name)
    {
        var raw = Attr(e, name);
        if (raw is null) return Array.Empty<string>();

        var result = new List<string>();

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (NullReferences.Contains(part, StringComparer.Ordinal)) continue;
            if (!result.Contains(part, StringComparer.Ordinal)) result.Add(part);
        }

        return result;
    }
}
