using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// <c>GROUP_DIVISION_OVERLAP</c> — DB unikal indeksi ushlay olmaydigan yagona holat
/// (00 §2.7, §10.3): bir sinfda TURLI bo'linishlarning guruhlari bir slotda.
/// </summary>
public class GroupDivisionOverlapTests
{
    private static PlacedCardView Card(int id, string subject, int day, int period, params PlacedGroupRef[] groups)
        => new(id, subject, day, period, 1, 1, groups);

    private static PlacedGroupRef Group(int id, string name, int tag)
        => new(id, name, SchoolClassId: 1, ClassName: "5-A", DivisionTag: tag);

    // =====================================================================
    // Sof qoida
    // =====================================================================

    [Fact]
    public void Turli_bolinish_guruhlari_bir_slotda_xato_beradi()
    {
        // Arrange — "1-guruh" (tag 1) va "O'g'illar" (tag 2) bir vaqtda.
        var cards = new[]
        {
            Card(1, "Matematika", 0, 3, Group(10, "1-guruh", 1)),
            Card(2, "Jismoniy tarbiya", 0, 3, Group(20, "O'g'illar", 2)),
        };

        // Act
        var conflicts = GroupDivisionOverlapValidator.Check(cards);

        // Assert
        var conflict = Assert.Single(conflicts);
        Assert.Equal(ConflictCodes.GroupDivisionOverlap, conflict.Code);
        Assert.Equal(ConflictSeverity.Error, conflict.Severity);
        Assert.Contains("1-guruh", conflict.Message);
        Assert.Contains("O'g'illar", conflict.Message);
        Assert.Contains("5-A", conflict.Message);
        Assert.Contains("Dushanba", conflict.Message);
    }

    [Fact]
    public void Bir_bolinish_ichidagi_guruhlar_parallel_ota_oladi()
    {
        // Arrange — "1-guruh" + "2-guruh" (ikkalasi ham tag 1) — bu RUXSAT.
        var cards = new[]
        {
            Card(1, "Ingliz tili", 0, 3, Group(10, "1-guruh", 1)),
            Card(2, "Nemis tili", 0, 3, Group(11, "2-guruh", 1)),
        };

        // Act & Assert
        Assert.Empty(GroupDivisionOverlapValidator.Check(cards));
    }

    [Fact]
    public void Turli_slotlarda_ziddiyat_yoq()
    {
        var cards = new[]
        {
            Card(1, "Matematika", 0, 3, Group(10, "1-guruh", 1)),
            Card(2, "Jismoniy tarbiya", 0, 4, Group(20, "O'g'illar", 2)),
        };

        Assert.Empty(GroupDivisionOverlapValidator.Check(cards));
    }

    [Fact]
    public void Butun_sinf_va_guruh_ham_ziddiyat_hisoblanadi()
    {
        // "Butun sinf" (tag 0) + "1-guruh" (tag 1) — buni DB ham ushlaydi,
        // lekin qoida uni ham xato deb bilishi kerak.
        var cards = new[]
        {
            Card(1, "Tarix", 0, 2, Group(1, "Butun sinf", 0)),
            Card(2, "Ingliz tili", 0, 2, Group(10, "1-guruh", 1)),
        };

        Assert.Single(GroupDivisionOverlapValidator.Check(cards));
    }

    [Fact]
    public void Juft_dars_ikkinchi_soatida_ham_ushlanadi()
    {
        // 2 soatlik karta (3-4 soat) va 4-soatdagi boshqa bo'linish darsi.
        var cards = new[]
        {
            new PlacedCardView(1, "Texnologiya", 0, 3, 2, 1, new[] { Group(10, "1-guruh", 1) }),
            new PlacedCardView(2, "Jismoniy tarbiya", 0, 4, 1, 1, new[] { Group(20, "Qizlar", 2) }),
        };

        var conflict = Assert.Single(GroupDivisionOverlapValidator.Check(cards));
        Assert.Contains("4-soatda", conflict.Message);
    }

