using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// CONTRACT 2.4 — o'qituvchi bandligini DARS SOATI raqami bilan belgilash
/// (<see cref="TeacherDayAvailability"/>) va uni <c>TeacherAvailability</c>
/// vaqt oraliqlariga o'girish testlari.
/// </summary>
public class LessonAvailabilityTests
{
    private static TeacherDayAvailability Day(WeekDay day, bool hasRestriction, params int[] lessons)
        => new(day, hasRestriction, lessons);

    private static TeacherDayAvailability Find(
        IReadOnlyList<TeacherDayAvailability> days, WeekDay day)
        => days.Single(d => d.Day == day);

    // -----------------------------------------------------------------
    // 1. Saqlash → o'qish aylanishi (ketma-ket soatlar)
    // -----------------------------------------------------------------
    [Fact]
    public async Task Ketma_ket_soatlar_saqlanib_aynan_qaytadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var service = db.Get<IAvailabilityService>();

        // Act
        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Dushanba, true, 1, 2, 3, 4)
        });

        var days = await service.GetLessonAvailabilityAsync(teacher.Id);
        var monday = Find(days, WeekDay.Dushanba);

        // Assert
        Assert.True(monday.HasRestriction);
        Assert.Equal(new[] { 1, 2, 3, 4 }, monday.AllowedLessonNumbers);

        // Ketma-ket soatlar bitta oraliqqa birlashadi.
        var raw = await service.GetByTeacherAsync(teacher.Id);
        var single = Assert.Single(raw);
        Assert.True(single.IsAvailable);
        Assert.Equal(WeekDay.Dushanba, single.DayOfWeek);
    }

    // -----------------------------------------------------------------
    // 2. Uzilgan tanlov: 1,2,5 → 2 ta oraliq
    // -----------------------------------------------------------------
    [Fact]
    public async Task Uzilgan_tanlov_ikkita_oraliq_boladi_va_aynan_qaytadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var service = db.Get<IAvailabilityService>();

        // Act
        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Seshanba, true, 1, 2, 5)
        });

        var raw = await service.GetByTeacherAsync(teacher.Id);
        var days = await service.GetLessonAvailabilityAsync(teacher.Id);
        var tuesday = Find(days, WeekDay.Seshanba);

        // Assert — 1-2 va 5 alohida oraliq.
        Assert.Equal(2, raw.Count);
        Assert.All(raw, a => Assert.True(a.IsAvailable));

        var slots = await db.Get<IWorkDayService>().GetLessonSlotsAsync();
        var first = raw.OrderBy(a => a.StartTime).First();
        var second = raw.OrderBy(a => a.StartTime).Last();
        Assert.Equal(slots[0].StartTime, first.StartTime);
        Assert.Equal(slots[1].EndTime, first.EndTime);
        Assert.Equal(slots[4].StartTime, second.StartTime);
        Assert.Equal(slots[4].EndTime, second.EndTime);

        Assert.True(tuesday.HasRestriction);
        Assert.Equal(new[] { 1, 2, 5 }, tuesday.AllowedLessonNumbers);
    }

    // -----------------------------------------------------------------
    // 3. Cheklov yo'q → yozuv yozilmaydi
    // -----------------------------------------------------------------
    [Fact]
    public async Task Cheklov_yoq_bolsa_yozuv_yozilmaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var service = db.Get<IAvailabilityService>();

        // Act
        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Dushanba, false),
            Day(WeekDay.Seshanba, true, 3)
        });

        var raw = await service.GetByTeacherAsync(teacher.Id);
        var days = await service.GetLessonAvailabilityAsync(teacher.Id);

        // Assert — faqat Seshanba uchun yozuv bor.
        Assert.Single(raw);
        Assert.Equal(WeekDay.Seshanba, raw[0].DayOfWeek);

        var monday = Find(days, WeekDay.Dushanba);
        Assert.False(monday.HasRestriction);
        Assert.Empty(monday.AllowedLessonNumbers);
    }

    // -----------------------------------------------------------------
    // 4. Cheklov bor, lekin ro'yxat bo'sh → o'sha kuni umuman ishlamaydi
    // -----------------------------------------------------------------
    [Fact]
    public async Task Bosh_royxat_kun_boyi_ishlamaydi_degani()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);

        var service = db.Get<IAvailabilityService>();

        // Act
        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Dushanba, true)
        });

        var days = await service.GetLessonAvailabilityAsync(teacher.Id);
        var monday = Find(days, WeekDay.Dushanba);

        // Assert — birorta soat ruxsat etilmaydi.
        Assert.True(monday.HasRestriction);
        Assert.Empty(monday.AllowedLessonNumbers);

        // Validator ham har bir soatda TEACHER_UNAVAILABLE beradi.
        var validator = db.Get<IScheduleValidator>();
        for (var lesson = 1; lesson <= 7; lesson++)
        {
            var draft = new ScheduleEntryDraft(
                null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, lesson, null);
            var result = await validator.ValidateAsync(draft);

            Assert.False(result.IsValid);
            Assert.Contains(result.Conflicts, c => c.Code == ConflictCodes.TeacherUnavailable);
        }
    }

    // -----------------------------------------------------------------
    // 5. Integratsiya: 1–4 soat ruxsat → 3-soat o'tadi, 6-soat o'tmaydi
    // -----------------------------------------------------------------
    [Fact]
    public async Task Ruxsat_etilgan_soatga_dars_qoyiladi_boshqasiga_yoq()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);

        await db.Get<IAvailabilityService>().SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Dushanba, true, 1, 2, 3, 4)
        });

        var schedule = db.Get<IScheduleService>();

        // Act — 3-soat ruxsat etilgan.
        var ok = await schedule.PlaceAsync(
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 3, null));

        // Act — 6-soat ruxsat etilmagan.
        var bad = await schedule.PlaceAsync(
            new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 6, null));

        // Assert
        Assert.True(ok.Placed);
        Assert.NotNull(ok.Entry);

        Assert.False(bad.Placed);
        Assert.Contains(bad.Validation.Conflicts, c => c.Code == ConflictCodes.TeacherUnavailable);
    }

    // -----------------------------------------------------------------
    // 6. Faqat faol ish kunlari qaytadi
    // -----------------------------------------------------------------
    [Fact]
    public async Task Faqat_faol_ish_kunlari_qaytadi()
    {
        // Arrange — seed'da Dushanba–Shanba faol, Yakshanba nofaol.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();

        // Act
        var days = await db.Get<IAvailabilityService>().GetLessonAvailabilityAsync(teacher.Id);

        // Assert
        Assert.Equal(6, days.Count);
        Assert.DoesNotContain(days, d => d.Day == WeekDay.Yakshanba);
        Assert.Equal(
            new[]
            {
                WeekDay.Dushanba, WeekDay.Seshanba, WeekDay.Chorshanba,
                WeekDay.Payshanba, WeekDay.Juma, WeekDay.Shanba
            },
            days.Select(d => d.Day));
        Assert.All(days, d => Assert.False(d.HasRestriction));
    }

    // -----------------------------------------------------------------
    // 7. LessonSlot bo'lmasa — istisno yo'q
    // -----------------------------------------------------------------
    [Fact]
    public async Task Dars_soatlari_bolmasa_istisno_bolmaydi()
    {
        // Arrange — faqat ish kunlari, dars soatlari yo'q.
        using var db = new TestDbFactory();
        db.SeedWorkDays();
        var teacher = db.AddTeacher();
        var service = db.Get<IAvailabilityService>();

        // Act
        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Dushanba, true, 1, 2, 3)
        });

        var raw = await service.GetByTeacherAsync(teacher.Id);
        var days = await service.GetLessonAvailabilityAsync(teacher.Id);

        // Assert — hech narsa yozilmaydi, o'qish ham xatosiz ishlaydi.
        Assert.Empty(raw);
        Assert.Equal(6, days.Count);
        Assert.All(days, d => Assert.False(d.HasRestriction));
    }

    // -----------------------------------------------------------------
    // 8. Noma'lum soat raqamlari e'tiborsiz qoldiriladi
    // -----------------------------------------------------------------
    [Fact]
    public async Task Nomalum_soat_raqamlari_etiborsiz_qoldiriladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var service = db.Get<IAvailabilityService>();

        // Act — 99 va 0 mavjud emas.
        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Dushanba, true, 0, 2, 3, 99)
        });

        var days = await service.GetLessonAvailabilityAsync(teacher.Id);

        // Assert
        Assert.Equal(new[] { 2, 3 }, Find(days, WeekDay.Dushanba).AllowedLessonNumbers);
    }

    // -----------------------------------------------------------------
    // 9. Qayta saqlash eski yozuvlarni to'liq almashtiradi
    // -----------------------------------------------------------------
    [Fact]
    public async Task Qayta_saqlash_eski_yozuvlarni_almashtiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var service = db.Get<IAvailabilityService>();

        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Dushanba, true, 1, 2),
            Day(WeekDay.Seshanba, true, 5, 6)
        });

        // Act — endi faqat Chorshanba qoladi.
        await service.SaveLessonAvailabilityAsync(teacher.Id, new[]
        {
            Day(WeekDay.Chorshanba, true, 7)
        });

        var raw = await service.GetByTeacherAsync(teacher.Id);
        var days = await service.GetLessonAvailabilityAsync(teacher.Id);

        // Assert
        Assert.Single(raw);
        Assert.Equal(WeekDay.Chorshanba, raw[0].DayOfWeek);
        Assert.False(Find(days, WeekDay.Dushanba).HasRestriction);
        Assert.False(Find(days, WeekDay.Seshanba).HasRestriction);
        Assert.Equal(new[] { 7 }, Find(days, WeekDay.Chorshanba).AllowedLessonNumbers);
    }
}
