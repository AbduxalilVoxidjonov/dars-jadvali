using DarsJadvali.Application.Import;
using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Import;

/// <summary>
/// Xatoga chidamlilik: iflos eksport importni YIQITMASLIGI kerak.
/// </summary>
public class AscImportRobustnessTests
{
    [Fact]
    public async Task Iflos_eksport_yiqitmaydi_va_ogohlantirishga_yozadi()
    {
        using var world = new AscWorld();

        var result = await world.ImportFileAsync("messy.xml");

        Assert.True(result.Success, result.ToReport());
        Assert.NotEmpty(result.Warnings);

        var codes = result.Messages.Select(m => m.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ASC-UNKNOWN-SUBJECT", codes);     // lesson L1 → fan topilmadi
        Assert.Contains("ASC-UNKNOWN-TEACHER", codes);     // lesson L2 → o'qituvchi topilmadi
        Assert.Contains("ASC-UNKNOWN-CLASS", codes);       // lesson L4 / guruh GX
        Assert.Contains("ASC-UNKNOWN-GROUP", codes);       // lesson L4 → guruh GX
        Assert.Contains("ASC-UNKNOWN-GRADE", codes);       // class C2 → parallel 42
        Assert.Contains("ASC-UNKNOWN-CLASSROOM", codes);   // card → xona RX
        Assert.Contains("ASC-UNKNOWN-LESSON", codes);      // card → dars LZ
        Assert.Contains("ASC-UNKNOWN-PERIOD", codes);      // card → 99-soat
        Assert.Contains("ASC-CARD-NO-DAY", codes);         // card → days=""
        Assert.Contains("ASC-FRACTIONAL-PERIODS", codes);  // periodsperweek=1.5
        Assert.Contains("ASC-LESSON-NO-PERIODS", codes);   // periodsperweek=0
        Assert.Contains("ASC-INVALID-VALUE", codes);       // period=-3, grade=99
        Assert.Contains("ASC-STUDENTS-SKIPPED", codes);
        Assert.Contains("ASC-UNSUPPORTED", codes);
        Assert.Contains("ASC-ENTIRECLASS-ADDED", codes);
    }

    [Fact]
    public async Task Iflos_eksportdan_ham_haqiqiy_maʼlumot_chiqadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("messy.xml");
        world.Detach();

        // period="-3" tashlandi, qolgan ikkitasi qoldi.
        Assert.Equal(2, world.Context.Periods.Count());

        // Vaqti ko'rsatilmagan soat uchun standart vaqt hisoblandi.
        var second = world.Context.Periods.Single(p => p.PeriodNo == 2);
        Assert.True(second.EndTime > second.StartTime);

        // grade="99" CHECK chegarasidan tashqarida → yozilmadi.
        Assert.Equal(1, world.Context.Grades.Count());

        // capacity="0" → NULL.
        Assert.Null(world.Context.Classrooms.Single().Capacity);

        // "Вакант" o'qituvchi sifatida belgilandi.
        Assert.True(world.Context.Teachers.Single(t => t.ExternalId == "T2").IsVacancy);

        // gender="X" → aniqlanmadi.
        Assert.Null(world.Context.Teachers.Single(t => t.ExternalId == "T1").Gender);

        // Har sinfda "butun sinf" guruhi bor.
        foreach (var schoolClass in world.Context.SchoolClasses.Include(c => c.StudentGroups).ToList())
        {
            Assert.Single(schoolClass.StudentGroups, g => g.IsEntireClass);
        }

        // 1.5 soat → 2 ga yaxlitlandi.
        Assert.Equal(2, world.Context.Lessons.Single(l => l.ExternalId == "L2").PeriodsPerWeek);
    }

    [Fact]
    public async Task Bandlik_toʻqnashuvi_importni_yiqitmaydi_kartochka_oʻtkazib_yuboriladi()
    {
        using var world = new AscWorld();

        var result = await world.ImportFileAsync("conflict.xml");

        Assert.True(result.Success, result.ToReport());
        Assert.Contains(result.Messages, m => m.Code == "ASC-CARD-CONFLICT");

        world.Detach();

        // "Butun sinf" darsi qo'yildi, guruh darsi esa o'tkazib yuborildi.
        Assert.Equal(1, world.Context.Cards.Count());

        var stat = result.Stats.Single(s => s.Kind == ImportEntityKind.Card);
        Assert.Equal(2, stat.Found);
        Assert.Equal(1, stat.Created);
        Assert.Equal(1, stat.Skipped);
    }

    [Fact]
    public async Task Buzuq_XML_uchun_tushunarli_istisno()
    {
        using var world = new AscWorld();

        await using var stream = AscTestData.Open("broken.xml");

        var ex = await Assert.ThrowsAsync<AscImportException>(
            () => world.Importer.ImportAsync(stream, world.Options()));

        Assert.Contains("o'qib bo'lmadi", ex.Message, StringComparison.Ordinal);

        // Buzuq faylda bazaga umuman tegilmaydi.
        Assert.Empty(world.Context.Subjects);
    }

    [Fact]
    public async Task Mavjud_boʻlmagan_oʻquv_yili_xato_beradi_lekin_yiqitmaydi()
    {
        using var world = new AscWorld();

        await using var stream = AscTestData.Open("school-small.xml");
        var result = await world.Importer.ImportAsync(
            stream, new ImportOptions { AcademicYearId = 999_999 });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, m => m.Code == "ASC-NO-YEAR");
        Assert.Empty(world.Context.Subjects);
    }

    [Fact]
    public async Task Takroriy_kartochka_dublikat_yaratmaydi()
    {
        using var world = new AscWorld();

        // L1 ning dushanba 1-soatdagi kartochkasini ikki marta yozamiz.
        var text = AscTestData.Read("school-small.xml").Replace(
            "<card lessonid=\"L1\" period=\"1\" days=\"10000\" weeks=\"11\" terms=\"11\" classroomids=\"\"/>",
            "<card lessonid=\"L1\" period=\"1\" days=\"10000\" weeks=\"11\" terms=\"11\" classroomids=\"\"/>"
            + "\n    <card lessonid=\"L1\" period=\"1\" days=\"10000\" weeks=\"11\" terms=\"11\" classroomids=\"\"/>",
            StringComparison.Ordinal);

        await using var stream = AscTestData.Stream(text);
        var result = await world.Importer.ImportAsync(stream, world.Options());

        Assert.True(result.Success, result.ToReport());
        Assert.Contains(result.Messages,
            m => m.Code is "ASC-CARD-DUPLICATE" or "ASC-CARD-CONFLICT");

        world.Detach();
        Assert.Equal(13, world.Context.Cards.Count());
    }

    [Fact]
    public async Task Qisqartma_toʻqnashuvi_import_yiqitmaydi()
    {
        using var world = new AscWorld();

        // Ikkala fan ham short="Mat" — UX_Subjects_Code global unikal indeks bor.
        var text = AscTestData.Read("school-small.xml").Replace(
            "<subject id=\"SFIZ\" name=\"Fizika\" short=\"Fiz\"/>",
            "<subject id=\"SFIZ\" name=\"Fizika\" short=\"Mat\"/>",
            StringComparison.Ordinal);

        await using var stream = AscTestData.Stream(text);
        var result = await world.Importer.ImportAsync(stream, world.Options());

        Assert.True(result.Success, result.ToReport());
        world.Detach();

        var codes = world.Context.Subjects.Select(s => s.Code).ToList();
        Assert.Equal(3, codes.Count);
        Assert.Equal(3, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
