using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// 05-audit K-06: ilgari generator <c>Warning</c> li slotni qabul qilar, lekin
/// <c>ScheduleService.PlaceAsync</c> xuddi shu slotni rad etardi — natijada generator
/// yaratgan darsni foydalanuvchi bir katak ham surolmasdi. Endi ikkalasi ham
/// <see cref="SchedulePlacementPolicy"/> ga tayanadi.
/// </summary>
public class PlacementPolicyConsistencyTests
{
    [Fact]
    public void Siyosat_Error_ni_har_doim_tosadi()
    {
        var conflicts = new[]
        {
            new Conflict(ConflictSeverity.Error, ConflictCodes.TeacherBusy, "O'qituvchi band."),
        };

        Assert.False(SchedulePlacementPolicy.IsAcceptable(conflicts));
        Assert.False(SchedulePlacementPolicy.IsAcceptable(conflicts, force: true));
    }

    [Fact]
    public void Siyosat_Warning_ni_faqat_force_bilan_otkazadi()
    {
        var conflicts = new[]
        {
            new Conflict(ConflictSeverity.Warning, ConflictCodes.SubjectRepeatedInDay, "Fan takrorlanmoqda."),
        };

        Assert.False(SchedulePlacementPolicy.IsAcceptable(conflicts));
        Assert.True(SchedulePlacementPolicy.IsAcceptable(conflicts, force: true));
        Assert.True(SchedulePlacementPolicy.IsAcceptable(Array.Empty<Conflict>()));
    }

    [Fact]
    public async Task Eski_generator_qolda_kochirib_bolmaydigan_dars_yaratmaydi()
    {
        // Arrange — 3 kun faol, lekin haftasiga 6 soat kerak. Ortiqcha soatlarni
        // qo'yish SUBJECT_REPEATED_IN_DAY (Warning) keltirib chiqaradi.
        using var db = new TestDbFactory();
        db.SeedLessonSlots(7);
        foreach (var day in WeekDayExtensions.All)
        {
            db.Context.WorkDays.Add(new Domain.Entities.WorkDay
            {
                DayOfWeek = day,
                IsActive = day is WeekDay.Dushanba or WeekDay.Seshanba or WeekDay.Chorshanba,
                MaxLessonsPerDay = 7
            });
        }

        db.Context.SaveChanges();

        var teacher = db.AddTeacher("Aliyev Vali");
        var subject = db.AddSubject("Matematika", "MAT");
        var group = db.AddClassGroup("5-A");
        db.AddAssignment(teacher, subject, group, weeklyHours: 6);

        // Act
        var result = await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { ClearExisting = true, RandomSeed = 42 });

        // Assert — 3 soat qo'yildi, qolgani ATAYLAB qo'yilmadi.
        Assert.Equal(3, result.PlacedCount);
        Assert.Equal(3, result.UnplacedCount);
        Assert.False(result.Success);

        // Eng muhimi: jadvalda ogohlantirish ham yo'q — ya'ni har bir yozuvni
        // PlaceAsync ham (force=false bilan) qabul qilardi.
        var validation = await db.Get<IScheduleValidator>().ValidateAllAsync();
        Assert.True(validation.IsValid, validation.ToDisplayText());
        Assert.False(validation.HasWarnings, validation.ToDisplayText());
    }

    [Fact]
    public async Task Generator_yozuvlarini_PlaceAsync_ham_qabul_qiladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher("Aliyev Vali");
        var subject = db.AddSubject("Matematika", "MAT");
        var group = db.AddClassGroup("5-A");
        db.AddAssignment(teacher, subject, group, weeklyHours: 4);

        await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { ClearExisting = true, RandomSeed = 5 });

        var entries = await db.Context.ScheduleEntries.AsNoTracking().ToListAsync();
        Assert.NotEmpty(entries);

        // Act & Assert — har bir yozuvni O'Z JOYIGA qayta qo'yish force'siz o'tishi kerak.
        var service = db.Get<IScheduleService>();
        foreach (var entry in entries)
        {
            var placement = await service.PlaceAsync(new ScheduleEntryDraft(
                entry.Id, entry.ClassGroupId, entry.SubjectId, entry.TeacherId,
                entry.DayOfWeek, entry.LessonNumber, entry.RoomNumber, entry.ScheduleId));

            Assert.True(placement.Placed, placement.Validation.ToDisplayText());
        }
    }
}
