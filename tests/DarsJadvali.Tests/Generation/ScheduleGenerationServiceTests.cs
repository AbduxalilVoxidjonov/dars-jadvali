using DarsJadvali.Application.Scheduling;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Scheduling.Pipeline;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// Yangi (kartochka asosidagi) generatsiya servisi: determinizm, qulflar, bandlik va
/// diagnostika.
/// </summary>
public class ScheduleGenerationServiceTests
{
    /// <summary>3 sinf × 3 fan; har fan haftasiga 3 soat = 27 kartochka.</summary>
    private static void SeedSmallSchool(GenerationWorld world)
    {
        var math = world.AddSubject("Matematika", "MAT");
        var physics = world.AddSubject("Fizika", "FIZ");
        var history = world.AddSubject("Tarix", "TAR");

        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");
        var t3 = world.AddTeacher("Rustamov Olim");

        foreach (var name in new[] { "5-A", "5-B", "5-V" })
        {
            var cls = world.AddClass(name);
            world.AddLesson(math, t1, cls, periodsPerWeek: 3);
            world.AddLesson(physics, t2, cls, periodsPerWeek: 3);
            world.AddLesson(history, t3, cls, periodsPerWeek: 3);
        }
    }

    private static List<(int Lesson, int Day, int Period, int Weeks)> Snapshot(GenerationWorld world)
    {
        world.Context.ChangeTracker.Clear();
        return world.Context.Cards.AsNoTracking()
            .Join(world.Context.Periods.AsNoTracking(), c => c.PeriodId, p => p.Id, (c, p) => new
            {
                c.LessonId,
                c.DayNo,
                p.PeriodNo,
                c.WeeksMask
            })
            .AsEnumerable()
            .Select(x => (x.LessonId, x.DayNo, x.PeriodNo, x.WeeksMask))
            .OrderBy(x => x.LessonId).ThenBy(x => x.DayNo).ThenBy(x => x.PeriodNo)
            .ToList();
    }

