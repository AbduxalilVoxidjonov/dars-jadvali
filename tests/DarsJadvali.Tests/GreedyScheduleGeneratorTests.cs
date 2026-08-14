using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// Avtomatik generator testlari: kichik ma'lumot to'plamida konfliktsiz jadval chiqishi kerak.
/// </summary>
public class GreedyScheduleGeneratorTests
{
    /// <summary>
    /// 2 o'qituvchi, 2 fan, 2 sinf, har bir biriktirmada haftasiga 3 soat = jami 12 dars.
    /// </summary>
    private static (int Expected, TeacherAssignment[] Assignments) SeedSmallDataset(TestDbFactory db)
    {
        db.SeedDefaults();

        var teacher1 = db.AddTeacher("Aliyev Vali");
        var teacher2 = db.AddTeacher("Karimova Nodira");

        var math = db.AddSubject("Matematika", "MAT");
        var physics = db.AddSubject("Fizika", "FIZ");

        var groupA = db.AddClassGroup("5-A");
        var groupB = db.AddClassGroup("5-B");

        const int weekly = 3;
        var assignments = new[]
        {
            db.AddAssignment(teacher1, math, groupA, weekly),
            db.AddAssignment(teacher1, math, groupB, weekly),
            db.AddAssignment(teacher2, physics, groupA, weekly),
            db.AddAssignment(teacher2, physics, groupB, weekly)
        };

        return (assignments.Length * weekly, assignments);
    }

    [Fact]
    public async Task Generator_kichik_toplamda_barcha_soatlarni_joylashtiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var (expected, _) = SeedSmallDataset(db);

        // Act
        var result = await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { ClearExisting = true, RandomSeed = 42 });

        // Assert
        Assert.True(result.Success, string.Join(" | ", result.Messages));
        Assert.Equal(expected, result.PlacedCount);
        Assert.Equal(0, result.UnplacedCount);
        Assert.Equal(expected, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task Generatsiyadan_keyin_jadval_konfliktsiz_boladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        SeedSmallDataset(db);

        // Act
        await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { ClearExisting = true, RandomSeed = 7 });
        var validation = await db.Get<IScheduleValidator>().ValidateAllAsync();

        // Assert — Error darajali konflikt bo'lmasligi shart.
        Assert.True(validation.IsValid, validation.ToDisplayText());
    }

    [Fact]
    public async Task Generator_nofaol_kunga_dars_qoymaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        SeedSmallDataset(db);

        // Act
        await db.Get<IScheduleGenerator>().GenerateAsync(new GenerationOptions { RandomSeed = 1 });

        // Assert
        var entries = await db.Context.ScheduleEntries.AsNoTracking().ToListAsync();
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.NotEqual(WeekDay.Yakshanba, e.DayOfWeek));
        Assert.All(entries, e => Assert.InRange(e.LessonNumber, 1, 7));
    }

    [Fact]
    public async Task ClearExisting_true_bolsa_eski_jadval_ochiriladi()
    {
        // Arrange — generatsiyadan oldin qo'lda qo'yilgan dars bor.
        using var db = new TestDbFactory();
        var (expected, _) = SeedSmallDataset(db);
        var teacher = await db.Context.Teachers.FirstAsync();
        var subject = await db.Context.Subjects.FirstAsync();
        var group = await db.Context.ClassGroups.FirstAsync();
        db.AddEntry(group, subject, teacher, WeekDay.Shanba, 7);

        // Act
        var result = await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { ClearExisting = true, RandomSeed = 3 });

        // Assert — eski yozuv o'rniga faqat yangi generatsiya natijasi qoladi.
        Assert.Equal(expected, result.PlacedCount);
        Assert.Equal(expected, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public void Generator_nomi_va_tavsifi_boshligicha_emas()
    {
        // Arrange
        using var db = new TestDbFactory();

        // Act
        var generator = db.Get<IScheduleGenerator>();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(generator.Name));
        Assert.False(string.IsNullOrWhiteSpace(generator.Description));
    }

    [Fact]
    public async Task Generator_jarayon_haqida_xabar_beradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        SeedSmallDataset(db);
        var reports = new List<GenerationProgress>();
        var progress = new Progress<GenerationProgress>(p => reports.Add(p));

        // Act
        var result = await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { RandomSeed = 11 }, progress);

        // Assert
        Assert.True(result.Elapsed >= TimeSpan.Zero);
        Assert.True(result.PlacedCount > 0);
    }
}
