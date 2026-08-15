using DarsJadvali.Application.Import;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Import;

/// <summary>Uchdan-uchgacha aSc XML importi.</summary>
public class AscXmlImporterTests
{
    // -------------------------------------------------------------------------
    // Umumiy manzara
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Toʻliq_kichik_maktab_muvaffaqiyatli_import_qilinadi()
    {
        using var world = new AscWorld();

        var result = await world.ImportFileAsync("school-small.xml");

        Assert.True(result.Success, result.ToReport());
        Assert.False(result.DryRun);
        Assert.Empty(result.Errors);

        Assert.Equal(6, world.Context.Periods.Count(p => p.AcademicYearId == world.Year.Id));
        Assert.Equal(2, world.Context.Terms.Count(t => t.AcademicYearId == world.Year.Id));
        Assert.Equal(2, world.Context.Schedules.Count(s => s.AcademicYearId == world.Year.Id));
        Assert.Equal(1, world.Context.Grades.Count(g => g.AcademicYearId == world.Year.Id));
        Assert.Equal(3, world.Context.Subjects.Count(s => s.AcademicYearId == world.Year.Id));
        Assert.Equal(3, world.Context.Teachers.Count(t => t.AcademicYearId == world.Year.Id));
        Assert.Equal(2, world.Context.Classrooms.Count(c => c.AcademicYearId == world.Year.Id));
        Assert.Equal(2, world.Context.SchoolClasses.Count(c => c.AcademicYearId == world.Year.Id));
        Assert.Equal(6, world.Context.ClassDivisions.Count());
        Assert.Equal(10, world.Context.StudentGroups.Count());
        Assert.Equal(6, world.Context.Lessons.Count(l => l.AcademicYearId == world.Year.Id));
        Assert.Equal(13, world.Context.Cards.Count());
    }

    [Fact]
    public async Task Hisobot_oʻzbekcha_va_toʻldirilgan()
    {
        using var world = new AscWorld();

        var result = await world.ImportFileAsync("school-small.xml");
        var report = result.ToReport();

        Assert.Contains("yaratildi", report, StringComparison.Ordinal);
        Assert.Contains("Fanlar", report, StringComparison.Ordinal);
        Assert.Contains("Kartochkalar", report, StringComparison.Ordinal);
        Assert.Contains("Jadval variantlari", report, StringComparison.Ordinal);
        Assert.True(result.TotalCreated > 0);

        var subjects = result.Stats.Single(s => s.Kind == ImportEntityKind.Subject);
        Assert.Equal(3, subjects.Found);
        Assert.Equal(3, subjects.Created);
        Assert.Equal(0, subjects.Updated);
    }

    // -------------------------------------------------------------------------
    // divisiontag — importning eng muhim joyi
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Divisiontag_boʻlinish_va_guruhlarga_toʻgʻri_aylanadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var classA = world.Context.SchoolClasses
            .Include(c => c.Divisions)
            .ThenInclude(d => d.StudentGroups)
            .Single(c => c.ShortName == "5A");

        Assert.Equal(new[] { 0, 1, 2 }, classA.Divisions.Select(d => d.DivisionTag).OrderBy(t => t));

        var entire = classA.Divisions.Single(d => d.DivisionTag == 0);
        Assert.Single(entire.StudentGroups);
        Assert.True(entire.StudentGroups.Single().IsEntireClass);

        var halves = classA.Divisions.Single(d => d.DivisionTag == 1);
        Assert.Equal(new[] { "1-guruh", "2-guruh" }, halves.StudentGroups.Select(g => g.Name).OrderBy(n => n));
        Assert.All(halves.StudentGroups, g => Assert.False(g.IsEntireClass));

        var gender = classA.Divisions.Single(d => d.DivisionTag == 2);
        Assert.Equal(2, gender.StudentGroups.Count);

