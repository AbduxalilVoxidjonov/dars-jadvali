using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// 1-API: <see cref="IScheduleSnapshotProvider"/> + <see cref="ScheduleValidator.Evaluate"/>.
/// </summary>
/// <remarks>
/// <c>ScheduleSnapshot</c> va <c>LessonAvailabilityRules</c> <c>internal</c> bo'lgani uchun
/// Desktop ularni ishlata olmasdi va <c>TimetableBoard.Evaluate</c> da qoidalar TAKRORLANGAN
/// edi. Bu testlar qoida endi YAGONA manbadan kelishini isbotlaydi.
/// </remarks>
public class ScheduleSnapshotApiTests
{
    /// <summary>Nusxa bir marta yuklanadi va baholash bazaga umuman bormaydi.</summary>
    [Fact]
    public async Task Nusxa_yuklanadi_va_baholash_xotirada_bajariladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 2);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        // Act
        var snapshot = await db.Get<IScheduleSnapshotProvider>().LoadAsync();

        // Assert — nusxada mavjud jadval bor.
        Assert.Single(snapshot.Entries);
        Assert.NotEmpty(snapshot.ActiveWorkDays);
        Assert.Single(snapshot.Teachers);
        Assert.Single(snapshot.ClassGroups);

        // Band slot rad etiladi (bazaga qayta murojaat qilinmaydi).
        var busy = new ScheduleEntryDraft(
            null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null, snapshot.ScheduleId);
        var busyResult = ScheduleValidator.Evaluate(busy, snapshot);
        Assert.False(busyResult.IsValid);
        Assert.Contains(busyResult.Conflicts, c => c.Code == ConflictCodes.ClassBusy);

        // Bo'sh slot qabul qilinadi.
        var free = busy with { LessonNumber = 2 };
        Assert.True(ScheduleValidator.Evaluate(free, snapshot).IsValid);
    }

    /// <summary>
    /// Baholash bazadagi validator bilan AYNAN bir xil natija beradi —
    /// qoida ikki joyda ikki xil bo'lib qolmaydi.
    /// </summary>
    [Fact]
    public async Task Xotiradagi_baholash_validator_bilan_bir_xil()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 1);
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 3);

        var snapshot = await db.Get<IScheduleSnapshotProvider>().LoadAsync();
        var validator = db.Get<IScheduleValidator>();

        var drafts = new[]
        {
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Seshanba, 3, null),
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Seshanba, 4, null),
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Yakshanba, 1, null),
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Juma, 99, null),
        };

        // Act + Assert
        foreach (var draft in drafts)
        {
            var fromDb = await validator.ValidateAsync(draft);
            var fromSnapshot = ScheduleValidator.Evaluate(
                draft with { ScheduleId = snapshot.ScheduleId }, snapshot);

            Assert.Equal(fromDb.IsValid, fromSnapshot.IsValid);
            Assert.Equal(
                fromDb.Conflicts.Select(c => c.Code).OrderBy(x => x),
                fromSnapshot.Conflicts.Select(c => c.Code).OrderBy(x => x));
        }
    }

    /// <summary>
    /// O'qituvchining ish vaqti qoidasi dars soati o'lchovida ham nusxadan olinadi —
    /// UI uni o'zida qayta yozmaydi.
    /// </summary>
    [Fact]
    public async Task Oqituvchi_ish_vaqti_nusxadan_soat_olchovida_olinadi()
    {
        // Arrange — o'qituvchi dushanba faqat 08:30–10:10 (1–2 soat) ishlaydi.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        db.AddAvailability(teacher, WeekDay.Dushanba,
            new TimeSpan(8, 30, 0), new TimeSpan(10, 10, 0), isAvailable: true);
        db.EnsureActiveSchedule();

        // Act
        var snapshot = await db.Get<IScheduleSnapshotProvider>().LoadAsync();

        // Assert
        Assert.True(snapshot.IsTeacherAvailableAt(teacher.Id, WeekDay.Dushanba, 1));
        Assert.True(snapshot.IsTeacherAvailableAt(teacher.Id, WeekDay.Dushanba, 2));
        Assert.False(snapshot.IsTeacherAvailableAt(teacher.Id, WeekDay.Dushanba, 3));

        // Boshqa kunda cheklov yo'q.
        Assert.True(snapshot.IsTeacherAvailableAt(teacher.Id, WeekDay.Seshanba, 5));

        // To'siq ro'yxati ham shu qoidadan hosil bo'ladi.
        var blocked = snapshot.BlockedTeacherSlots();
        Assert.All(blocked, b => Assert.Equal(WeekDay.Dushanba, b.Day));
        Assert.Contains(blocked, b => b.TeacherId == teacher.Id && b.LessonNumber == 3);
        Assert.DoesNotContain(blocked, b => b.LessonNumber == 1);
    }
}
