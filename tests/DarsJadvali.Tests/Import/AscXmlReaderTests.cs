using DarsJadvali.Application.Import;
using DarsJadvali.Infrastructure.Import.Xml;
using Xunit;

namespace DarsJadvali.Tests.Import;

/// <summary>aSc XML o'quvchisi — bazaga tegmasdan, faqat parse.</summary>
public class AscXmlReaderTests
{
    private static AscDocument Read(string fileName)
    {
        using var stream = AscTestData.Open(fileName);
        return AscXmlReader.Read(stream);
    }

    [Fact]
    public void Kichik_maktab_faylining_barcha_boʻlimlari_oʻqiladi()
    {
        var doc = Read("school-small.xml");

        Assert.Equal("asctt2012", doc.FormatName);
        Assert.Equal(6, doc.Periods.Count);
        Assert.Equal(3, doc.DaysDefs.Count);
        Assert.Equal(3, doc.WeeksDefs.Count);
        Assert.Equal(3, doc.TermsDefs.Count);
        Assert.Equal(3, doc.Subjects.Count);
        Assert.Equal(3, doc.Teachers.Count);
        Assert.Equal(2, doc.Classrooms.Count);
        Assert.Single(doc.Grades);
        Assert.Equal(2, doc.Classes.Count);
        Assert.Equal(10, doc.Groups.Count);
        Assert.Equal(6, doc.Lessons.Count);
        Assert.Equal(7, doc.Cards.Count);
    }

    [Fact]
    public void Oʻlchovlar_bit_satrlar_uzunligidan_aniqlanadi()
    {
        var doc = Read("school-small.xml");

        Assert.Equal(5, doc.DetectedDaysPerWeek);
        Assert.Equal(2, doc.DetectedWeeksInCycle);
        Assert.Equal(2, doc.DetectedTermsCount);
    }

    [Fact]
    public void Lesson_va_card_xonalari_ALOHIDA_oʻqiladi()
    {
        var doc = Read("school-small.xml");

        // lessons.classroomids = RUXSAT ETILGAN xonalar (ikkitasi).
        var lesson = doc.Lessons.Single(l => l.Id == "L2");
        Assert.Equal(new[] { "R1", "R2" }, lesson.ClassroomIds);

        // cards.classroomids = TAYINLANGAN xona (bittasi).
        var card = doc.Cards.Single(c => c.LessonId == "L2");
        Assert.Equal(new[] { "R1" }, card.ClassroomIds);
    }

    [Fact]
    public void Minus_bir_va_boʻsh_qiymatlar_havola_emas()
    {
        var doc = Read("messy.xml");

        var withoutTeacher = doc.Classes.Single(c => c.Id == "C1");
        Assert.Null(withoutTeacher.TeacherId);

        var subject = doc.Subjects.Single(s => s.Id == "S2");
        Assert.Equal(string.Empty, subject.Name);
        Assert.Null(subject.Short);

        var classroom = doc.Classrooms.Single();
        Assert.Equal(0, classroom.Capacity);
    }

    [Fact]
    public void Eski_2008_sxemasi_tanib_olinadi()
    {
        var doc = Read("legacy2008.xml");

        Assert.Equal("asctt2008", doc.FormatName);
        Assert.Empty(doc.DaysDefs);
        Assert.Equal(5, doc.DayNames.Count);
        Assert.All(doc.Cards, c => Assert.NotNull(c.Day));
        Assert.Equal(8, doc.Grades.Single().GradeNo);
    }

    [Fact]
    public void Buzuq_XML_tushunarli_xato_beradi()
    {
        using var stream = AscTestData.Open("broken.xml");

        var ex = Assert.Throws<AscImportException>(() => AscXmlReader.Read(stream));
        Assert.Contains("o'qib bo'lmadi", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void aSc_boʻlmagan_XML_rad_etiladi()
    {
        using var stream = AscTestData.Open("not-asc.xml");

        var ex = Assert.Throws<AscImportException>(() => AscXmlReader.Read(stream));
        Assert.Contains("timetable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boʻsh_fayl_rad_etiladi()
    {
        using var stream = AscTestData.Stream("   ");

        Assert.Throws<AscImportException>(() => AscXmlReader.Read(stream));
    }

    [Fact]
    public void Qoʻllab_quvvatlanmaydigan_boʻlimlar_sanaladi()
    {
        var doc = Read("messy.xml");

        Assert.Equal(1, doc.StudentSubjectCount);
        Assert.True(doc.UnsupportedSections.ContainsKey("studentsubject"));
        Assert.Equal(2, doc.Students.Count);
    }
}
