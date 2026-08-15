using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Tests.Generation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// Jadval to'rining YOZUVCHI API'lari: bitta kartochkani o'chirish, yangi kartochka
/// yaratish (xona bilan), joylashtirilmagan darsning sinf/guruh Id'lari va sinf
/// smenasini o'zgartirish.
/// </summary>
/// <remarks>
/// Ilgari bu yo'llarning hech biri yo'q edi: bitta kartochkani o'chirish uchun butun
/// jadval qayta yozilardi (<c>Card.Id</c> lar o'zgarardi), yangi kartochka yaratish
/// esa prezentatsiya qatlamini <c>ISchedulingStore</c> ni to'g'ridan-to'g'ri
/// chaqirishga majbur qilardi.
/// </remarks>
public class CardBoardWriteApiTests
{
    // ---------------------------------------------------------------------
    // DeleteCardAsync
    // ---------------------------------------------------------------------

    /// <summary>
    /// Bitta kartochka o'chadi, bandlik qatorlari ham ketadi, QOLGAN kartochkalarning
    /// Id'lari esa o'zgarmaydi — undo tarixi va taxta holati saqlanib qoladi.
    /// </summary>
    [Fact]
    public async Task Bitta_kartochka_ochiriladi_qolganlarning_Idlari_ozgarmaydi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);

        var first = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        var second = world.AddCard(lesson, dayNo: 1, periodNo: 1);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var occurrencesBefore = await world.Context.CardOccurrences.CountAsync();
        Assert.True(occurrencesBefore > 0);

        // Act
        var deleted = await world.Get<ICardBoardService>().DeleteCardAsync(first.Id);

        // Assert
        Assert.True(deleted);

        world.Context.ChangeTracker.Clear();
        var remaining = await world.Context.Cards.AsNoTracking().ToListAsync();
        Assert.Equal(second.Id, Assert.Single(remaining).Id);

        Assert.Empty(await world.Context.CardOccurrences.AsNoTracking()
            .Where(o => o.CardId == first.Id).ToListAsync());
        Assert.NotEmpty(await world.Context.CardOccurrences.AsNoTracking()
            .Where(o => o.CardId == second.Id).ToListAsync());
    }

    /// <summary>Mavjud bo'lmagan kartochka — xato emas, shunchaki <c>false</c>.</summary>
    [Fact]
    public async Task Yoq_kartochkani_ochirish_false_qaytaradi()
    {
        using var world = new GenerationWorld();
        Assert.False(await world.Get<ICardBoardService>().DeleteCardAsync(4242));
    }

    /// <summary>Kartochkaga tayinlangan xona bog'lanishi ham birga o'chadi.</summary>
    [Fact]
    public async Task Ochirilgan_kartochkaning_xona_boglanishi_ham_ketadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Fizika", "FIZ");
        var cls = world.AddClass("6-B");
        var room = world.AddClassroom("204-xona");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 1);

        var card = world.AddCard(lesson, dayNo: 0, periodNo: 2);
        world.AssignRoom(card, room);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        Assert.True(await world.Get<ICardBoardService>().DeleteCardAsync(card.Id));

        world.Context.ChangeTracker.Clear();
        Assert.Empty(await world.Context.CardClassrooms.AsNoTracking().ToListAsync());
    }

    // ---------------------------------------------------------------------
    // CreateCardAsync
    // ---------------------------------------------------------------------

    /// <summary>Yangi kartochka yoziladi va bandlik qatorlari darhol quriladi.</summary>
    [Fact]
    public async Task Yangi_kartochka_yaratiladi_va_bandlik_quriladi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2, periodsPerCard: 2);

        // Act
        var result = await world.Get<ICardBoardService>().CreateCardAsync(
            new CardCreateRequest(lesson.Id, DayNo: 2, PeriodId: world.PeriodsByNo[3].Id, Length: 2));

        // Assert
        Assert.True(result.Created, string.Join(" | ", result.Conflicts.Select(c => c.Message)));
        Assert.True(result.CardId > 0);
        Assert.True(result.OccurrenceRows > 0);

        world.Context.ChangeTracker.Clear();
        var card = await world.Context.Cards.AsNoTracking().SingleAsync(c => c.Id == result.CardId);
        Assert.Equal(2, card.DayNo);
        Assert.Equal(world.PeriodsByNo[3].Id, card.PeriodId);
        Assert.Equal(2, card.Length);

        // Juft dars IKKALA soatni band qiladi.
        var periodNos = await world.Context.CardOccurrences.AsNoTracking()
            .Where(o => o.CardId == result.CardId)
            .Select(o => o.PeriodNo).Distinct().OrderBy(n => n).ToListAsync();
        Assert.Equal(new[] { 3, 4 }, periodNos);
    }

    /// <summary>Band slotga yaratish rad etiladi va bazaga UMUMAN tegilmaydi.</summary>
    [Fact]
    public async Task Band_slotga_yaratish_rad_etiladi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 4);

        world.AddCard(lesson, dayNo: 0, periodNo: 1);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var result = await world.Get<ICardBoardService>().CreateCardAsync(
            new CardCreateRequest(lesson.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id));

        Assert.False(result.Created);
        Assert.NotEmpty(result.Conflicts);
        Assert.Equal(0, result.CardId);

        world.Context.ChangeTracker.Clear();
        Assert.Equal(1, await world.Context.Cards.CountAsync());
    }

    /// <summary>Dars ta'rifi topilmasa — aniq sabab bilan rad etiladi.</summary>
    [Fact]
    public async Task Yoq_dars_uchun_yaratish_rad_etiladi()
    {
        using var world = new GenerationWorld();

        var result = await world.Get<ICardBoardService>().CreateCardAsync(
            new CardCreateRequest(4242, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id));

        Assert.False(result.Created);
        Assert.Contains(result.Conflicts, c => c.Message.Contains("4242", StringComparison.Ordinal));
    }

    /// <summary>
    /// Xona MATNI (<c>Card.LegacyRoomNumber</c>) va tayinlangan xona
    /// (<c>CardClassroom</c>) ikkalasi ham yoziladi va DTO'ga qaytadi.
    /// </summary>
    [Fact]
    public async Task Yaratishda_xona_saqlanadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Kimyo", "KIM");
        var cls = world.AddClass("7-A");
        var room = world.AddClassroom("Kimyo xonasi");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 1);

        var result = await world.Get<ICardBoardService>().CreateCardAsync(
            new CardCreateRequest(lesson.Id, DayNo: 1, PeriodId: world.PeriodsByNo[2].Id)
            {
                ClassroomIds = new[] { room.Id },
                RoomNumber = "301",
                IsLocked = true,
            });

        Assert.True(result.Created, string.Join(" | ", result.Conflicts.Select(c => c.Message)));

        world.Context.ChangeTracker.Clear();
        var card = await world.Context.Cards.AsNoTracking().SingleAsync(c => c.Id == result.CardId);
        Assert.Equal("301", card.LegacyRoomNumber);
        Assert.True(card.IsLocked);

        var link = await world.Context.CardClassrooms.AsNoTracking().SingleAsync();
        Assert.Equal(room.Id, link.ClassroomId);

        // Tayinlangan xona bandlik qatorlariga ham tushadi — ikkinchi dars shu xonaga
        // qo'yilmasligi uchun.
        Assert.Contains(
            await world.Context.CardOccurrences.AsNoTracking()
                .Where(o => o.CardId == result.CardId).ToListAsync(),
            o => o.ResourceKind == Domain.Enums.ResourceKind.Classroom && o.ResourceId == room.Id);

        var view = Assert.Single(
            await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id));
        Assert.Equal(new[] { room.Id }, view.ClassroomIds);
    }

    /// <summary>Tayinlangan xona BAND bo'lsa ikkinchi kartochka yaratilmaydi.</summary>
    [Fact]
    public async Task Band_xonaga_ikkinchi_kartochka_yaratilmaydi()
    {
        using var world = new GenerationWorld();
        var subject = world.AddSubject("Kimyo", "KIM");
        var room = world.AddClassroom("Kimyo xonasi", isShared: true);

        var lessonA = world.AddLesson(
            subject, world.AddTeacher("Aliyev Vali"), world.AddClass("7-A"), periodsPerWeek: 1);
        var lessonB = world.AddLesson(
            subject, world.AddTeacher("Karimov Olim"), world.AddClass("7-B"), periodsPerWeek: 1);

        var board = world.Get<ICardBoardService>();
        var request = new CardCreateRequest(lessonA.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id)
        {
            ClassroomIds = new[] { room.Id },
        };

        Assert.True((await board.CreateCardAsync(request)).Created);

        var second = await board.CreateCardAsync(
            new CardCreateRequest(lessonB.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id)
            {
                ClassroomIds = new[] { room.Id },
            });

        Assert.False(second.Created);
        Assert.Contains(second.Conflicts,
            c => c.Code == Application.Validation.ConflictCodes.RoomBusy);
    }

    /// <summary>Hafta maskasi hurmat qilinadi: B haftalik dars faqat B haftani band qiladi.</summary>
    [Fact]
    public async Task Hafta_maskasi_bilan_yaratilgan_kartochka_faqat_oz_haftasini_band_qiladi()
    {
        using var world = new GenerationWorld(weeksInCycle: 2);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Tarix", "TAR");
        var cls = world.AddClass("8-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 1);

        var result = await world.Get<ICardBoardService>().CreateCardAsync(
            new CardCreateRequest(
                lesson.Id, DayNo: 3, PeriodId: world.PeriodsByNo[2].Id, Length: 1, WeeksMask: 0b10));

        Assert.True(result.Created, string.Join(" | ", result.Conflicts.Select(c => c.Message)));

        world.Context.ChangeTracker.Clear();
        var card = await world.Context.Cards.AsNoTracking().SingleAsync();
        Assert.Equal(3, card.DayNo);
        Assert.Equal(0b10, card.WeeksMask);

        // Faqat B haftada band bo'ladi (WeekNo = 1).
        var weeks = await world.Context.CardOccurrences.AsNoTracking()
            .Select(o => o.WeekNo).Distinct().ToListAsync();
        Assert.Equal(new[] { 1 }, weeks);
    }

    // ---------------------------------------------------------------------
    // UnplacedLessonView: SchoolClassIds / StudentGroupIds
    // ---------------------------------------------------------------------

    /// <summary>
    /// Joylashtirilmagan dars endi sinf va guruh Id'larini beradi — prezentatsiya
    /// qatlami sinfni NOM bo'yicha izlashga majbur emas (bir xil nomli sinfda bu
    /// yo'l sinardi).
    /// </summary>
    [Fact]
    public async Task Joylashtirilmagan_dars_sinf_va_guruh_Idlarini_beradi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Ingliz tili", "ING");
        var cls = world.AddClass("9-A");
        var group = world.Group(cls, "1-guruh");
        var lesson = world.AddLesson(subject, teacher, cls, group, periodsPerWeek: 3);

        var unplaced = Assert.Single(
            await world.Get<ICardBoardService>().GetUnplacedAsync(world.Schedule.Id));

        Assert.Equal(lesson.Id, unplaced.LessonId);
        Assert.Equal(new[] { cls.Id }, unplaced.SchoolClassIds);
        Assert.Equal(new[] { group.Id }, unplaced.StudentGroupIds);
        Assert.Equal(3, unplaced.RemainingPeriods);
    }

    // ---------------------------------------------------------------------
    // SetClassShiftAsync
    // ---------------------------------------------------------------------

    /// <summary>Sinf smenasi o'zgaradi.</summary>
    [Fact]
    public async Task Sinf_smenasi_ozgartiriladi()
    {
        using var world = new GenerationWorld(shiftCount: 2);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        Assert.True(await world.Store().SetClassShiftAsync(cls.Id, world.Shifts[1].Id));

        world.Context.ChangeTracker.Clear();
        var stored = await world.Context.SchoolClasses.AsNoTracking().SingleAsync(c => c.Id == cls.Id);
        Assert.Equal(world.Shifts[1].Id, stored.ShiftId);
    }

    /// <summary>Smenadan chiqarish (<c>null</c>) ham qo'llab-quvvatlanadi.</summary>
    [Fact]
    public async Task Sinf_smenadan_chiqariladi()
    {
        using var world = new GenerationWorld(shiftCount: 2);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        Assert.True(await world.Store().SetClassShiftAsync(cls.Id, null));

        world.Context.ChangeTracker.Clear();
        Assert.Null((await world.Context.SchoolClasses.AsNoTracking()
            .SingleAsync(c => c.Id == cls.Id)).ShiftId);
    }

    /// <summary>Yo'q sinf yoki yo'q smena — <c>false</c>, baza tegilmaydi.</summary>
    [Fact]
    public async Task Yoq_sinf_yoki_smena_rad_etiladi()
    {
        using var world = new GenerationWorld(shiftCount: 2);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        Assert.False(await world.Store().SetClassShiftAsync(4242, world.Shifts[1].Id));
        Assert.False(await world.Store().SetClassShiftAsync(cls.Id, 4242));

        world.Context.ChangeTracker.Clear();
        Assert.Equal(world.Shifts[0].Id, (await world.Context.SchoolClasses.AsNoTracking()
            .SingleAsync(c => c.Id == cls.Id)).ShiftId);
    }

    /// <summary>
    /// BOSHQA o'quv yilining smenasi rad etiladi — aks holda sinf hech qachon
    /// ochilmaydigan dars soatlariga bog'lanib qolardi.
    /// </summary>
    [Fact]
    public async Task Boshqa_oquv_yilining_smenasi_rad_etiladi()
    {
        using var world = new GenerationWorld(shiftCount: 1);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        var otherYear = new Domain.Entities.AcademicYear { Name = "2024–2025", StartYear = 2024 };
        world.Context.AcademicYears.Add(otherYear);
        world.Context.SaveChanges();

        var alienShift = new Domain.Entities.Shift
        {
            AcademicYearId = otherYear.Id,
            ShiftNo = 1,
            Name = "Begona smena",
            ShortName = "B",
        };
        world.Context.Shifts.Add(alienShift);
        world.Context.SaveChanges();

        Assert.False(await world.Store().SetClassShiftAsync(cls.Id, alienShift.Id));

        world.Context.ChangeTracker.Clear();
        Assert.Equal(world.Shifts[0].Id, (await world.Context.SchoolClasses.AsNoTracking()
            .SingleAsync(c => c.Id == cls.Id)).ShiftId);
    }

    /// <summary>Ayni smena qayta tayinlansa — yozuv qilinmaydi, lekin <c>true</c>.</summary>
    [Fact]
    public async Task Ayni_smena_qayta_tayinlansa_true_qaytadi()
    {
        using var world = new GenerationWorld(shiftCount: 2);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        Assert.True(await world.Store().SetClassShiftAsync(cls.Id, world.Shifts[0].Id));
    }
}
