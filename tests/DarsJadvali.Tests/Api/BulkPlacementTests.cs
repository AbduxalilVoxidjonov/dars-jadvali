using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// 3-API: <see cref="IScheduleService.PlaceManyAsync"/> — bitta tranzaksiyada ommaviy yozish.
/// </summary>
/// <remarks>
/// Undo/redo dagi <c>CompositeCommand</c> N ta harakatni qaytarayotganda
/// <c>PlaceAsync</c> ni N marta chaqirardi: N ta <c>SaveChanges</c> va o'rtada xato
/// chiqsa <b>yarim qaytarilgan</b> jadval. Bu yerda "hammasi yoki hech narsa".
/// </remarks>
public class BulkPlacementTests
{
    /// <summary>Barchasi to'g'ri bo'lsa hammasi bitta amalda yoziladi.</summary>
    [Fact]
    public async Task Hammasi_togri_bolsa_hammasi_yoziladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 3);
        db.EnsureActiveSchedule();

        var drafts = new[]
        {
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null),
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Seshanba, 2, null),
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Chorshanba, 3, null),
        };

        // Act
        var result = await db.Get<IScheduleService>().PlaceManyAsync(drafts);

        // Assert
        Assert.True(result.Applied);
        Assert.All(result.Results, r => Assert.True(r.Placed));
        Assert.All(result.Results, r => Assert.NotNull(r.Entry));

        var saved = await db.GetFromNewScope<IUnitOfWork>().ScheduleEntries.GetAllAsync();
        Assert.Equal(3, saved.Count);
    }

    /// <summary>
    /// Bittasi rad etilsa <b>hech biri</b> yozilmaydi — yarim holat qolmaydi.
    /// </summary>
    [Fact]
    public async Task Bittasi_rad_etilsa_hech_biri_yozilmaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 5);
        db.EnsureActiveSchedule();

        var drafts = new[]
        {
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null),
            // Yakshanba — dam olish kuni: DAY_INACTIVE.
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Yakshanba, 1, null),
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Seshanba, 1, null),
        };

        // Act
        var result = await db.Get<IScheduleService>().PlaceManyAsync(drafts);

        // Assert
        Assert.False(result.Applied);
        Assert.True(result.Results[0].Placed);
        Assert.False(result.Results[1].Placed);
        Assert.Contains(result.Rejections, c => c.Code == ConflictCodes.DayInactive);

        // Baza tegilmagan.
        Assert.Empty(await db.GetFromNewScope<IUnitOfWork>().ScheduleEntries.GetAllAsync());
    }

    /// <summary>
    /// Ro'yxat ichidagi ikkita loyiha bir slotga tushsa ikkinchisi rad etiladi:
    /// tekshiruv oldingi qarorlarni KO'RADI.
    /// </summary>
    [Fact]
    public async Task Royxat_ichidagi_toqnashuv_ham_aniqlanadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 5);
        db.EnsureActiveSchedule();

        var drafts = new[]
        {
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null),
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null),
        };

        // Act
        var result = await db.Get<IScheduleService>().PlaceManyAsync(drafts);

        // Assert
        Assert.False(result.Applied);
        Assert.False(result.Results[1].Placed);
        Assert.Empty(await db.GetFromNewScope<IUnitOfWork>().ScheduleEntries.GetAllAsync());
    }

    /// <summary>Mavjud yozuvlarni ommaviy ko'chirish (undo stsenariysi) ishlaydi.</summary>
    [Fact]
    public async Task Mavjud_yozuvlarni_ommaviy_kochirish_ishlaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 3);
        var first = db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);
        var second = db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 1);

        // Birinchisini ikkinchisining eski o'rniga surish — tartib muhim:
        // tekshiruv oldingi qarorni ko'rmasa, ikkalasi bir slotda bo'lib qolardi.
        var drafts = new[]
        {
            new ScheduleEntryDraft(second.Id, group.Id, subject.Id, teacher.Id, WeekDay.Seshanba, 2, null),
            new ScheduleEntryDraft(first.Id, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 2, null),
        };

        // Act
        var result = await db.Get<IScheduleService>().PlaceManyAsync(drafts);

        // Assert
        Assert.True(result.Applied, string.Join(" | ", result.Rejections.Select(c => c.Message)));

        var saved = (await db.GetFromNewScope<IUnitOfWork>().ScheduleEntries.GetAllAsync())
            .OrderBy(e => (int)e.DayOfWeek)
            .ToList();

        Assert.Equal(2, saved.Count);
        Assert.All(saved, e => Assert.Equal(2, e.LessonNumber));
    }

    /// <summary>Bo'sh ro'yxat xatosiz o'tadi.</summary>
    [Fact]
    public async Task Bosh_royxat_xatosiz_otadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults();
        db.EnsureActiveSchedule();

        var result = await db.Get<IScheduleService>()
            .PlaceManyAsync(Array.Empty<ScheduleEntryDraft>());

        Assert.True(result.Applied);
        Assert.Empty(result.Results);
    }
}
