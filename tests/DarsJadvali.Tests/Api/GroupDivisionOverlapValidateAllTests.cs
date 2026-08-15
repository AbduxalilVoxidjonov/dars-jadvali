using DarsJadvali.Application.Validation;
using DarsJadvali.Tests.Generation;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// 00 §10.8 (6-TODO): <c>GROUP_DIVISION_OVERLAP</c> endi UMUMIY validatsiya oqimiga —
/// <see cref="IScheduleValidator.ValidateAllAsync(CancellationToken)"/> ga — ulangan.
/// </summary>
/// <remarks>
/// Ilgari bu qoida faqat generatsiya yo'lida (<c>IScheduleGenerationService.ValidateAsync</c>)
/// chaqirilardi, ya'ni QO'LDA tahrirlangan yoki tashqaridan keltirilgan jadval umumiy
/// tekshiruvdan bemalol o'tib ketardi. Baza indeksi buni ushlay olmaydi: turli
/// bo'linishdagi guruhlarning Id'lari har xil.
/// </remarks>
public class GroupDivisionOverlapValidateAllTests
{
    [Fact]
    public async Task ValidateAll_turli_bolinish_ziddiyatini_qaytaradi()
    {
        // Arrange — "1-guruh" va "O'g'illar" AYNAN bir slotda.
        using var world = new GenerationWorld();
        var cls = world.AddClass("5-A");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");

        var english = world.AddLesson(
            world.AddSubject("Ingliz tili", "ING"), t1, cls, world.Group(cls, "1-guruh"), periodsPerWeek: 1);
        var sport = world.AddLesson(
            world.AddSubject("Jismoniy tarbiya", "JIS"), t2, cls, world.Group(cls, "O'g'illar"), periodsPerWeek: 1);

        world.AddCard(english, dayNo: 0, periodNo: 3);
        world.AddCard(sport, dayNo: 0, periodNo: 3);

        // Bandlik qatorlari MUAMMOSIZ quriladi — indeks buzilmaydi.
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        // Act
        var result = await world.Get<IScheduleValidator>().ValidateAllAsync(world.Schedule.Id);

        // Assert
        Assert.False(result.IsValid);
        var conflict = Assert.Single(
            result.Conflicts, c => c.Code == ConflictCodes.GroupDivisionOverlap);

        Assert.Equal(ConflictSeverity.Error, conflict.Severity);
        Assert.Contains("1-guruh", conflict.Message, StringComparison.Ordinal);
        Assert.Contains("O'g'illar", conflict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAll_bir_bolinish_ichidagi_parallel_darslarga_shikoyat_qilmaydi()
    {
        // Arrange — "1-guruh" + "2-guruh" bir slotda: bu RUXSAT etilgan holat.
        using var world = new GenerationWorld();
        var cls = world.AddClass("5-A");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");

        var english = world.AddLesson(
            world.AddSubject("Ingliz tili", "ING"), t1, cls, world.Group(cls, "1-guruh"), periodsPerWeek: 1);
        var german = world.AddLesson(
            world.AddSubject("Nemis tili", "NEM"), t2, cls, world.Group(cls, "2-guruh"), periodsPerWeek: 1);

        world.AddCard(english, dayNo: 0, periodNo: 3);
        world.AddCard(german, dayNo: 0, periodNo: 3);

        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        // Act
        var result = await world.Get<IScheduleValidator>().ValidateAllAsync(world.Schedule.Id);

        // Assert
        Assert.DoesNotContain(result.Conflicts, c => c.Code == ConflictCodes.GroupDivisionOverlap);
    }

    /// <summary>Kartochka yo'q bo'lsa yangi tekshiruv hech narsa qo'shmaydi.</summary>
    [Fact]
    public async Task ValidateAll_kartochkasiz_jadvalda_ozgarmaydi()
    {
        using var world = new GenerationWorld();
        world.AddClass("5-A");

        var result = await world.Get<IScheduleValidator>().ValidateAllAsync(world.Schedule.Id);

        Assert.True(result.IsValid);
        Assert.Empty(result.Conflicts);
    }

    /// <summary>
    /// Kartochka manbasi berilmasa (Infrastructure ro'yxatdan o'tkazilmagan holat)
    /// qolgan qoidalar avvalgidek ishlaydi — yangi bog'liqlik MAJBURIY emas.
    /// </summary>
    [Fact]
    public async Task Kartochka_manbasisiz_validator_ham_ishlaydi()
    {
        using var world = new GenerationWorld();
        var cls = world.AddClass("5-A");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");

        var english = world.AddLesson(
            world.AddSubject("Ingliz tili", "ING"), t1, cls, world.Group(cls, "1-guruh"), periodsPerWeek: 1);
        var sport = world.AddLesson(
            world.AddSubject("Jismoniy tarbiya", "JIS"), t2, cls, world.Group(cls, "O'g'illar"), periodsPerWeek: 1);

        world.AddCard(english, dayNo: 0, periodNo: 3);
        world.AddCard(sport, dayNo: 0, periodNo: 3);

        // Ataylab ISchedulingStore SIZ.
        var validator = new ScheduleValidator(world.UnitOfWork());
        var result = await validator.ValidateAllAsync(world.Schedule.Id);

        Assert.DoesNotContain(result.Conflicts, c => c.Code == ConflictCodes.GroupDivisionOverlap);
    }
}
