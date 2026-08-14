using System.Text;
using System.Text.RegularExpressions;
using DarsJadvali.Application.Export;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Export;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// PDF eksport moduli testlari: haqiqiy PDF baytlari yaratiladi (mock emas),
/// so'ng natija tekshiriladi.
/// </summary>
public sealed class PdfExportTests
{
    private static ISchoolTimetablePdfExporter CreateExporter(TestDbFactory db) =>
        new SchoolTimetablePdfExporter(db.Get<ITimetableExportModelBuilder>());

    /// <summary>3 sinf, 3 fan, 2 o'qituvchi va bir nechta dars — o'zbek harflari bilan.</summary>
    private static void SeedSchool(TestDbFactory db)
    {
        db.SeedDefaults();

        var mat = db.AddSubject("Matematika", "MAT");
        var ona = db.AddSubject("Oʻzbek tili", "OZB");          // U+02BB
        var ing = db.AddSubject("Ingliz tili", "ING");

        var t1 = db.AddTeacher("Gʻayratov Sanjar");              // U+02BB
        var t2 = db.AddTeacher("Aʼzamov Bekzod");                // U+02BC

        var a = db.AddClassGroup("5-A", "101");
        var b = db.AddClassGroup("5-B", "102");
        var v = db.AddClassGroup("10-A", "201");

        foreach (var group in new[] { a, b, v })
        {
            db.AddAssignment(t1, mat, group, 3);
            db.AddAssignment(t2, ona, group, 2);
            db.AddAssignment(t1, ing, group, 2);
        }

        var day = WeekDay.Dushanba;
        var lesson = 1;
        foreach (var group in new[] { a, b, v })
        {
            db.AddEntry(group, mat, t1, day, lesson);
            db.AddEntry(group, ona, t2, day, lesson + 1);
            day = day == WeekDay.Dushanba ? WeekDay.Seshanba : WeekDay.Chorshanba;
            lesson++;
        }
    }

