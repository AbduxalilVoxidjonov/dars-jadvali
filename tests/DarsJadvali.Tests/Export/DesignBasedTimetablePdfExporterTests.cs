using System.Text;
using System.Text.RegularExpressions;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Export.Printing;
using Xunit;

namespace DarsJadvali.Tests.Export;

/// <summary>
/// Uchidan-uchiga: haqiqiy ma'lumotlar bazasi → adapter → dizayn → PDF/HTML.
/// </summary>
public sealed class DesignBasedTimetablePdfExporterTests
{
    private static DesignBasedTimetablePdfExporter Create(TestDbFactory db, DesignExportOptions? options = null) =>
        new(
            db.Get<IScheduleService>(),
            db.Get<IWorkDayService>(),
            db.Get<ITeacherService>(),
            db.Get<IClassGroupService>(),
            options);

    private static string Head(byte[] bytes) =>
        Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));

    private static int PageCount(byte[] bytes)
    {
        var match = Regex.Match(Encoding.Latin1.GetString(bytes), @"/Count\s+(\d+)");
        Assert.True(match.Success, "PDF ichida /Count topilmadi.");
        return int.Parse(match.Groups[1].Value);
    }

    /// <summary>O'zbek lotin va kirill nomlari bilan kichik maktab.</summary>
    private static (int ClassId, int TeacherId) SeedSchool(TestDbFactory db, int classCount = 3)
    {
        db.SeedDefaults(maxLessons: 7);

        var mat = db.AddSubject("Matematika", "MAT");
        var ona = db.AddSubject("Oʻzbek tili", "OZB");
        var rus = db.AddSubject("Русский язык", "RUS");

        // Har sinfga o'z o'qituvchisi: bitta o'qituvchi bir vaqtda ikki sinfda
        // tura olmaydi (baza darajasidagi UNIQUE cheklov).
        var classes = new List<Domain.Entities.ClassGroup>(classCount);
        var teachers = new List<Domain.Entities.Teacher>(classCount);

        for (var i = 0; i < classCount; i++)
        {
            var teacher = db.AddTeacher(i == 0 ? "Gʻayratov Sanjar" : $"Аʼзамов Бекзод {i}");
            var group = db.AddClassGroup($"{5 + i}-A", $"{101 + i}");

            db.AddAssignment(teacher, mat, group, 3);
            db.AddAssignment(teacher, ona, group, 2);

            db.AddEntry(group, mat, teacher, WeekDay.Dushanba, 1);
            db.AddEntry(group, ona, teacher, WeekDay.Dushanba, 2);
            db.AddEntry(group, rus, teacher, WeekDay.Seshanba, 3, room: "204");

            classes.Add(group);
            teachers.Add(teacher);
        }

        return (classes[0].Id, teachers[0].Id);
    }

    // ------------------------------------------------------------------

    [Fact]
    public async Task Sinf_jadvali_haqiqiy_pdf_beradi()
    {
        using var db = new TestDbFactory();
        var (classId, _) = SeedSchool(db);

        var document = await Create(db).ExportClassScheduleAsync(
            classId, new PdfExportOptions { SchoolName = "12-sonli maktab" });

        Assert.Equal("%PDF-", Head(document.Content));
        Assert.True(document.Content.Length > 2000);
        Assert.Equal(1, PageCount(document.Content));
        Assert.EndsWith(".pdf", document.FileName, StringComparison.Ordinal);
        Assert.Contains("5-A", document.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Oqituvchi_jadvali_haqiqiy_pdf_beradi()
    {
        using var db = new TestDbFactory();
        var (_, teacherId) = SeedSchool(db);

        var document = await Create(db).ExportTeacherScheduleAsync(teacherId);

        Assert.Equal("%PDF-", Head(document.Content));
        Assert.True(document.Content.Length > 2000);
        Assert.EndsWith(".pdf", document.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Maktab_jadvali_kop_sahifali_boladi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db, classCount: 9);

        var document = await Create(db).ExportSchoolScheduleAsync(
            new PdfExportOptions { SchoolName = "12-sonli maktab" });

        Assert.Equal("%PDF-", Head(document.Content));

        // Maktab dizaynida sahifasiga 4 ta sinf → 9 sinf = 3 sahifa.
        Assert.Equal(3, PageCount(document.Content));
    }

    [Fact]
    public async Task Notogri_qamrov_aniq_xato_beradi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);

        var exporter = Create(db);

        await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportClassScheduleAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportTeacherScheduleAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportClassScheduleAsync(999999));
    }

    [Fact]
    public async Task Bosh_bazada_ham_pdf_chiqadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults();

        var document = await Create(db).ExportSchoolScheduleAsync();

        Assert.Equal("%PDF-", Head(document.Content));
    }

    [Fact]
    public async Task Html_eksport_offline_va_ozbek_matnli()
    {
        using var db = new TestDbFactory();
        var (classId, _) = SeedSchool(db);

        var document = await Create(db).ExportClassScheduleHtmlAsync(
            classId, new PdfExportOptions { SchoolName = "12-sonli maktab" });

        var html = Encoding.UTF8.GetString(document.Content);

        Assert.EndsWith(".html", document.FileName, StringComparison.Ordinal);
        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Oʻzbek tili", html, StringComparison.Ordinal);
        Assert.Contains("Русский язык", html, StringComparison.Ordinal);
        Assert.Contains("Gʻayratov Sanjar", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Maktab_html_eksporti_barcha_sinflarni_qamraydi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db, classCount: 5);

        var document = await Create(db).ExportSchoolScheduleHtmlAsync();
        var html = Encoding.UTF8.GetString(document.Content);

        Assert.Equal(5, Regex.Matches(html, "<section class=\"grid\"").Count);
        Assert.Contains("<nav class=\"sections\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ikki_smena_uzluksiz_raqamlanadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 12);

        var subject = db.AddSubject("Matematika", "MAT");
        var teacher = db.AddTeacher("Qodirov Aziz");
        var group = db.AddClassGroup("5-A", "101");
        db.AddAssignment(teacher, subject, group, 2);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 8);

        var exporter = Create(db, new DesignExportOptions { FirstShiftPeriodCount = 6 });
        var document = await exporter.ExportClassScheduleHtmlAsync(group.Id);
        var html = Encoding.UTF8.GetString(document.Content);

        Assert.Contains("1-smena", html, StringComparison.Ordinal);
        Assert.Contains("2-smena", html, StringComparison.Ordinal);

        // 8-soat aynan "8" bo'lib qoladi (2-smenaning 2-darsi emas).
        Assert.Contains(">8<", html, StringComparison.Ordinal);
        Assert.Contains(">12<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sozlamalar_katak_mazmunini_boshqaradi()
    {
        using var db = new TestDbFactory();
        var (classId, _) = SeedSchool(db);

        var withRoom = await Create(db).ExportClassScheduleHtmlAsync(
            classId, new PdfExportOptions { IncludeRoom = true, IncludeTeacherName = true });
        var withoutRoom = await Create(db).ExportClassScheduleHtmlAsync(
            classId, new PdfExportOptions { IncludeRoom = false, IncludeTeacherName = false });

        var a = Encoding.UTF8.GetString(withRoom.Content);
        var b = Encoding.UTF8.GetString(withoutRoom.Content);

        Assert.Contains("Gʻayratov Sanjar", a, StringComparison.Ordinal);
        Assert.DoesNotContain("<div class=\"line\">Gʻayratov Sanjar</div>", b, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adapter_eski_modelda_standart_qiymat_beradi()
    {
        using var db = new TestDbFactory();
        var (classId, _) = SeedSchool(db);

        var classGroup = await db.Get<IClassGroupService>().GetByIdAsync(classId);
        var entries = await db.Get<IScheduleService>().GetByClassGroupAsync(classId);

        var timetable = ScheduleEntryPrintableAdapter.BuildClass(
            classGroup!,
            entries,
            new[] { WeekDay.Dushanba, WeekDay.Seshanba, WeekDay.Chorshanba },
            ScheduleEntryPrintableAdapter.ToPeriods(null, 7),
            new PrintableContext("Maktab", "2025/2026", "1-chorak"));

        Assert.NotEmpty(timetable.AllCards);

        // ScheduleEntry da bu maydonlar yo'q — Card ga o'tguncha standart qiymat.
        Assert.All(timetable.AllCards, c =>
        {
            Assert.Equal(1, c.Length);
            Assert.True(c.IsEveryWeek);
            Assert.Null(c.GroupName);
        });

        // Adapter fan/o'qituvchi/xona nomlarini haqiqatan olib kelgan.
        Assert.Contains(timetable.AllCards, c => c.SubjectName == "Oʻzbek tili");
        Assert.Contains(timetable.AllCards, c => c.TeacherNames.Contains("Gʻayratov Sanjar"));
        Assert.Contains(timetable.AllCards, c => c.RoomName == "204");
    }

    [Fact]
    public void Adapter_ikki_smena_nomlarini_beradi()
    {
        var periods = ScheduleEntryPrintableAdapter.ToPeriods(null, maxLessonNumber: 12, firstShiftPeriodCount: 6);

        Assert.Equal(12, periods.Count);
        Assert.Equal(Enumerable.Range(1, 12), periods.Select(p => p.Number));
        Assert.Equal("1-smena", periods[0].ShiftName);
        Assert.Equal("1-smena", periods[5].ShiftName);
        Assert.Equal("2-smena", periods[6].ShiftName);
        Assert.Equal("12", periods[11].Label);
    }

    [Fact]
    public void Adapter_bitta_smenada_polosa_qoymaydi()
    {
        var periods = ScheduleEntryPrintableAdapter.ToPeriods(null, maxLessonNumber: 6, firstShiftPeriodCount: 6);

        Assert.All(periods, p => Assert.Null(p.ShiftName));
    }
}
