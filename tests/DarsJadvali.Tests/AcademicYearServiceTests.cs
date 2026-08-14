using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// O'quv yillari: qo'shish, nomini o'zgartirish, o'chirish (kaskad) va
/// eski yillarning saqlanib qolishi.
/// </summary>
public class AcademicYearServiceTests
{
    [Fact]
    public async Task CreateAsync_yangi_oquv_yili_qoshadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var service = db.Get<IAcademicYearService>();

        // Act
        var created = await service.CreateAsync("  2026–2027  ", 2026, "  Sinov  ");

        // Assert — nom va izoh trim qilinadi.
        Assert.Equal("2026–2027", created.Name);
        Assert.Equal(2026, created.StartYear);
        Assert.Equal("Sinov", created.Note);
    }

    [Fact]
    public async Task CreateAsync_takroriy_nomga_va_bosh_nomga_ruxsat_bermaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var service = db.Get<IAcademicYearService>();
        await service.CreateAsync("2025–2026", 2025);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync("2025–2026", 2025));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync("   ", 2027));
    }

    [Fact]
    public async Task GetAllAsync_yangisidan_eskisiga_qarab_tartiblaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var service = db.Get<IAcademicYearService>();
        await service.CreateAsync("2024–2025", 2024);
        await service.CreateAsync("2026–2027", 2026);
        await service.CreateAsync("2025–2026", 2025);

        // Act
        var all = await service.GetAllAsync();

        // Assert — eski o'quv yillari saqlanib qoladi.
        Assert.Equal(3, all.Count);
        Assert.Equal(new[] { 2026, 2025, 2024 }, all.Select(y => y.StartYear).ToArray());
    }

    [Fact]
    public async Task RenameAsync_nom_va_boshlanish_yilini_ozgartiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var service = db.Get<IAcademicYearService>();
        var year = await service.CreateAsync("2025–2026", 2025);

        // Act
        await service.RenameAsync(year.Id, "2025–2026 (qayta)", 2025, "Izoh");

        // Assert
        var updated = await service.GetByIdAsync(year.Id);
        Assert.NotNull(updated);
        Assert.Equal("2025–2026 (qayta)", updated!.Name);
        Assert.Equal("Izoh", updated.Note);
    }

    [Fact]
    public async Task DeleteAsync_ichidagi_jadval_va_yozuvlarni_ham_ochiradi()
    {
        // Arrange — ikki o'quv yili, har birida jadval va darslar.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);

        var keepSchedule = db.EnsureActiveSchedule();
        var keepYear = await db.Context.AcademicYears.SingleAsync();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, keepSchedule);

        var doomedYear = db.AddAcademicYear("2026–2027", 2026);
        var doomedA = db.AddSchedule(doomedYear, "Asosiy jadval");
        var doomedB = db.AddSchedule(doomedYear, "2-variant");
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, doomedA);
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 1, null, doomedA);
        db.AddEntry(group, subject, teacher, WeekDay.Chorshanba, 1, null, doomedB);

        Assert.Equal(4, await db.Context.ScheduleEntries.CountAsync());

        // Act
        await db.Get<IAcademicYearService>().DeleteAsync(doomedYear.Id);

        // Assert — yil, uning 2 ta jadvali va 3 ta yozuvi butunlay ketdi.
        Assert.Equal(1, await db.Context.AcademicYears.CountAsync());
        Assert.Equal(1, await db.Context.Schedules.CountAsync());
        Assert.Equal(1, await db.Context.ScheduleEntries.CountAsync());
        Assert.Equal(keepYear.Id, (await db.Context.AcademicYears.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(keepSchedule.Id, (await db.Context.ScheduleEntries.AsNoTracking().SingleAsync()).ScheduleId);
    }

    [Fact]
    public async Task DeleteAsync_faol_jadvalli_yil_ochirilsa_boshqasi_faollashadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var keepSchedule = db.EnsureActiveSchedule();

        var newYear = db.AddAcademicYear("2026–2027", 2026);
        var newSchedule = db.AddSchedule(newYear, "Asosiy jadval");

        var sets = db.Get<IScheduleSetService>();
        await sets.SetActiveAsync(newSchedule.Id);

        // Act — aynan faol jadvalli yilni o'chiramiz.
        await db.Get<IAcademicYearService>().DeleteAsync(newYear.Id);

        // Assert — dastur jadvalsiz qolmaydi.
        Assert.Equal(keepSchedule.Id, await sets.GetActiveIdAsync());
    }

    [Fact]
    public async Task DeleteAsync_oxirgi_yilni_ochirmaydi()
    {
        // Arrange — bitta yil, bitta jadval.
        using var db = new TestDbFactory();
        db.EnsureActiveSchedule();
        var year = await db.Context.AcademicYears.SingleAsync();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.Get<IAcademicYearService>().DeleteAsync(year.Id));
        Assert.Equal(1, await db.Context.AcademicYears.CountAsync());
        Assert.Equal(1, await db.Context.Schedules.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateCurrentAsync_bosh_bazada_joriy_yilni_yaratadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var service = db.Get<IAcademicYearService>();

        // Act
        var year = await service.GetOrCreateCurrentAsync();

        // Assert
        var (expectedName, expectedStart) = ActiveScheduleResolver.CurrentAcademicYearName(DateTime.Now);
        Assert.Equal(expectedName, year.Name);
        Assert.Equal(expectedStart, year.StartYear);

        // Ikkinchi chaqiruv yangi yil yaratmaydi.
        var again = await service.GetOrCreateCurrentAsync();
        Assert.Equal(year.Id, again.Id);
        Assert.Equal(1, await db.Context.AcademicYears.CountAsync());
    }

    [Theory]
    [InlineData(2025, 9, "2025–2026", 2025)]
    [InlineData(2025, 12, "2025–2026", 2025)]
    [InlineData(2026, 1, "2025–2026", 2025)]
    [InlineData(2026, 8, "2025–2026", 2025)]
    [InlineData(2026, 9, "2026–2027", 2026)]
    public void CurrentAcademicYearName_sentyabrdan_yangi_yilni_boshlaydi(
        int year, int month, string expectedName, int expectedStart)
    {
        // Act
        var (name, startYear) = ActiveScheduleResolver.CurrentAcademicYearName(new DateTime(year, month, 15));

        // Assert
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedStart, startYear);
    }
}