    private static string Head(byte[] bytes) =>
        Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));

    /// <summary>PDF sarlavhasidagi /Count qiymatidan sahifalar sonini o'qiydi.</summary>
    private static int PageCount(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var match = Regex.Match(raw, @"/Count\s+(\d+)");
        Assert.True(match.Success, "PDF ichida /Count topilmadi.");
        return int.Parse(match.Groups[1].Value);
    }

    // ------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_HaqiqiyPdfYaratadi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);

        var bytes = await CreateExporter(db).ExportAsync(new PdfExportOptions
        {
            SchoolName = "12-sonli umumiy oʻrta taʼlim maktabi"
        });

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, $"PDF juda kichik: {bytes.Length} bayt");
        Assert.Equal("%PDF-", Head(bytes));
        Assert.True(PageCount(bytes) >= 1);
    }

    [Fact]
    public async Task ExportAsync_BoshJadvalda_IstisnoBermaydi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults();   // kunlar bor, lekin sinf ham, dars ham yo'q

        var bytes = await CreateExporter(db).ExportAsync(new PdfExportOptions());

        Assert.Equal("%PDF-", Head(bytes));
        Assert.Equal(1, PageCount(bytes));
    }

    [Fact]
    public async Task ExportAsync_UmumanSozlanmaganBazada_IstisnoBermaydi()
    {
        using var db = new TestDbFactory();   // hech qanday seed yo'q: ish kunlari ham yo'q

        var bytes = await CreateExporter(db).ExportAsync(new PdfExportOptions());

        Assert.Equal("%PDF-", Head(bytes));
    }

    [Fact]
    public async Task ExportAsync_SinflarBorLekinDarsYoq_BoshHujjatQaytaradi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults();
        db.AddClassGroup("5-A", "101");

        var model = await db.Get<ITimetableExportModelBuilder>().BuildAsync(new PdfExportOptions());
        Assert.True(model.IsEmpty);

        var bytes = await CreateExporter(db).ExportAsync(new PdfExportOptions());
        Assert.Equal("%PDF-", Head(bytes));
    }

    [Fact]
    public async Task ClassGroupId_FiltriIshlaydi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);

        var builder = db.Get<ITimetableExportModelBuilder>();

        var all = await builder.BuildAsync(new PdfExportOptions());
        Assert.Equal(3, all.Blocks.Count);

        var target = all.Blocks.Single(b => b.ClassName == "5-B");
        var filtered = await builder.BuildAsync(new PdfExportOptions { ClassGroupId = target.ClassGroupId });

        Assert.Single(filtered.Blocks);
        Assert.Equal("5-B", filtered.Blocks[0].ClassName);
        Assert.True(filtered.EntryCount > 0);
        Assert.True(filtered.EntryCount < all.EntryCount);

        // PDF ham kichikroq bo'lishi kerak (kamroq qator).
        var exporter = CreateExporter(db);
        var allBytes = await exporter.ExportAsync(new PdfExportOptions());
        var oneBytes = await exporter.ExportAsync(new PdfExportOptions { ClassGroupId = target.ClassGroupId });

        Assert.Equal("%PDF-", Head(oneBytes));
        Assert.True(oneBytes.Length < allBytes.Length,
            $"Filtrlangan PDF kichikroq bo'lishi kerak edi: {oneBytes.Length} >= {allBytes.Length}");
    }

    [Fact]
    public async Task ClassGroupId_MavjudBolmagan_BoshHujjat()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);

        var bytes = await CreateExporter(db).ExportAsync(new PdfExportOptions { ClassGroupId = 99999 });

        Assert.Equal("%PDF-", Head(bytes));
        Assert.Equal(1, PageCount(bytes));
    }

    [Fact]
    public async Task KopSahifa_KerakBolganda_YaratiladiVaSahifalarSoniOshadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults();

        var mat = db.AddSubject("Matematika", "MAT");

        for (var i = 0; i < 12; i++)
        {
            // Har sinfga alohida o'qituvchi — aks holda "bir vaqtda ikki joyda" unikal indeksi buziladi.
            var teacher = db.AddTeacher($"Oʻqituvchi {i + 1}");
            var group = db.AddClassGroup($"{5 + i / 3}-{(char)('A' + i % 3)}", (101 + i).ToString());
            db.AddAssignment(teacher, mat, group, 1);
            db.AddEntry(group, mat, teacher, WeekDay.Dushanba, 1 + i % 6);
        }

        var bytes = await CreateExporter(db).ExportAsync(new PdfExportOptions());

        Assert.Equal("%PDF-", Head(bytes));
        Assert.True(PageCount(bytes) > 1, "12 ta sinf bir sahifaga sig'masligi kerak edi.");
    }

    [Fact]
    public void SuggestFileName_KutilganFormatda()
    {
        using var db = new TestDbFactory();
        var exporter = CreateExporter(db);

        var name = exporter.SuggestFileName(new PdfExportOptions(), new DateTime(2026, 8, 13));

        Assert.Equal("Maktab-jadvali-2026-08-13.pdf", name);
        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
        Assert.Matches(@"^Maktab-jadvali-\d{4}-\d{2}-\d{2}\.pdf$", name);
    }

    [Fact]
    public void SuggestFileName_SanaOzgarsaNomHamOzgaradi()
    {
        using var db = new TestDbFactory();
        var exporter = CreateExporter(db);

        Assert.Equal("Maktab-jadvali-2027-01-01.pdf",
            exporter.SuggestFileName(new PdfExportOptions(), new DateTime(2027, 1, 1)));
    }

    // ------------------------------------------------------------------
    // Model quruvchisi
    // ------------------------------------------------------------------

    [Fact]
    public async Task Model_FaqatFaolKunlarniOladi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);   // Yakshanba nofaol

        var model = await db.Get<ITimetableExportModelBuilder>().BuildAsync(new PdfExportOptions());

        Assert.Equal(6, model.Days.Count);
        Assert.DoesNotContain(WeekDay.Yakshanba, model.Days);
        Assert.Equal("Dushanba", model.DayNames[0]);
        Assert.Equal("Shanba", model.DayNames[^1]);
    }

    [Fact]
    public async Task Model_HarSinfUchunMaksimalSoatQadarQator()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);   // MaxLessonsPerDay = 7

        var model = await db.Get<ITimetableExportModelBuilder>().BuildAsync(new PdfExportOptions());

        Assert.All(model.Blocks, b => Assert.Equal(7, b.Rows.Count));
        Assert.All(model.Blocks, b => Assert.All(b.Rows, r => Assert.Equal(6, r.Cells.Count)));
        Assert.Equal("1-soat", model.Blocks[0].Rows[0].LessonLabel);
        Assert.Equal("08:30-09:15", model.Blocks[0].Rows[0].TimeLabel);
    }

    [Fact]
    public async Task Model_SinflarTabiiyTartibdaSaralanadi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);   // 5-A, 5-B, 10-A

        var model = await db.Get<ITimetableExportModelBuilder>().BuildAsync(new PdfExportOptions());

        Assert.Equal(new[] { "5-A", "5-B", "10-A" }, model.Blocks.Select(b => b.ClassName).ToArray());
    }

    [Fact]
    public async Task Model_OqituvchiVaXonaSozlamalariniHurmatQiladi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);

        var builder = db.Get<ITimetableExportModelBuilder>();

        var full = await builder.BuildAsync(new PdfExportOptions());
        var fullCell = full.Blocks.SelectMany(b => b.Rows).SelectMany(r => r.Cells).First(c => c is not null)!;
        Assert.False(string.IsNullOrWhiteSpace(fullCell.SubjectName));
        Assert.False(string.IsNullOrWhiteSpace(fullCell.TeacherName));
        Assert.False(string.IsNullOrWhiteSpace(fullCell.RoomNumber));   // sinf xonasidan olinadi

        var lean = await builder.BuildAsync(new PdfExportOptions
        {
            IncludeTeacherName = false,
            IncludeRoom = false
        });
        var leanCell = lean.Blocks.SelectMany(b => b.Rows).SelectMany(r => r.Cells).First(c => c is not null)!;
        Assert.False(string.IsNullOrWhiteSpace(leanCell.SubjectName));
        Assert.Null(leanCell.TeacherName);
        Assert.Null(leanCell.RoomNumber);
    }

    [Fact]
    public async Task Model_OzbekHarflariSaqlanadi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);

        var model = await db.Get<ITimetableExportModelBuilder>().BuildAsync(new PdfExportOptions());
        var cells = model.Blocks.SelectMany(b => b.Rows).SelectMany(r => r.Cells).Where(c => c is not null).ToList();

        Assert.Contains(cells, c => c!.SubjectName.Contains('ʻ'));   // "Oʻzbek tili"
        Assert.Contains(cells, c => c!.TeacherName is not null && c.TeacherName.Contains('ʼ'));  // "Aʼzamov"
    }

    [Fact]
    public async Task Model_BoshKataklarNullBoladi()
    {
        using var db = new TestDbFactory();
        SeedSchool(db);

        var model = await db.Get<ITimetableExportModelBuilder>().BuildAsync(new PdfExportOptions());
        var allCells = model.Blocks.SelectMany(b => b.Rows).SelectMany(r => r.Cells).ToList();

        Assert.Contains(allCells, c => c is null);
        Assert.Equal(model.EntryCount, allCells.Count(c => c is not null));
    }

    [Fact]
    public async Task Model_BoshJadvalUchunIsEmptyRost()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults();

        var model = await db.Get<ITimetableExportModelBuilder>().BuildAsync(new PdfExportOptions());

        Assert.True(model.IsEmpty);
        Assert.Equal(0, model.EntryCount);
        Assert.Equal("Hali dars qo'yilmagan", TimetableDocumentModel.EmptyMessage);
    }

    // ------------------------------------------------------------------
    // Shrift yechuvchisi
    // ------------------------------------------------------------------

    [Fact]
    public void EmbeddedFontResolver_ShriftBaytlariniQaytaradi()
    {
        var regular = EmbeddedFontResolver.Instance.ResolveTypeface("istalgan", isBold: false, isItalic: false);
        var bold = EmbeddedFontResolver.Instance.ResolveTypeface("istalgan", isBold: true, isItalic: false);

        Assert.NotNull(regular);
        Assert.NotNull(bold);
        Assert.NotEqual(regular!.FaceName, bold!.FaceName);

        var regularBytes = EmbeddedFontResolver.Instance.GetFont(regular.FaceName);
        var boldBytes = EmbeddedFontResolver.Instance.GetFont(bold.FaceName);

        Assert.NotNull(regularBytes);
        Assert.NotNull(boldBytes);
        Assert.True(regularBytes!.Length > 100_000);
        Assert.True(boldBytes!.Length > 100_000);
        Assert.NotEqual(regularBytes.Length, boldBytes.Length);
    }
}