        // O'quvchilar soni ham ko'chadi.
        Assert.Equal(30, entire.StudentGroups.Single().StudentCount);
    }

    [Fact]
    public async Task Bir_boʻlinishning_ikki_guruhi_bitta_slotda_yonma_yon_turadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var firstSchedule = world.Context.Schedules
            .OrderBy(s => s.Id)
            .First(s => s.AcademicYearId == world.Year.Id);

        // Chorshanba (DayNo = 2), 3-soat: L2 (1-guruh) va L3 (2-guruh).
        var cards = world.Context.Cards
            .Include(c => c.Lesson)
            .Include(c => c.Period)
            .Where(c => c.ScheduleId == firstSchedule.Id && c.DayNo == 2)
            .ToList()
            .Where(c => c.Period!.PeriodNo == 3)
            .ToList();

        Assert.Equal(2, cards.Count);
        Assert.Equal(new[] { "L2", "L3" }, cards.Select(c => c.Lesson!.ExternalId).OrderBy(x => x));

        // Ikkalasi ham bandlik jadvalida — unikal indeks buzilmagan.
        var cardIds = cards.Select(c => c.Id).ToList();
        var occurrences = world.Context.CardOccurrences
            .Where(o => cardIds.Contains(o.CardId) && o.ResourceKind == ResourceKind.StudentGroup)
            .ToList();

        Assert.Equal(2, occurrences.Select(o => o.ResourceId).Distinct().Count());
    }

    [Fact]
    public async Task Guruhsiz_dars_butun_sinf_guruhiga_bogʻlanadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var lesson = world.Context.Lessons
            .Include(l => l.Groups)
            .Single(l => l.ExternalId == "L1");

        var groupId = Assert.Single(lesson.Groups).StudentGroupId;
        var group = world.Context.StudentGroups.Single(g => g.Id == groupId);

        Assert.True(group.IsEntireClass);
    }

    // -------------------------------------------------------------------------
    // lessons.classroomids vs cards.classroomids
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Ruxsat_etilgan_va_tayinlangan_xonalar_ajratiladi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var lesson = world.Context.Lessons
            .Include(l => l.Classrooms)
            .Single(l => l.ExternalId == "L2");

        // lessons.classroomids = RUXSAT ETILGAN → LessonClassroom (2 ta)
        Assert.Equal(2, lesson.Classrooms.Count);
        Assert.Equal(new[] { 0, 1 }, lesson.Classrooms.Select(c => c.Priority).OrderBy(p => p));

        // cards.classroomids = TAYINLANGAN → CardClassroom (har kartochkada 1 ta)
        var cards = world.Context.Cards
            .Include(c => c.Classrooms)
            .Where(c => c.LessonId == lesson.Id)
            .ToList();

        Assert.Equal(2, cards.Count);
        Assert.All(cards, c => Assert.Single(c.Classrooms));

        var assignedShort = world.Context.Classrooms
            .Single(r => r.Id == cards[0].Classrooms.Single().ClassroomId).ShortName;
        Assert.Equal("101", assignedShort);

        // L3 da ruxsat etilgan xona YO'Q, lekin tayinlangan xona BOR.
        var l3 = world.Context.Lessons
            .Include(l => l.Classrooms)
            .Single(l => l.ExternalId == "L3");
        Assert.Empty(l3.Classrooms);

        var l3Card = world.Context.Cards.Include(c => c.Classrooms).First(c => c.LessonId == l3.Id);
        Assert.Single(l3Card.Classrooms);
    }

    // -------------------------------------------------------------------------
    // Bitmask'lar
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Kun_va_hafta_maskalari_intga_oʻgiriladi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var lessons = world.Context.Lessons.ToDictionary(l => l.ExternalId!, l => l);

        // daysdefid=DANY ("00000") → cheklov yo'q
        Assert.Equal(0, lessons["L1"].AllowedDaysMask);

        // daysdefid=DMON ("10000") → faqat dushanba
        Assert.Equal(1, lessons["L4"].AllowedDaysMask);

        // weeksdefid=WA ("10") → faqat A hafta
        Assert.Equal(1, lessons["L6"].AllowedWeeksMask);

        // weeksdefid=WALL ("11") → ikkala hafta
        Assert.Equal(3, lessons["L1"].AllowedWeeksMask);
    }

    [Fact]
    public async Task Kartochkaning_hafta_maskasi_koʻchadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var l1 = world.Context.Lessons.Single(l => l.ExternalId == "L1");
        var l6 = world.Context.Lessons.Single(l => l.ExternalId == "L6");

        Assert.All(world.Context.Cards.Where(c => c.LessonId == l1.Id), c => Assert.Equal(3, c.WeeksMask));
        Assert.All(world.Context.Cards.Where(c => c.LessonId == l6.Id), c => Assert.Equal(1, c.WeeksMask));

        // Hafta sikli o'quv yilida ham, jadval variantida ham 2 ga ko'tarildi.
        Assert.Equal(2, world.Context.AcademicYears.Single(y => y.Id == world.Year.Id).WeeksInCycle);
        Assert.All(world.Context.Schedules.Where(s => s.AcademicYearId == world.Year.Id),
            s => Assert.Equal(2, s.WeeksInCycle));
    }

    [Fact]
    public async Task Juft_dars_kartochka_uzunligiga_aylanadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var lesson = world.Context.Lessons.Single(l => l.ExternalId == "L4");
        Assert.Equal(2, lesson.PeriodsPerCard);

        var cards = world.Context.Cards.Where(c => c.LessonId == lesson.Id).ToList();
        Assert.Equal(2, cards.Count);                 // 2 chorak × 1 kartochka
        Assert.All(cards, c => Assert.Equal(2, c.Length));

        // Juft dars ikki soatni band qiladi → bandlik qatorlari ikki barobar.
        var cardIds = cards.Select(c => c.Id).ToList();
        var periods = world.Context.CardOccurrences
            .Where(o => cardIds.Contains(o.CardId))
            .Select(o => o.PeriodNo)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        Assert.Equal(new[] { 2, 3 }, periods);
    }

    // -------------------------------------------------------------------------
    // Choraklar → alohida jadval variantlari
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Har_chorak_uchun_alohida_jadval_varianti_yaratiladi()
    {
        using var world = new AscWorld();

        var result = await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var schedules = world.Context.Schedules
            .Include(s => s.Term)
            .Where(s => s.AcademicYearId == world.Year.Id)
            .OrderBy(s => s.Id)
            .ToList();

        Assert.Equal(2, schedules.Count);
        Assert.Equal(2, result.ScheduleIds.Count);
        Assert.All(schedules, s => Assert.NotNull(s.TermId));
        Assert.Equal(new[] { 1, 2 }, schedules.Select(s => s.Term!.Ordinal).OrderBy(o => o));

        // Chorak nomlari termsdefs dan olinadi.
        Assert.Contains("Birinchi chorak", schedules[0].Name, StringComparison.Ordinal);
        Assert.Contains("Ikkinchi chorak", schedules[1].Name, StringComparison.Ordinal);

        // Hech qaysisi avtomatik faol qilinmaydi.
        Assert.All(schedules, s => Assert.False(s.IsActive));
    }

    [Fact]
    public async Task Faqat_bitta_chorakda_amal_qiluvchi_kartochka_bitta_variantga_tushadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var firstSchedule = world.Context.Schedules
            .Where(s => s.AcademicYearId == world.Year.Id)
            .OrderBy(s => s.Id)
            .First();

        var lesson = world.Context.Lessons.Single(l => l.ExternalId == "L5");
        var cards = world.Context.Cards.Where(c => c.LessonId == lesson.Id).ToList();

        // terms="10" → faqat I chorak.
        var card = Assert.Single(cards);
        Assert.Equal(firstSchedule.Id, card.ScheduleId);
    }

    [Fact]
    public async Task Ikki_chorakda_amal_qiluvchi_kartochka_ikkala_variantga_nusxalanadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var lesson = world.Context.Lessons.Single(l => l.ExternalId == "L1");
        var cards = world.Context.Cards.Where(c => c.LessonId == lesson.Id).ToList();

        // 2 ta aSc kartochka × 2 chorak.
        Assert.Equal(4, cards.Count);
        Assert.Equal(2, cards.Select(c => c.ScheduleId).Distinct().Count());
    }

    // -------------------------------------------------------------------------
    // Ma'lumotnomalar
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Maʼlumotnoma_maydonlari_koʻchadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var teacher = world.Context.Teachers.Single(t => t.ExternalId == "TA");
        Assert.Equal("Aliyev Vali", teacher.FullName);
        Assert.Equal("AV", teacher.ShortName);
        Assert.Equal(Gender.Male, teacher.Gender);
        Assert.Equal("av@maktab.uz", teacher.Email);

        var subject = world.Context.Subjects.Single(s => s.ExternalId == "SMAT");
        Assert.Equal("Matematika", subject.Name);
        Assert.Equal("Mat", subject.ShortName);
        Assert.Equal("Mat", subject.Code);

        var room = world.Context.Classrooms.Single(r => r.ExternalId == "R1");
        Assert.Equal(30, room.Capacity);

        // capacity="-1" → NULL (CK_Classrooms_Capacity).
        Assert.Null(world.Context.Classrooms.Single(r => r.ExternalId == "R2").Capacity);

        var schoolClass = world.Context.SchoolClasses.Single(c => c.ExternalId == "C5A");
        Assert.Equal("5-A", schoolClass.Name);
        Assert.NotNull(schoolClass.GradeId);
        Assert.Equal(teacher.Id, schoolClass.ClassTeacherId);
        Assert.Equal(room.Id, schoolClass.HomeClassroomId);
    }

    [Fact]
    public async Task Kartochkalarni_import_qilmaslik_rejasini_saqlaydi()
    {
        using var world = new AscWorld();

        var result = await world.ImportFileAsync(
            "school-small.xml", world.Options(importCards: false));

        Assert.True(result.Success, result.ToReport());
        Assert.Equal(6, world.Context.Lessons.Count());
        Assert.Empty(world.Context.Cards);
        Assert.Contains(result.Messages, m => m.Code == "ASC-CARDS-OFF");
    }

    // -------------------------------------------------------------------------
    // Eski sxema
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Eski_2008_sxemasidagi_kun_raqami_toʻgʻri_oʻgiriladi()
    {
        using var world = new AscWorld();

        var result = await world.ImportFileAsync("legacy2008.xml");
        world.Detach();

        Assert.True(result.Success, result.ToReport());

        var cards = world.Context.Cards.OrderBy(c => c.DayNo).ToList();
        Assert.Equal(2, cards.Count);
        Assert.Equal(new[] { 0, 2 }, cards.Select(c => c.DayNo));

        // termsdefs yo'q → bitta jadval varianti, chorakka bog'lanmagan.
        var schedule = Assert.Single(world.Context.Schedules.Where(s => s.AcademicYearId == world.Year.Id));
        Assert.Null(schedule.TermId);
    }
}
