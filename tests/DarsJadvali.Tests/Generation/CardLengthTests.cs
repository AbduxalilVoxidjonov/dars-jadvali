using DarsJadvali.Application.Scheduling;
using DarsJadvali.Scheduling.Pipeline;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// <c>Card.Length</c> — kartochka uzunligi darsda emas, KARTOCHKADA saqlanadi.
/// </summary>
/// <remarks>
/// Ilgari uzunlik <c>Lesson.PeriodsPerCard</c> dan olinardi va shu sababli
/// <c>PeriodsPerWeek % PeriodsPerCard != 0</c> holati ("haftasiga 5 soat: 2 + 2 + 1")
/// umuman ifodalanmasdi: mapper bunday darsni yakka soatlarga bo'lib yuborardi.
/// Shu fayl aynan o'sha bo'shliq yopilganini isbotlaydi.
/// </remarks>
public class CardLengthTests
{
    /// <summary>5 soat / 2 soatlik juft dars → uzunliklar aynan 2 + 2 + 1.</summary>
    [Fact]
    public async Task Ikki_qoshuv_ikki_qoshuv_bir_taqsimoti_saqlanadi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7, activeDays: 5);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");

        // Haftasiga 5 soat, juft dars istagi 2 → 5 % 2 = 1 (bo'linmaydigan qoldiq).
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 5, periodsPerCard: 2);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions
        {
            ScheduleId = world.Schedule.Id,
            Seed = 4242,
            Complexity = Complexity.Small,
            TimeLimit = TimeSpan.FromSeconds(10),
        });

        // Assert — natija yozildi va uzunliklar 2 + 2 + 1.
        Assert.True(report.Applied, string.Join(" | ", report.Messages));

        var lengths = await world.Context.Cards
            .AsNoTracking()
            .Where(c => c.ScheduleId == world.Schedule.Id)
            .Select(c => c.Length)
            .ToListAsync();

        Assert.Equal(new[] { 1, 2, 2 }, lengths.OrderBy(x => x).ToArray());

        // Jami soat me'yorga teng: 2 + 2 + 1 = 5.
        Assert.Equal(5, lengths.Sum());
    }

    /// <summary>
    /// Bandlik proyeksiyasi kartochka uzunligi bo'yicha yoyiladi: 2 soatlik kartochka
    /// ikkita ketma-ket soatni, qoldiq kartochka esa bittasini band qiladi.
    /// </summary>
    [Fact]
    public async Task Bandlik_qatorlari_kartochka_uzunligi_boyicha_yoyiladi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7, activeDays: 5);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 5, periodsPerCard: 2);

        // Act
        await world.Service().GenerateAsync(new ScheduleGenerationOptions
        {
            ScheduleId = world.Schedule.Id,
            Seed = 4242,
            Complexity = Complexity.Small,
            TimeLimit = TimeSpan.FromSeconds(10),
        });

        // Assert — har kartochkaning o'qituvchi bandligi aynan Length ta qator.
        var cards = await world.Context.Cards
            .AsNoTracking()
            .Where(c => c.ScheduleId == world.Schedule.Id)
            .ToListAsync();

        foreach (var card in cards)
        {
            var rows = await world.Context.CardOccurrences
                .AsNoTracking()
                .CountAsync(o => o.CardId == card.Id &&
                                 o.ResourceKind == Domain.Enums.ResourceKind.Teacher);

            Assert.Equal(card.Length, rows);
        }

        // Umumiy o'qituvchi bandligi = haftalik me'yor (5 soat).
        var total = await world.Context.CardOccurrences
            .AsNoTracking()
            .CountAsync(o => o.ScheduleId == world.Schedule.Id &&
                             o.ResourceKind == Domain.Enums.ResourceKind.Teacher);

        Assert.Equal(5, total);
    }

    /// <summary>
    /// Bir darsning turli uzunlikdagi kartochkalari bir-birining ustiga chiqmaydi:
    /// 2 soatlik kartochkaning IKKINCHI soati ham band bo'ladi.
    /// </summary>
    [Fact]
    public async Task Juft_kartochka_ikkinchi_soatini_ham_egallaydi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7, activeDays: 5);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3, periodsPerCard: 2);

        // Qo'lda: 1-kun, 2-soatdan boshlanuvchi 2 soatlik kartochka.
        world.AddCard(lesson, dayNo: 0, periodNo: 2, length: 2);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        // Assert
        var periods = await world.Context.CardOccurrences
            .AsNoTracking()
            .Where(o => o.ResourceKind == Domain.Enums.ResourceKind.Teacher)
            .Select(o => o.PeriodNo)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        Assert.Equal(new[] { 2, 3 }, periods.ToArray());
    }

    /// <summary>
    /// <c>Length = 0</c> jimgina <c>1</c> ga aylanmaydi — <c>CK_Cards_Length</c> to'sadi.
    /// Bu <c>HasDefaultValue</c> "sentinel" tuzog'iga qarshi qo'riqchi test.
    /// </summary>
    [Fact]
    public async Task Nol_uzunlik_CHECK_bilan_toziladi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);

        world.Context.Cards.Add(new Domain.Entities.Card
        {
            ScheduleId = world.Schedule.Id,
            LessonId = lesson.Id,
            PeriodId = world.PeriodsByNo[1].Id,
            DayNo = 0,
            WeeksMask = 1,
            Length = 0,
        });

        // Act + Assert
        await Assert.ThrowsAsync<Infrastructure.Persistence.CheckConstraintViolationException>(
            () => world.Context.SaveChangesAsync());
    }
}
