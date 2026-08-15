using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// Guruh bo'linishi — eng muhim talab. Bandlik <c>CardOccurrence</c> ning yagona
/// unikal indeksi orqali DB darajasida kafolatlanadi:
/// <c>(ScheduleId, ResourceKind, ResourceId, DayNo, PeriodNo, WeekNo)</c>.
/// </summary>
public class GroupSplitOccupancyTests
{
    [Fact]
    public async Task Ayni_fan_ikki_guruhga_ikki_oqituvchi_bir_vaqtda_RUXSAT()
    {
        // Arrange — 7(a): sinf 2 guruhga bo'linadi, BIR XIL fan, turli o'qituvchilar.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var ingliz = w.AddSubject("Ingliz tili", "ING");
        var oneTeacher = w.AddTeacher("Aliyev Vali");
        var twoTeacher = w.AddTeacher("Karimova Nodira");

        var first = w.AddLesson(ingliz, oneTeacher, klass, w.Group(klass, "1-guruh"));
        var second = w.AddLesson(ingliz, twoTeacher, klass, w.Group(klass, "2-guruh"));

        w.AddCard(first, dayNo: 0, periodNo: 3);
        w.AddCard(second, dayNo: 0, periodNo: 3);

        // Act
        var rows = await w.RebuildAsync();

        // Assert — ikkala karta ham turibdi: guruhlar kesishmaydi.
        Assert.Equal(4, rows); // 2 karta × (1 o'qituvchi + 1 guruh)
        Assert.Equal(2, w.Occurrences().Count(o => o.ResourceKind == ResourceKind.StudentGroup));
    }

    [Fact]
    public async Task Turli_fanlar_ikki_guruhga_bir_vaqtda_RUXSAT()
    {
        // Arrange — 7(b): 1-guruhga informatika, 2-guruhga mehnat.
        using var w = new V2World();
        var klass = w.AddClass("6-B");
        var informatika = w.AddSubject("Informatika", "INF");
        var mehnat = w.AddSubject("Mehnat", "MEH");
        var oneTeacher = w.AddTeacher("Aliyev Vali");
        var twoTeacher = w.AddTeacher("Karimova Nodira");

        var first = w.AddLesson(informatika, oneTeacher, klass, w.Group(klass, "1-guruh"));
        var second = w.AddLesson(mehnat, twoTeacher, klass, w.Group(klass, "2-guruh"));

        w.AddCard(first, dayNo: 2, periodNo: 5);
        w.AddCard(second, dayNo: 2, periodNo: 5);

        // Act
        var rows = await w.RebuildAsync();

        // Assert
        Assert.Equal(4, rows);
    }

    [Fact]
    public async Task Butun_sinf_darsi_va_guruh_darsi_bir_vaqtda_RAD()
    {
        // Arrange — butun sinf darsi sinfning BARCHA guruhlarini band qiladi.
        using var w = new V2World();
        var klass = w.AddClass("7-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var informatika = w.AddSubject("Informatika", "INF");
        var oneTeacher = w.AddTeacher("Aliyev Vali");
        var twoTeacher = w.AddTeacher("Karimova Nodira");

        var whole = w.AddLesson(matematika, oneTeacher, klass, w.EntireClass(klass));
        var half = w.AddLesson(informatika, twoTeacher, klass, w.Group(klass, "1-guruh"));

        w.AddCard(whole, dayNo: 1, periodNo: 2);
        w.AddCard(half, dayNo: 1, periodNo: 2);

        // Act + Assert — DB darajasida rad etiladi, jimgina o'tib ketmaydi.
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => w.RebuildAsync());
    }

    [Fact]
    public async Task Butun_sinf_darsi_barcha_besh_guruhni_band_qiladi()
    {
        // Arrange
        using var w = new V2World();
        var klass = w.AddClass("8-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");
        var whole = w.AddLesson(matematika, teacher, klass, w.EntireClass(klass));
        w.AddCard(whole, dayNo: 1, periodNo: 2);

        // Act
        await w.RebuildAsync();

        // Assert — 5 guruh (butun sinf + 1/2 guruh + o'g'il/qiz) + 1 o'qituvchi.
        var occurrences = w.Occurrences();
        Assert.Equal(5, occurrences.Count(o => o.ResourceKind == ResourceKind.StudentGroup));
        Assert.Equal(1, occurrences.Count(o => o.ResourceKind == ResourceKind.Teacher));
    }