    [Fact]
    public void Turli_haftalardagi_darslar_toqnashmaydi()
    {
        var cards = new[]
        {
            new PlacedCardView(1, "Matematika", 0, 3, 1, 0b01, new[] { Group(10, "1-guruh", 1) }),
            new PlacedCardView(2, "Jismoniy tarbiya", 0, 3, 1, 0b10, new[] { Group(20, "O'g'illar", 2) }),
        };

        Assert.Empty(GroupDivisionOverlapValidator.Check(cards));
    }

    // =====================================================================
    // Baza ustida: DB ushlay olmaydi, Application ushlaydi
    // =====================================================================

    [Fact]
    public async Task Baza_ushlay_olmaydi_lekin_validator_ushlaydi()
    {
        // Arrange — bir sinfning "1-guruh" va "O'g'illar" darslari AYNAN bir slotda.
        using var world = new GenerationWorld();
        var cls = world.AddClass("5-A");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");
        var english = world.AddSubject("Ingliz tili", "ING");
        var sport = world.AddSubject("Jismoniy tarbiya", "JIS");

        var groupLesson = world.AddLesson(english, t1, cls, world.Group(cls, "1-guruh"), periodsPerWeek: 1);
        var boysLesson = world.AddLesson(sport, t2, cls, world.Group(cls, "O'g'illar"), periodsPerWeek: 1);

        world.AddCard(groupLesson, dayNo: 0, periodNo: 3);
        world.AddCard(boysLesson, dayNo: 0, periodNo: 3);

        // Act — bandlik qatorlari MUAMMOSIZ quriladi: guruh Id'lari har xil,
        // shuning uchun UNIQUE indeks buzilmaydi. Aynan shu sabab qoida kerak.
        var rows = await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);
        var conflicts = await world.Service().ValidateAsync(world.Schedule.Id);

        // Assert
        Assert.True(rows > 0, "Bandlik qatorlari yozilmadi.");
        var conflict = Assert.Single(conflicts);
        Assert.Equal(ConflictCodes.GroupDivisionOverlap, conflict.Code);
    }

    [Fact]
    public async Task Bir_bolinish_ichidagi_parallel_darslar_bazada_ham_ruxsat()
    {
        // Arrange — "1-guruh" + "2-guruh" bir slotda (7a/7b stsenariysi).
        using var world = new GenerationWorld();
        var cls = world.AddClass("5-A");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");
        var english = world.AddSubject("Ingliz tili", "ING");
        var german = world.AddSubject("Nemis tili", "NEM");

        var first = world.AddLesson(english, t1, cls, world.Group(cls, "1-guruh"), periodsPerWeek: 1);
        var second = world.AddLesson(german, t2, cls, world.Group(cls, "2-guruh"), periodsPerWeek: 1);

        world.AddCard(first, dayNo: 0, periodNo: 3);
        world.AddCard(second, dayNo: 0, periodNo: 3);

        // Act
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);
        var conflicts = await world.Service().ValidateAsync(world.Schedule.Id);

        // Assert
        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task Generator_bolinish_ziddiyatini_yaratmaydi()
    {
        // Arrange — bitta sinfda uchala bo'linishning darslari bor.
        using var world = new GenerationWorld();
        var cls = world.AddClass("5-A");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");
        var t3 = world.AddTeacher("Rustamov Olim");

        world.AddLesson(world.AddSubject("Matematika", "MAT"), t1, cls, world.EntireClass(cls), periodsPerWeek: 5);
        world.AddLesson(world.AddSubject("Ingliz tili", "ING"), t2, cls, world.Group(cls, "1-guruh"), periodsPerWeek: 3);
        world.AddLesson(world.AddSubject("Jismoniy tarbiya", "JIS"), t3, cls, world.Group(cls, "O'g'illar"), periodsPerWeek: 2);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 2026 });

        // Assert — yadro C-GBL-08 ni hard cheklov sifatida ushlaydi.
        Assert.Empty(report.Conflicts);
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        Assert.Empty(await world.Service().ValidateAsync(world.Schedule.Id));
        Assert.Equal(10, await world.Context.Cards.CountAsync());
    }
}
