using DarsJadvali.Application.Scheduling;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Scheduling.Model;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// EF ↔ yadro mapperi: kalit xaritasi, smena uzluksizligi, A/B hafta va TimeOff 3 holati.
/// </summary>
public class SchedulingMapperTests
{
    // =====================================================================
    // 1. Ikki tomonlama kalit mosligi
    // =====================================================================

    [Fact]
    public async Task Kalit_xaritasi_ikki_tomonlama_va_zich()
    {
        // Arrange
        using var world = new GenerationWorld();
        var math = world.AddSubject("Matematika", "MAT");
        var physics = world.AddSubject("Fizika", "FIZ");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");
        var a = world.AddClass("5-A");
        var b = world.AddClass("5-B");
        var l1 = world.AddLesson(math, t1, a, periodsPerWeek: 3);
        var l2 = world.AddLesson(physics, t2, b, periodsPerWeek: 2);

        // Act
        var input = await world.LoadAsync();
        var mapped = world.Mapper().BuildProblem(input);
        var map = mapped.Map;

        // Assert — har bir EF kaliti uchun indeks, har bir indeks uchun EF kaliti.
        foreach (var teacherId in new[] { t1.Id, t2.Id })
        {
            Assert.Equal(teacherId, map.Teachers.DbIdOf(map.Teachers.IndexOf(teacherId)));
        }

        foreach (var classId in new[] { a.Id, b.Id })
        {
            Assert.Equal(classId, map.Classes.DbIdOf(map.Classes.IndexOf(classId)));
        }

        foreach (var lessonId in new[] { l1.Id, l2.Id })
        {
            Assert.Equal(lessonId, map.Lessons.DbIdOf(map.Lessons.IndexOf(lessonId)));
        }

        // Indekslar 0..N-1 zich va yadro massivlari bilan bir xil o'lchamda.
        Assert.Equal(map.Teachers.Count, mapped.Problem.Teachers.Length);
        Assert.Equal(map.Classes.Count, mapped.Problem.Classes.Length);
        Assert.Equal(map.Groups.Count, mapped.Problem.Groups.Length);
        Assert.Equal(map.Subjects.Count, mapped.Problem.Subjects.Length);
        Assert.Equal(map.Lessons.Count, mapped.Problem.Lessons.Length);

        // Har sinfda aSc'dagi standart 5 guruh bor.
        Assert.Equal(10, map.Groups.Count);

        for (var i = 0; i < map.Teachers.Count; i++)
        {
            Assert.Equal(i, map.Teachers.IndexOf(map.Teachers.DbIdOf(i)));
        }
    }

    [Fact]
    public async Task Notogri_kalit_tushunarli_xato_beradi()
    {
        using var world = new GenerationWorld();
        world.AddClass("5-A");

        var input = await world.LoadAsync();
        var mapped = world.Mapper().BuildProblem(input);

        var ex = Assert.Throws<SchedulingMappingException>(() => mapped.Map.Teachers.IndexOf(9999));
        Assert.Contains("9999", ex.Message);
        Assert.Contains("O'qituvchi", ex.Message);
    }

    // =====================================================================
    // 2. Smena uzluksizligi
    // =====================================================================

    [Fact]
    public async Task Smena_soatlari_bitta_uzluksiz_olchamda()
    {
        // Arrange — 2 smena × 6 soat = 1..12 uzluksiz.
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var input = await world.LoadAsync();

        // Act
        var mapped = world.Mapper().BuildProblem(input);
        var map = mapped.Map;

        // Assert — 12 ta soat, indeks 0..11, PeriodNo 1..12 (bo'shliqsiz).
        Assert.Equal(12, map.PeriodCount);
        Assert.Equal(Enumerable.Range(1, 12), map.PeriodNoOfIndex);
        Assert.Equal(6, map.IndexOfPeriodNo[7]);   // 2-smenaning birinchi soati
        Assert.Equal(world.PeriodsByNo[7].Id, map.Periods.DbIdOf(6));
    }

    [Fact]
    public async Task Sinf_faqat_oz_smenasining_soatlarida_ishlaydi()
    {
        // Arrange
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var second = world.AddClass("7-A", world.Shifts[1]);
        var subject = world.AddSubject("Tarix", "TAR");
        var teacher = world.AddTeacher("Rustamov Olim");
        world.AddLesson(subject, teacher, second, periodsPerWeek: 3);

        var input = await world.LoadAsync();

        // Act
        var mapped = world.Mapper().BuildProblem(input);
        var grid = mapped.Problem.Grid;
        var classDef = mapped.Problem.Classes[mapped.Map.Classes.IndexOf(second.Id)];

        // Assert — 1-smenaning soatlari (indeks 0..5) taqiqlangan, 2-smenaniki ochiq.
        for (var periodIndex = 0; periodIndex < 6; periodIndex++)
        {
            Assert.Equal(AvailabilityState.Forbidden,
                classDef.Availability.Get(grid.SlotOf(0, periodIndex)));
        }

        for (var periodIndex = 6; periodIndex < 12; periodIndex++)
        {
            Assert.Equal(AvailabilityState.Allowed,
                classDef.Availability.Get(grid.SlotOf(0, periodIndex)));
        }
    }

