using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// <c>IScheduleService</c> xatti-harakati: Error bo'lsa saqlamaslik,
/// force bilan Warning'ni bosish, ko'chirish, o'chirish va tozalash.
/// </summary>
public class ScheduleServiceTests
{
    [Fact]
    public async Task PlaceAsync_togri_malumot_bilan_yozuvni_saqlaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 5);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, "101");

        // Act
        var result = await db.Get<IScheduleService>().PlaceAsync(draft);

        // Assert
        Assert.True(result.Placed);
        Assert.NotNull(result.Entry);
        Assert.Equal(1, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task PlaceAsync_Error_bolsa_saqlamaydi()
    {
        // Arrange — biriktirma yo'q (NO_ASSIGNMENT — Error).
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleService>().PlaceAsync(draft);

        // Assert
        Assert.False(result.Placed);
        Assert.Null(result.Entry);
        Assert.False(result.Validation.IsValid);
        Assert.Equal(0, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task PlaceAsync_Warning_bolsa_force_siz_saqlamaydi()
    {
        // Arrange — fan shu kuni takrorlanmoqda (Warning).
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 3, null);

        // Act
        var result = await db.Get<IScheduleService>().PlaceAsync(draft, force: false);

        // Assert — ogohlantirish foydalanuvchiga qaytadi, yozuv qo'shilmaydi.
        Assert.False(result.Placed);
        Assert.True(result.Validation.HasWarnings);
        Assert.Equal(1, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task PlaceAsync_force_true_Warning_ni_bosib_otadi()
    {
        // Arrange — fan takrorlanishi (Warning), force = true.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 3, null);

        // Act
        var result = await db.Get<IScheduleService>().PlaceAsync(draft, force: true);

        // Assert
        Assert.True(result.Placed);
        Assert.Equal(2, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task PlaceAsync_force_true_bolsa_ham_Error_ni_bosmaydi()
    {
        // Arrange — nofaol kun (Error).
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Yakshanba, 1, null);

        // Act
        var result = await db.Get<IScheduleService>().PlaceAsync(draft, force: true);

        // Assert — Error hech qachon bosilmaydi.
        Assert.False(result.Placed);
        Assert.Equal(0, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task MoveAsync_yozuvni_yangi_kun_va_soatga_kochiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);
        var entry = db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        // Act
        var result = await db.Get<IScheduleService>().MoveAsync(entry.Id, WeekDay.Chorshanba, 4);

        // Assert
        Assert.True(result.Placed);
        var moved = await db.Context.ScheduleEntries.AsNoTracking().SingleAsync(x => x.Id == entry.Id);
        Assert.Equal(WeekDay.Chorshanba, moved.DayOfWeek);
        Assert.Equal(4, moved.LessonNumber);
    }

    [Fact]
    public async Task MoveAsync_konflikt_bolsa_kochirmaydi()
    {
        // Arrange — sinf Seshanba 1-darsda band; o'sha joyga ko'chirishga urinamiz.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject1 = db.AddSubject("Matematika", "MAT");
        var subject2 = db.AddSubject("Fizika", "FIZ");
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject1, group, weeklyHours: 10);
        db.AddAssignment(teacher, subject2, group, weeklyHours: 10);
        var entry = db.AddEntry(group, subject1, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(group, subject2, teacher, WeekDay.Seshanba, 1);

        // Act
        var result = await db.Get<IScheduleService>().MoveAsync(entry.Id, WeekDay.Seshanba, 1);

        // Assert — yozuv joyida qoladi.
        Assert.False(result.Placed);
        var unchanged = await db.Context.ScheduleEntries.AsNoTracking().SingleAsync(x => x.Id == entry.Id);
        Assert.Equal(WeekDay.Dushanba, unchanged.DayOfWeek);
        Assert.Equal(1, unchanged.LessonNumber);
    }

    [Fact]
    public async Task RemoveAsync_yozuvni_ochiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        var entry = db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        // Act
        await db.Get<IScheduleService>().RemoveAsync(entry.Id);

        // Assert
        Assert.Equal(0, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task ClearAsync_butun_jadvalni_tozalaydi()
    {
        // Arrange — ikki sinfda bittadan dars.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var groupA = db.AddClassGroup("5-A");
        var groupB = db.AddClassGroup("5-B");
        db.AddAssignment(teacher, subject, groupA);
        db.AddAssignment(teacher, subject, groupB);
        db.AddEntry(groupA, subject, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(groupB, subject, teacher, WeekDay.Dushanba, 2);

        // Act
        await db.Get<IScheduleService>().ClearAsync();

        // Assert
        Assert.Equal(0, await db.Context.ScheduleEntries.CountAsync());
    }

    [Fact]
    public async Task ClearAsync_faqat_korsatilgan_sinfni_tozalaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var groupA = db.AddClassGroup("5-A");
        var groupB = db.AddClassGroup("5-B");
        db.AddAssignment(teacher, subject, groupA);
        db.AddAssignment(teacher, subject, groupB);
        db.AddEntry(groupA, subject, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(groupB, subject, teacher, WeekDay.Dushanba, 2);

        // Act
        await db.Get<IScheduleService>().ClearAsync(groupA.Id);

        // Assert — faqat 5-B ning darsi qoladi.
        var remaining = await db.Context.ScheduleEntries.AsNoTracking().ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(groupB.Id, remaining[0].ClassGroupId);
    }

    [Fact]
    public async Task GetByClassGroupAsync_va_GetByTeacherAsync_filtrlaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher1 = db.AddTeacher("Birinchi O'qituvchi");
        var teacher2 = db.AddTeacher("Ikkinchi O'qituvchi");
        var subject1 = db.AddSubject("Matematika", "MAT");
        var subject2 = db.AddSubject("Fizika", "FIZ");
        var groupA = db.AddClassGroup("5-A");
        var groupB = db.AddClassGroup("5-B");
        db.AddAssignment(teacher1, subject1, groupA);
        db.AddAssignment(teacher2, subject2, groupB);
        db.AddEntry(groupA, subject1, teacher1, WeekDay.Dushanba, 1);
        db.AddEntry(groupB, subject2, teacher2, WeekDay.Dushanba, 1);

        var service = db.Get<IScheduleService>();

        // Act
        var byClass = await service.GetByClassGroupAsync(groupA.Id);
        var byTeacher = await service.GetByTeacherAsync(teacher2.Id);

        // Assert
        Assert.Single(byClass);
        Assert.Equal(groupA.Id, byClass[0].ClassGroupId);
        Assert.Single(byTeacher);
        Assert.Equal(teacher2.Id, byTeacher[0].TeacherId);
    }
}