    [Fact]
    public async Task Kichik_maktabda_barcha_kartochkalar_joylashadi()
    {
        // Arrange
        using var world = new GenerationWorld();
        SeedSmallSchool(world);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 42 });

        // Assert
        Assert.True(report.Success, string.Join(" | ", report.HardViolations.Concat(report.Messages)));
        Assert.True(report.Applied);
        Assert.Equal(27, report.TotalCards);
        Assert.Equal(27, report.PlacedCards);
        Assert.Equal(0, report.UnplacedCards);
        Assert.Empty(report.HardViolations);
        Assert.Equal(27, await world.Context.Cards.CountAsync());

        // Bandlik: har karta uchun 1 o'qituvchi + 5 guruh = 6 qator.
        Assert.Equal(27 * 6, await world.Context.CardOccurrences.CountAsync());
        Assert.Equal(27 * 6, report.OccurrenceRows);
    }

    [Fact]
    public async Task Servis_DI_konteynerdan_olinadi_va_ishlaydi()
    {
        // Arrange — Desktop/Web ham AYNAN shu yo'l bilan oladi.
        using var world = new GenerationWorld();
        SeedSmallSchool(world);

        // Act
        var service = world.Get<IScheduleGenerationService>();
        var report = await service.GenerateAsync(new ScheduleGenerationOptions { Seed = 1 });

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(service.Name));
        Assert.False(string.IsNullOrWhiteSpace(service.Description));
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        Assert.Equal(27, await world.Context.Cards.CountAsync());
    }

    [Fact]
    public async Task Bir_xil_seed_bir_xil_jadval_beradi()
    {
        // Arrange
        using var first = new GenerationWorld();
        SeedSmallSchool(first);
        using var second = new GenerationWorld();
        SeedSmallSchool(second);

        var options = new ScheduleGenerationOptions { Seed = 777, Complexity = Complexity.Small };

        // Act
        await first.Service().GenerateAsync(options);
        await second.Service().GenerateAsync(options);

        // Assert
        Assert.Equal(Snapshot(first), Snapshot(second));
    }

    [Fact]
    public async Task Takroriy_generatsiya_bir_xil_natija_beradi()
    {
        // Arrange
        using var world = new GenerationWorld();
        SeedSmallSchool(world);
        var options = new ScheduleGenerationOptions { Seed = 555, Complexity = Complexity.Small };

        // Act
        await world.Service().GenerateAsync(options);
        var firstRun = Snapshot(world);

        await world.Service().GenerateAsync(options);
        var secondRun = Snapshot(world);

        // Assert — kartochka Id'lari yangi, lekin joylashuv aynan bir xil.
        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public async Task Turli_seed_turli_jadval_berishi_mumkin()
    {
        // Arrange
        using var world = new GenerationWorld();
        SeedSmallSchool(world);

        // Act
        await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 1, Complexity = Complexity.Small });
        var a = Snapshot(world);

        await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 2, Complexity = Complexity.Small });
        var b = Snapshot(world);

        // Assert — ikkalasi ham to'g'ri, lekin aniq bir xil bo'lishi shart emas.
        Assert.Equal(a.Count, b.Count);
    }

    [Fact]
    public async Task Qulflangan_kartochka_joyida_qoladi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var subject = world.AddSubject("Matematika", "MAT");
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);

        // Payshanba (dayNo 3), 5-soat — qulflangan.
        world.AddCard(lesson, dayNo: 3, periodNo: 5, isLocked: true);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 99 });

        // Assert
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        world.Context.ChangeTracker.Clear();

        var locked = await world.Context.Cards.AsNoTracking()
            .Join(world.Context.Periods.AsNoTracking(), c => c.PeriodId, p => p.Id, (c, p) => new { c, p })
            .Where(x => x.c.IsLocked)
            .Select(x => new { x.c.DayNo, x.p.PeriodNo })
            .ToListAsync();

        var one = Assert.Single(locked);
        Assert.Equal(3, one.DayNo);
        Assert.Equal(5, one.PeriodNo);
        Assert.Equal(3, await world.Context.Cards.CountAsync());
    }

    [Fact]
    public async Task Juft_dars_ketma_ket_ikki_soatni_egallaydi()
    {
        // Arrange — haftasiga 4 soat, kartochkasi 2 soatlik → 2 ta juft dars.
        using var world = new GenerationWorld();
        var subject = world.AddSubject("Texnologiya", "TEX");
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 4, periodsPerCard: 2);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 17 });

        // Assert — 2 kartochka, lekin bandlik 2 soatga yoyiladi.
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        Assert.Equal(2, report.TotalCards);
        Assert.Equal(2, await world.Context.Cards.CountAsync());

        // Har karta 2 soat × (1 o'qituvchi + 5 guruh) = 12 qator.
        Assert.Equal(2 * 2 * 6, await world.Context.CardOccurrences.CountAsync());
    }

    [Fact]
    public async Task Imkonsiz_masalada_tushunarli_diagnostika_qaytadi()
    {
        // Arrange — 5 kun × 6 soat = 30 pozitsiya, lekin 40 soat talab qilinmoqda.
        using var world = new GenerationWorld(activeDays: 5, periodsPerShift: 6);
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        world.AddLesson(world.AddSubject("Matematika", "MAT"), teacher, cls, periodsPerWeek: 40);

        // Act
        var report = await world.Service().GenerateAsync(
            new ScheduleGenerationOptions { Seed = 5, Complexity = Complexity.Small });

        // Assert — xato tashlanmaydi, sabab o'zbekcha tushuntiriladi.
        Assert.False(report.Success);
        Assert.True(report.UnplacedCards > 0);
        Assert.NotEmpty(report.VerificationFaults);
        Assert.Contains(report.VerificationFaults, f => f.Contains("CLASS_OVERLOADED", StringComparison.Ordinal));
        Assert.Contains(report.VerificationFaults, f => f.Contains("5-A", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Qisman_natijani_saqlamaslik_mumkin()
    {
        // Arrange
        using var world = new GenerationWorld(activeDays: 5, periodsPerShift: 6);
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        world.AddLesson(world.AddSubject("Matematika", "MAT"), teacher, cls, periodsPerWeek: 40);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions
        {
            Seed = 5,
            Complexity = Complexity.Small,
            SavePartial = false
        });

        // Assert
        Assert.False(report.Applied);
        Assert.Equal(0, await world.Context.Cards.CountAsync());
    }

    [Fact]
    public async Task Progress_fazalari_ozbekcha_nom_bilan_keladi()
    {
        // Arrange
        using var world = new GenerationWorld();
        SeedSmallSchool(world);
        var reports = new List<ScheduleGenerationProgress>();

        // Act
        await world.Service().GenerateAsync(
            new ScheduleGenerationOptions { Seed = 3, Complexity = Complexity.Small },
            new Progress<ScheduleGenerationProgress>(p => reports.Add(p)));

        // Assert
        Assert.NotEmpty(reports);
        Assert.All(reports, p => Assert.False(string.IsNullOrWhiteSpace(p.PhaseName)));
        Assert.All(reports, p => Assert.InRange(p.Percent, 0d, 100d));
        Assert.Contains(reports, p => p.Phase == GenerationPhase.Construct);
    }

    [Fact]
    public async Task Ikki_smenali_maktabda_har_sinf_oz_smenasida_qoladi()
    {
        // Arrange — 1-smena 1..6, 2-smena 7..12.
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var morning = world.AddClass("5-A", world.Shifts[0]);
        var afternoon = world.AddClass("9-A", world.Shifts[1]);

        // Bitta o'qituvchi ikkala smenada ham ishlaydi.
        var teacher = world.AddTeacher("Aliyev Vali");
        var math = world.AddSubject("Matematika", "MAT");
        world.AddLesson(math, teacher, morning, periodsPerWeek: 5);
        world.AddLesson(math, teacher, afternoon, periodsPerWeek: 5);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 21 });

        // Assert
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        Assert.True(report.Success, string.Join(" | ", report.HardViolations));

        world.Context.ChangeTracker.Clear();
        var placed = await world.Context.Cards.AsNoTracking()
            .Join(world.Context.Periods.AsNoTracking(), c => c.PeriodId, p => p.Id,
                  (c, p) => new { c.LessonId, p.PeriodNo })
            .ToListAsync();

        var morningLesson = world.Context.Lessons.OrderBy(l => l.Id).First().Id;
        Assert.All(placed.Where(x => x.LessonId == morningLesson), x => Assert.InRange(x.PeriodNo, 1, 6));
        Assert.All(placed.Where(x => x.LessonId != morningLesson), x => Assert.InRange(x.PeriodNo, 7, 12));
    }

    [Fact]
    public async Task Sinf_bolinishlari_bir_slotda_parallel_ota_oladi()
    {
        // Arrange — bir bo'linishning ikki guruhi (1-guruh / 2-guruh).
        using var world = new GenerationWorld();
        var cls = world.AddClass("5-A");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");

        world.AddLesson(world.AddSubject("Ingliz tili", "ING"), t1, cls, world.Group(cls, "1-guruh"), periodsPerWeek: 2);
        world.AddLesson(world.AddSubject("Nemis tili", "NEM"), t2, cls, world.Group(cls, "2-guruh"), periodsPerWeek: 2);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 8 });

        // Assert — 4 kartochka, hard buzilish yo'q.
        Assert.True(report.Success, string.Join(" | ", report.HardViolations));
        Assert.Equal(4, await world.Context.Cards.CountAsync());
        Assert.Empty(await world.Service().ValidateAsync(world.Schedule.Id));
    }

    [Fact]
    public async Task TimeOff_tavsiya_etilmaydigan_vaqt_jarima_beradi()
    {
        // Arrange — o'qituvchining barcha bo'sh soatlari "?" bilan belgilangan,
        // ya'ni yechim bor, lekin jarimali.
        using var world = new GenerationWorld(activeDays: 1, periodsPerShift: 3);
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        world.AddLesson(world.AddSubject("Matematika", "MAT"), teacher, cls, periodsPerWeek: 3);

        for (var periodNo = 1; periodNo <= 3; periodNo++)
        {
            world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, periodNo,
                             AvailabilityLevel.NotRecommended);
        }

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 4 });

        // Assert — darslar qo'yildi, lekin C-AVL-06 jarimasi hisoblandi.
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        Assert.Equal(3, report.PlacedCards);
        Assert.Contains(report.PenaltyBreakdown, p => p.ConstraintId == "C-AVL-06");
        Assert.True(report.SoftCost > 0);
    }
}