    // =====================================================================
    // 3. A/B hafta
    // =====================================================================

    [Fact]
    public async Task AB_hafta_panjarada_ikki_hafta_beradi()
    {
        // Arrange — sikl 2 hafta.
        using var world = new GenerationWorld(weeksInCycle: 2);
        var subject = world.AddSubject("Kimyo", "KIM");
        var teacher = world.AddTeacher("Yusupov Anvar");
        var cls = world.AddClass("8-A");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);

        var input = await world.LoadAsync();

        // Act
        var mapped = world.Mapper().BuildProblem(input);

        // Assert — panjara 2 hafta; haftasiga 2 soat → siklda 4 karta.
        Assert.Equal(2, mapped.Problem.Grid.Weeks);
        Assert.Equal(2, mapped.Map.Weeks);
        Assert.Equal(4, mapped.Problem.Cards.Length);
    }

    [Fact]
    public async Task AB_hafta_maskasi_darsni_bitta_haftaga_qamaydi()
    {
        // Arrange — dars faqat 2-haftada (mask 0b10).
        using var world = new GenerationWorld(weeksInCycle: 2);
        var subject = world.AddSubject("Chizmachilik", "CHZ");
        var teacher = world.AddTeacher("Sobirov Jasur");
        var cls = world.AddClass("9-A");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 2, allowedWeeksMask: 0b10);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 5 });

        // Assert — 2 ta karta va hammasi 2-haftada (WeeksMask = 0b10).
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        var cards = world.Context.Cards.Where(c => c.ScheduleId == world.Schedule.Id).ToList();
        Assert.Equal(2, cards.Count);
        Assert.All(cards, c => Assert.Equal(0b10, c.WeeksMask));
    }

    [Fact]
    public async Task AB_hafta_kartochkalari_har_ikkala_haftaga_taqsimlanadi()
    {
        // Arrange
        using var world = new GenerationWorld(weeksInCycle: 2);
        var subject = world.AddSubject("Biologiya", "BIO");
        var teacher = world.AddTeacher("Nazarova Dilnoza");
        var cls = world.AddClass("10-A");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 11 });

        // Assert
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        var cards = world.Context.Cards.Where(c => c.ScheduleId == world.Schedule.Id).ToList();
        Assert.Equal(4, cards.Count);
        Assert.Equal(2, cards.Count(c => c.WeeksMask == 0b01));
        Assert.Equal(2, cards.Count(c => c.WeeksMask == 0b10));
    }

    // =====================================================================
    // 4. TimeOff — 3 holat
    // =====================================================================

    [Fact]
    public async Task TimeOff_uch_holati_togri_ogiriladi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);

        // Dushanba 1-soat — taqiqlangan; 2-soat — tavsiya etilmaydi; 3-soat — ruxsat.
        world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, 1, AvailabilityLevel.Forbidden);
        world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, 2, AvailabilityLevel.NotRecommended, penalty: 50);
        world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, 3, AvailabilityLevel.Allowed);

        var input = await world.LoadAsync();

        // Act
        var mapped = world.Mapper().BuildProblem(input);
        var grid = mapped.Problem.Grid;
        var def = mapped.Problem.Teachers[mapped.Map.Teachers.IndexOf(teacher.Id)];

        // Assert
        Assert.Equal(AvailabilityState.Forbidden, def.Availability.Get(grid.SlotOf(0, mapped.Map.IndexOfPeriodNo[1])));
        Assert.Equal(AvailabilityState.Questioned, def.Availability.Get(grid.SlotOf(0, mapped.Map.IndexOfPeriodNo[2])));
        Assert.Equal(AvailabilityState.Allowed, def.Availability.Get(grid.SlotOf(0, mapped.Map.IndexOfPeriodNo[3])));
    }

    [Fact]
    public async Task Taqiqlangan_vaqtga_dars_qoyilmaydi()
    {
        // Arrange — o'qituvchi dushanba butun kun band emas.
        using var world = new GenerationWorld(activeDays: 5, periodsPerShift: 6);
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 4);

        for (var periodNo = 1; periodNo <= 6; periodNo++)
        {
            world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, periodNo, AvailabilityLevel.Forbidden);
        }

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 3 });

        // Assert
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        var cards = world.Context.Cards.Where(c => c.ScheduleId == world.Schedule.Id).ToList();
        Assert.Equal(4, cards.Count);
        Assert.All(cards, c => Assert.NotEqual(0, c.DayNo));
    }

    [Fact]
    public async Task Nofaol_kunga_dars_qoyilmaydi()
    {
        // Arrange — faqat 3 kun faol.
        using var world = new GenerationWorld(activeDays: 3);
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 9 });

        // Assert
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        var cards = world.Context.Cards.Where(c => c.ScheduleId == world.Schedule.Id).ToList();
        Assert.NotEmpty(cards);
        Assert.All(cards, c => Assert.InRange(c.DayNo, 0, 2));
    }
}