    [Fact]
    public async Task Bir_oqituvchi_ikki_sinfda_bir_slotda_RAD()
    {
        // Arrange — bitta o'qituvchi, ikki turli sinf, ayni kun va soat.
        using var w = new V2World();
        var first = w.AddClass("5-A");
        var second = w.AddClass("5-B");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");

        var one = w.AddLesson(matematika, teacher, first, w.EntireClass(first));
        var two = w.AddLesson(matematika, teacher, second, w.EntireClass(second));

        w.AddCard(one, dayNo: 3, periodNo: 4);
        w.AddCard(two, dayNo: 3, periodNo: 4);

        // Act + Assert
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => w.RebuildAsync());
    }

    [Fact]
    public async Task Oqituvchi_ikkala_smenada_ishlaydi_va_bandligi_yaxlit_korinadi()
    {
        // Arrange — o'qituvchi 1-smenada ham, 2-smenada ham dars beradi.
        // Dars soatlari smenalar bo'ylab uzluksiz raqamlangani uchun
        // 1-smena 3-soat va 2-smena 9-soat TURLI slotlar — to'qnashuv yo'q.
        using var w = new V2World();
        var morning = w.AddClass("5-A", w.Shift1);
        var evening = w.AddClass("9-A", w.Shift2);
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");

        var morningLesson = w.AddLesson(matematika, teacher, morning, w.EntireClass(morning));
        var eveningLesson = w.AddLesson(matematika, teacher, evening, w.EntireClass(evening));

        w.AddCard(morningLesson, dayNo: 0, periodNo: 3);
        w.AddCard(eveningLesson, dayNo: 0, periodNo: 9);

        // Act
        await w.RebuildAsync();

        // Assert — ikkala smena bandligi BITTA jadvalda, bitta o'qituvchi ostida.
        var teacherRows = w.Occurrences()
            .Where(o => o.ResourceKind == ResourceKind.Teacher && o.ResourceId == teacher.Id)
            .ToList();

        Assert.Equal(2, teacherRows.Count);
        Assert.Equal(new[] { 3, 9 }, teacherRows.Select(o => o.PeriodNo).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Bir_oqituvchi_ikkinchi_smenada_ikki_joyda_RAD()
    {
        // Arrange — haqiqiy ikki karra bandlik 2-smenada ham ushlanadi.
        using var w = new V2World();
        var first = w.AddClass("9-A", w.Shift2);
        var second = w.AddClass("9-B", w.Shift2);
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");

        var one = w.AddLesson(matematika, teacher, first, w.EntireClass(first));
        var two = w.AddLesson(matematika, teacher, second, w.EntireClass(second));

        w.AddCard(one, dayNo: 0, periodNo: 9);
        w.AddCard(two, dayNo: 0, periodNo: 9);

        // Act + Assert
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => w.RebuildAsync());
    }

    [Fact]
    public async Task Juft_dars_ikkala_soatni_band_qiladi()
    {
        // Arrange — PeriodsPerCard = 2.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var mehnat = w.AddSubject("Mehnat", "MEH");
        var teacher = w.AddTeacher("Aliyev Vali");

        var lesson = w.AddLesson(mehnat, teacher, klass, w.EntireClass(klass),
            periodsPerWeek: 2, periodsPerCard: 2);

        w.AddCard(lesson, dayNo: 0, periodNo: 3);

        // Act
        await w.RebuildAsync();

        // Assert — 3- va 4-soat band.
        var teacherRows = w.Occurrences()
            .Where(o => o.ResourceKind == ResourceKind.Teacher)
            .Select(o => o.PeriodNo)
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(new[] { 3, 4 }, teacherRows);
    }

    [Fact]
    public async Task Juft_dars_ikkinchi_soatida_boshqa_dars_RAD()
    {
        // Arrange — juft dars 3–4 soat, boshqa dars 4-soatga qo'yiladi.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var mehnat = w.AddSubject("Mehnat", "MEH");
        var tarix = w.AddSubject("Tarix", "TAR");
        var teacher = w.AddTeacher("Aliyev Vali");
        var other = w.AddTeacher("Karimova Nodira");

        var doubleLesson = w.AddLesson(mehnat, teacher, klass, w.EntireClass(klass),
            periodsPerWeek: 2, periodsPerCard: 2);
        var single = w.AddLesson(tarix, other, klass, w.EntireClass(klass));

        w.AddCard(doubleLesson, dayNo: 0, periodNo: 3);
        w.AddCard(single, dayNo: 0, periodNo: 4);

        // Act + Assert — sinf 4-soatda ikki marta band bo'lolmaydi.
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => w.RebuildAsync());
    }

    [Fact]
    public async Task Juft_toq_hafta_bir_slotda_RUXSAT()
    {
        // Arrange — A/B hafta: bitta slot, ikki karta, turli WeeksMask.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var tarix = w.AddSubject("Tarix", "TAR");
        var geografiya = w.AddSubject("Geografiya", "GEO");
        var teacher = w.AddTeacher("Aliyev Vali");

        var odd = w.AddLesson(tarix, teacher, klass, w.EntireClass(klass), periodsPerWeek: 1);
        var even = w.AddLesson(geografiya, teacher, klass, w.EntireClass(klass), periodsPerWeek: 1);

        w.AddCard(odd, dayNo: 0, periodNo: 2, weeksMask: 0b01);   // toq hafta
        w.AddCard(even, dayNo: 0, periodNo: 2, weeksMask: 0b10);  // juft hafta

        // Act
        await w.RebuildAsync();

        // Assert — ikkalasi ham turibdi, faqat WeekNo bilan farq qiladi.
        var weeks = w.Occurrences()
            .Where(o => o.ResourceKind == ResourceKind.Teacher)
            .Select(o => o.WeekNo)
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(new[] { 0, 1 }, weeks);
    }

    [Fact]
    public async Task Ayni_haftada_ayni_slot_RAD()
    {
        // Arrange — A/B hafta maskalari KESISHSA to'qnashuv bo'ladi.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var tarix = w.AddSubject("Tarix", "TAR");
        var geografiya = w.AddSubject("Geografiya", "GEO");
        var teacher = w.AddTeacher("Aliyev Vali");

        var always = w.AddLesson(tarix, teacher, klass, w.EntireClass(klass), periodsPerWeek: 2);
        var odd = w.AddLesson(geografiya, teacher, klass, w.EntireClass(klass), periodsPerWeek: 1);

        w.AddCard(always, dayNo: 0, periodNo: 2, weeksMask: 0b11); // har ikki hafta
        w.AddCard(odd, dayNo: 0, periodNo: 2, weeksMask: 0b01);    // toq hafta — kesishadi

        // Act + Assert
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => w.RebuildAsync());
    }

    [Fact]
    public async Task Turli_chorak_variantlari_bir_slotni_band_qila_oladi()
    {
        // Arrange — chorak = ALOHIDA Schedule varianti, shuning uchun CardOccurrence da
        // TermNo ustuni yo'q: ScheduleId o'zi ajratib turadi.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");

        var lesson = w.AddLesson(matematika, teacher, klass, w.EntireClass(klass));
        w.AddCard(lesson, dayNo: 0, periodNo: 1);
        await w.RebuildAsync();

        var secondTerm = new Domain.Entities.Term
        {
            AcademicYearId = w.Year.Id, Ordinal = 2, Name = "II chorak", ShortName = "II"
        };
        w.Context.Terms.Add(secondTerm);
        w.Context.SaveChanges();

        var secondSchedule = new Domain.Entities.Schedule
        {
            AcademicYearId = w.Year.Id,
            TermId = secondTerm.Id,
            Name = "II chorak — asosiy",
            WeeksInCycle = 2,
            CreatedAt = DateTime.UtcNow
        };
        w.Context.Schedules.Add(secondSchedule);
        w.Context.SaveChanges();

        w.Context.Cards.Add(new Domain.Entities.Card
        {
            ScheduleId = secondSchedule.Id,
            LessonId = lesson.Id,
            PeriodId = w.Periods[1].Id,
            DayNo = 0,
            WeeksMask = 1
        });
        w.Context.SaveChanges();

        // Act — ikkinchi chorak varianti mustaqil quriladi.
        var rows = await w.Projector.RebuildForScheduleAsync(secondSchedule.Id);

        // Assert
        Assert.Equal(6, rows); // 5 guruh + 1 o'qituvchi
        Assert.Equal(12, w.Context.CardOccurrences.Count());
    }
}
