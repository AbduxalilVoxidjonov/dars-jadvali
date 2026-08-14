using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// O'quv yili ichidagi bir nechta dars jadvali (variant): nusxalash, faol jadvalni
/// almashtirish, o'chirish qoidalari va eng muhimi — jadvallar bir-biriga xalaqit bermasligi.
/// </summary>
public class ScheduleSetServiceTests
{
    [Fact]
    public async Task GetActiveAsync_bosh_bazada_oquv_yili_va_jadval_yaratadi()
    {
        // Arrange — butunlay bo'sh baza.
        using var db = new TestDbFactory();

        // Act
        var active = await db.Get<IScheduleSetService>().GetActiveAsync();

        // Assert — dastur hech qachon jadvalsiz qolmaydi.
        Assert.True(active.Id > 0);
        Assert.True(active.IsActive);
        Assert.Equal("Asosiy jadval", active.Name);
        Assert.Equal(1, await db.Context.AcademicYears.CountAsync());
        Assert.Equal(1, await db.Context.Schedules.CountAsync());
    }

    [Fact]
    public async Task DuplicateAsync_barcha_yozuvlarni_kochiradi_va_originalga_tegmaydi()
    {
        // Arrange — faol jadvalda 3 ta dars.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);

        var source = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, "101");
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 2, "101");
        db.AddEntry(group, subject, teacher, WeekDay.Chorshanba, 3, null);

        var service = db.Get<IScheduleSetService>();

        // Act
        var copy = await service.DuplicateAsync(source.Id, "2-variant");

        // Assert — nusxada ham 3 ta, originalda ham 3 ta.
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(source.AcademicYearId, copy.AcademicYearId);
        Assert.False(copy.IsActive);
        Assert.Equal(3, await service.GetEntryCountAsync(source.Id));
        Assert.Equal(3, await service.GetEntryCountAsync(copy.Id));

        // Yozuvlar mazmuni bir xil, lekin Id lari boshqa (haqiqiy nusxa).
        var sourceEntries = await db.Context.ScheduleEntries.AsNoTracking()
            .Where(e => e.ScheduleId == source.Id).OrderBy(e => e.Id).ToListAsync();
        var copyEntries = await db.Context.ScheduleEntries.AsNoTracking()
            .Where(e => e.ScheduleId == copy.Id).OrderBy(e => e.Id).ToListAsync();

        Assert.Equal(3, copyEntries.Count);
        Assert.Empty(sourceEntries.Select(e => e.Id).Intersect(copyEntries.Select(e => e.Id)));
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(sourceEntries[i].ClassGroupId, copyEntries[i].ClassGroupId);
            Assert.Equal(sourceEntries[i].SubjectId, copyEntries[i].SubjectId);
            Assert.Equal(sourceEntries[i].TeacherId, copyEntries[i].TeacherId);
            Assert.Equal(sourceEntries[i].DayOfWeek, copyEntries[i].DayOfWeek);
            Assert.Equal(sourceEntries[i].LessonNumber, copyEntries[i].LessonNumber);
            Assert.Equal(sourceEntries[i].RoomNumber, copyEntries[i].RoomNumber);
        }
    }

    [Fact]
    public async Task DuplicateAsync_nom_berilmasa_avtomatik_nom_tanlaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var source = db.EnsureActiveSchedule();
        var service = db.Get<IScheduleSetService>();

        // Act
        var first = await service.DuplicateAsync(source.Id);
        var second = await service.DuplicateAsync(source.Id);

        // Assert
        Assert.Equal("Asosiy jadval (nusxa)", first.Name);
        Assert.Equal("Asosiy jadval (nusxa 2)", second.Name);
    }

    [Fact]
    public async Task Ikki_xil_jadvaldagi_bir_xil_orin_konflikt_bermaydi()
    {
        // Arrange — ENG MUHIM TEST.
        // 1-jadvalda 5-A sinfi Dushanba 1-soatda band. 2-jadvalda o'sha o'rin bo'sh bo'lishi kerak.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);

        var first = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, "101", first);

        var year = await db.Context.AcademicYears.SingleAsync();
        var second = db.AddSchedule(year, "2-variant");

        var sets = db.Get<IScheduleSetService>();
        await sets.SetActiveAsync(second.Id);

        var draft = new ScheduleEntryDraft(
            null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, "101");

        // Act — aynan o'sha kun/soat/sinf/o'qituvchi/xona, lekin BOSHQA jadvalda.
        var result = await db.Get<IScheduleService>().PlaceAsync(draft);

        // Assert — hech qanday konflikt bo'lmasligi kerak.
        Assert.True(result.Placed, result.Validation.ToDisplayText());
        Assert.True(result.Validation.IsValid);
        Assert.False(result.Validation.HasWarnings);
        Assert.Equal(2, await db.Context.ScheduleEntries.CountAsync());
        Assert.Equal(1, await sets.GetEntryCountAsync(first.Id));
        Assert.Equal(1, await sets.GetEntryCountAsync(second.Id));
    }

    [Fact]
    public async Task Boshqa_oquv_yili_jadvali_ValidateAllAsync_da_konflikt_bermaydi()
    {
        // Arrange — eski yil jadvalida to'liq band jadval bor.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);

        var oldSchedule = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, oldSchedule);

        var newYear = db.AddAcademicYear("2026–2027", 2026);
        var newSchedule = db.AddSchedule(newYear, "Asosiy jadval");
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, newSchedule);

        await db.Get<IScheduleSetService>().SetActiveAsync(newSchedule.Id);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAllAsync();

        // Assert — yangi yil jadvali o'z-o'zicha to'g'ri, eski yil xalaqit bermaydi.
        Assert.True(result.IsValid, result.ToDisplayText());
    }

    [Fact]
    public async Task Faol_jadval_ozgarsa_IScheduleService_boshqa_yozuvlarni_qaytaradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);

        var first = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, first);
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 1, null, first);

        var year = await db.Context.AcademicYears.SingleAsync();
        var second = db.AddSchedule(year, "II yarim yillik");
        db.AddEntry(group, subject, teacher, WeekDay.Juma, 5, null, second);

        var schedules = db.Get<IScheduleService>();
        var sets = db.Get<IScheduleSetService>();

        // Act + Assert — 1-jadval faol.
        var before = await schedules.GetAllAsync();
        Assert.Equal(2, before.Count);
        Assert.All(before, e => Assert.Equal(first.Id, e.ScheduleId));

        // Act + Assert — 2-jadval faol.
        await sets.SetActiveAsync(second.Id);
        var after = await schedules.GetAllAsync();
        Assert.Single(after);
        Assert.Equal(second.Id, after[0].ScheduleId);
        Assert.Equal(WeekDay.Juma, after[0].DayOfWeek);

        // Sinf va o'qituvchi bo'yicha filtrlar ham faol jadvalga bo'ysunadi.
        Assert.Single(await schedules.GetByClassGroupAsync(group.Id));
        Assert.Single(await schedules.GetByTeacherAsync(teacher.Id));
    }

    [Fact]
    public async Task SetActiveAsync_faqat_bitta_jadvalni_faol_qoldiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var first = db.EnsureActiveSchedule();
        var year = await db.Context.AcademicYears.SingleAsync();
        var second = db.AddSchedule(year, "2-variant");
        var third = db.AddSchedule(year, "3-variant");

        var sets = db.Get<IScheduleSetService>();

        // Act
        await sets.SetActiveAsync(third.Id);

        // Assert
        var all = await db.Context.Schedules.AsNoTracking().ToListAsync();
        var onlyActive = Assert.Single(all, s => s.IsActive);
        Assert.Equal(third.Id, onlyActive.Id);
        Assert.False(all.Single(s => s.Id == first.Id).IsActive);
        Assert.False(all.Single(s => s.Id == second.Id).IsActive);

        // Faol jadval "esda qoladi" — qayta so'ralganda o'sha qaytadi.
        Assert.Equal(third.Id, await sets.GetActiveIdAsync());
    }

    [Fact]
    public async Task ClearAsync_faqat_faol_jadvalni_tozalaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);

        var active = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, active);

        var year = await db.Context.AcademicYears.SingleAsync();
        var other = db.AddSchedule(year, "Arxiv");
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, other);
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 2, null, other);

        // Act
        await db.Get<IScheduleService>().ClearAsync();

        // Assert — boshqa jadval yozuvlari saqlanib qoladi.
        var sets = db.Get<IScheduleSetService>();
        Assert.Equal(0, await sets.GetEntryCountAsync(active.Id));
        Assert.Equal(2, await sets.GetEntryCountAsync(other.Id));
    }

    [Fact]
    public async Task DeleteAsync_jadval_bilan_birga_yozuvlarini_ham_ochiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);

        var keep = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, keep);

        var year = await db.Context.AcademicYears.SingleAsync();
        var doomed = db.AddSchedule(year, "O'chiriladi");
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, doomed);
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 1, null, doomed);

        // Act
        await db.Get<IScheduleSetService>().DeleteAsync(doomed.Id);

        // Assert — kaskad: jadval ham, yozuvlari ham yo'q.
        Assert.Equal(1, await db.Context.Schedules.CountAsync());
        Assert.Equal(1, await db.Context.ScheduleEntries.CountAsync());
        Assert.Equal(keep.Id, (await db.Context.ScheduleEntries.AsNoTracking().SingleAsync()).ScheduleId);
    }

    [Fact]
    public async Task DeleteAsync_oxirgi_jadvalni_ochirmaydi()
    {
        // Arrange — bazada bitta jadval.
        using var db = new TestDbFactory();
        var only = db.EnsureActiveSchedule();

        // Act + Assert — dastur jadvalsiz qolmaydi.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.Get<IScheduleSetService>().DeleteAsync(only.Id));
        Assert.Contains("oxirgi", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.Context.Schedules.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_faol_jadvalni_ochirsa_boshqasi_avtomatik_faollashadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var active = db.EnsureActiveSchedule();
        var year = await db.Context.AcademicYears.SingleAsync();
        var other = db.AddSchedule(year, "2-variant");

        var sets = db.Get<IScheduleSetService>();

        // Act
        await sets.DeleteAsync(active.Id);

        // Assert
        Assert.Equal(other.Id, await sets.GetActiveIdAsync());
        Assert.True((await db.Context.Schedules.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task CreateAsync_bir_oquv_yili_ichida_takroriy_nomga_ruxsat_bermaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.EnsureActiveSchedule();
        var year = await db.Context.AcademicYears.SingleAsync();
        var sets = db.Get<IScheduleSetService>();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sets.CreateAsync(year.Id, "Asosiy jadval"));

        // Boshqa o'quv yilida esa o'sha nom bemalol ishlatiladi.
        var other = db.AddAcademicYear("2026–2027", 2026);
        var created = await sets.CreateAsync(other.Id, "Asosiy jadval");
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task RenameAsync_jadval_nomini_ozgartiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var schedule = db.EnsureActiveSchedule();
        var sets = db.Get<IScheduleSetService>();

        // Act
        await sets.RenameAsync(schedule.Id, "  II yarim yillik  ");

        // Assert — nom trim qilinadi.
        var updated = await sets.GetByIdAsync(schedule.Id);
        Assert.NotNull(updated);
        Assert.Equal("II yarim yillik", updated!.Name);
    }

    [Fact]
    public async Task GetByAcademicYearAsync_faqat_oz_yilining_jadvallarini_qaytaradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.EnsureActiveSchedule();
        var firstYear = await db.Context.AcademicYears.SingleAsync();
        db.AddSchedule(firstYear, "2-variant");

        var secondYear = db.AddAcademicYear("2026–2027", 2026);
        db.AddSchedule(secondYear, "Asosiy jadval");

        var sets = db.Get<IScheduleSetService>();

        // Act + Assert
        Assert.Equal(2, (await sets.GetByAcademicYearAsync(firstYear.Id)).Count);
        Assert.Single(await sets.GetByAcademicYearAsync(secondYear.Id));
        Assert.Equal(3, (await sets.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Generator_faqat_faol_jadvalga_yozadi()
    {
        // Arrange — boshqa jadvalda tegilmasligi kerak bo'lgan yozuv bor.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 4);

        var untouched = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, untouched);
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 1, null, untouched);

        var year = await db.Context.AcademicYears.SingleAsync();
        var target = db.AddSchedule(year, "Generatsiya");

        var sets = db.Get<IScheduleSetService>();
        await sets.SetActiveAsync(target.Id);

        // Act — ClearExisting=true bo'lsa ham faqat FAOL jadval tozalanadi.
        var result = await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { ClearExisting = true, RandomSeed = 42 });

        // Assert
        Assert.True(result.PlacedCount > 0);
        Assert.Equal(2, await sets.GetEntryCountAsync(untouched.Id));
        Assert.Equal(result.PlacedCount, await sets.GetEntryCountAsync(target.Id));

        var generated = await db.Context.ScheduleEntries.AsNoTracking()
            .Where(e => e.ScheduleId != untouched.Id).ToListAsync();
        Assert.All(generated, e => Assert.Equal(target.Id, e.ScheduleId));
    }

    [Fact]
    public async Task Generator_ScheduleId_korsatilsa_osha_jadvalga_yozadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 3);

        var active = db.EnsureActiveSchedule();
        var year = await db.Context.AcademicYears.SingleAsync();
        var target = db.AddSchedule(year, "Qo'lda tanlangan");

        // Act
        var result = await db.Get<IScheduleGenerator>()
            .GenerateAsync(new GenerationOptions { RandomSeed = 5, ScheduleId = target.Id });

        // Assert — faol jadval bo'sh qoladi.
        var sets = db.Get<IScheduleSetService>();
        Assert.True(result.PlacedCount > 0);
        Assert.Equal(0, await sets.GetEntryCountAsync(active.Id));
        Assert.Equal(result.PlacedCount, await sets.GetEntryCountAsync(target.Id));
    }

    [Fact]
    public async Task GetHoursSummaryAsync_faqat_faol_jadvaldagi_soatlarni_sanaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        var assignment = db.AddAssignment(teacher, subject, group, weeklyHours: 5);

        var active = db.EnsureActiveSchedule();
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, null, active);
        db.AddEntry(group, subject, teacher, WeekDay.Seshanba, 1, null, active);

        var year = await db.Context.AcademicYears.SingleAsync();
        var other = db.AddSchedule(year, "Boshqa variant");
        db.AddEntry(group, subject, teacher, WeekDay.Chorshanba, 1, null, other);
        db.AddEntry(group, subject, teacher, WeekDay.Payshanba, 1, null, other);
        db.AddEntry(group, subject, teacher, WeekDay.Juma, 1, null, other);

        var service = db.Get<IAssignmentService>();

        // Act
        var (weekly, placed, remaining) = await service.GetHoursSummaryAsync(assignment.Id);

        // Assert — boshqa jadvaldagi 3 ta dars hisobga OLINMAYDI.
        Assert.Equal(5, weekly);
        Assert.Equal(2, placed);
        Assert.Equal(3, remaining);

        // Aniq jadval ko'rsatilsa — o'sha sanaladi.
        var (_, placedOther, _) = await service.GetHoursSummaryAsync(assignment.Id, other.Id);
        Assert.Equal(3, placedOther);
    }
}
