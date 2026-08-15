using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Tests.Generation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// Prezentatsiya qatlami (Desktop) so'ragan API bo'shliqlari: bitta kartochkani
/// o'chirish, yangi kartochka yaratish (xona bilan), sinf smenasini o'zgartirish va
/// joylashtirilmagan darsning sinf/guruh Id lari.
/// </summary>
/// <remarks>
/// Bu beshtasigacha Desktop chetlab o'tish yo'llari bilan ishlardi: bitta kartochkani
/// o'chirish uchun BUTUN jadval qayta yozilardi (Id lar o'zgarib, undo tarixi
/// tozalanardi), yangi kartochka uchun <c>ISchedulingStore</c> va bandlik proyektori
/// TO'G'RIDAN-TO'G'RI chaqirilardi, sinf esa NOM bo'yicha tiklanardi.
/// </remarks>
public class CardBoardEditingApiTests
{
    // =====================================================================
    // 1-API: DeleteCardAsync
    // =====================================================================

    /// <summary>
    /// Bitta kartochka o'chganda QOLGANLARINING Id si o'zgarmaydi — undo tarixi
    /// va taxtadagi tanlov saqlanib qoladi.
    /// </summary>
    [Fact]
    public async Task Bitta_kartochka_ochirilganda_qolganlarining_Id_si_ozgarmaydi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");

        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 4);
        var first = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        var second = world.AddCard(lesson, dayNo: 1, periodNo: 2);
        var third = world.AddCard(lesson, dayNo: 2, periodNo: 3);

        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var board = world.Get<ICardBoardService>();

        // Act
        var deleted = await board.DeleteCardAsync(second.Id);

        // Assert
        Assert.True(deleted);

        var remaining = await board.GetCardsAsync(world.Schedule.Id);
        Assert.Equal(new[] { first.Id, third.Id }, remaining.Select(c => c.CardId).OrderBy(x => x).ToArray());

        // Bandlik qatorlari FAQAT o'chgan kartochkadan ketdi.
        Assert.Empty(await world.Context.CardOccurrences.AsNoTracking()
            .Where(o => o.CardId == second.Id).ToListAsync());

        Assert.NotEmpty(await world.Context.CardOccurrences.AsNoTracking()
            .Where(o => o.CardId == first.Id).ToListAsync());
    }

    /// <summary>Yo'q kartochkani o'chirish xato tashlamaydi — <c>false</c> qaytadi.</summary>
    [Fact]
    public async Task Yoq_kartochkani_ochirish_false_qaytaradi()
    {
        using var world = new GenerationWorld();
        Assert.False(await world.Get<ICardBoardService>().DeleteCardAsync(cardId: 12345));
    }

    /// <summary>O'chirilgan kartochkaning xona bog'lanishi ham ketadi.</summary>
    [Fact]
    public async Task Ochirilgan_kartochkaning_xona_boglanishi_ham_ketadi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Karimov Anvar");
        var subject = world.AddSubject("Fizika", "FIZ");
        var cls = world.AddClass("6-B");
        var room = world.AddClassroom("101-xona");

        var lesson = world.AddLesson(subject, teacher, cls);
        var card = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        world.AssignRoom(card, room);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        // Act
        Assert.True(await world.Get<ICardBoardService>().DeleteCardAsync(card.Id));

        // Assert
        Assert.Empty(await world.Context.CardClassrooms.AsNoTracking()
            .Where(r => r.CardId == card.Id).ToListAsync());

        // Xonaning O'ZI ma'lumotnomada qoladi.
        Assert.NotNull(await world.Context.Classrooms.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == room.Id));
    }

    // =====================================================================
    // 2-API: CreateCardAsync
    // =====================================================================

    /// <summary>Yangi kartochka yaratiladi va bandlik qatorlari darrov quriladi.</summary>
    [Fact]
    public async Task Yangi_kartochka_yaratiladi_va_bandligi_quriladi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 4);

        var board = world.Get<ICardBoardService>();
        var periodId = world.PeriodsByNo[2].Id;

        // Act
        var result = await board.CreateCardAsync(
            new CardCreateRequest(lesson.Id, DayNo: 1, PeriodId: periodId, Length: 2, WeeksMask: 1));

        // Assert
        Assert.True(result.Created);
        Assert.True(result.CardId > 0);
        Assert.Empty(result.Conflicts);
        Assert.True(result.OccurrenceRows > 0);

        var view = Assert.Single(await board.GetCardsAsync(world.Schedule.Id));
        Assert.Equal(result.CardId, view.CardId);
        Assert.Equal(lesson.Id, view.LessonId);
        Assert.Equal(1, view.DayNo);
        Assert.Equal(2, view.PeriodNo);
        Assert.Equal(2, view.Length);
        Assert.True(view.IsDouble);

        // Juft dars IKKALA soatni band qiladi.
        var busy = await world.Context.CardOccurrences.AsNoTracking()
            .Where(o => o.CardId == result.CardId && o.ResourceKind == ResourceKind.Teacher)
            .Select(o => o.PeriodNo)
            .ToListAsync();

        Assert.Equal(new[] { 2, 3 }, busy.OrderBy(x => x).ToArray());
    }

    /// <summary>
    /// Band slotga kartochka yaratib bo'lmaydi va bazaga UMUMAN tegilmaydi —
    /// ko'chirish (<c>PlaceManyAsync</c>) bilan bir xil qoida.
    /// </summary>
    [Fact]
    public async Task Band_slotga_kartochka_yaratib_bolmaydi()
    {
        // Arrange — o'qituvchi 1-kun 2-soatda allaqachon band.
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var first = world.AddClass("5-A");
        var second = world.AddClass("5-B");

        var busyLesson = world.AddLesson(subject, teacher, first, periodsPerWeek: 2);
        world.AddCard(busyLesson, dayNo: 1, periodNo: 2);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var newLesson = world.AddLesson(subject, teacher, second, periodsPerWeek: 2);
        var board = world.Get<ICardBoardService>();

        // Act
        var result = await board.CreateCardAsync(
            new CardCreateRequest(
                newLesson.Id, DayNo: 1, PeriodId: world.PeriodsByNo[2].Id, Length: 1, WeeksMask: 1));

        // Assert
        Assert.False(result.Created);
        Assert.Equal(0, result.CardId);
        Assert.Contains(result.Conflicts, c => c.Code == ConflictCodes.TeacherBusy);

        // Bazaga tegilmagan: hamon bitta kartochka.
        Assert.Equal(1, await world.Context.Cards.AsNoTracking().CountAsync());
    }

    /// <summary>Yo'q dars ta'rifi uchun kartochka yaratilmaydi.</summary>
    [Fact]
    public async Task Yoq_dars_uchun_kartochka_yaratilmaydi()
    {
        using var world = new GenerationWorld();

        var result = await world.Get<ICardBoardService>().CreateCardAsync(
            new CardCreateRequest(
                LessonId: 999, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id, Length: 1, WeeksMask: 1));

        Assert.False(result.Created);
        Assert.Single(result.Conflicts);
        Assert.Equal(0, await world.Context.Cards.AsNoTracking().CountAsync());
    }

    /// <summary>
    /// Desktop aynan shu QISQA imzoni chaqiradi — u ham faol jadvalga kartochka
    /// qo'yadi va so'rov varianti bilan bir xil natija beradi.
    /// </summary>
    [Fact]
    public async Task Qisqa_imzo_faol_jadvalga_kartochka_qoyadi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Geografiya", "GEO");
        var cls = world.AddClass("11-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);

        var board = world.Get<ICardBoardService>();

        // Act — jadval Id BERILMAYDI: faol jadval tanlanishi kerak.
        var result = await board.CreateCardAsync(
            lesson.Id, dayNo: 2, periodId: world.PeriodsByNo[4].Id, length: 1, weeksMask: 1);

        // Assert
        Assert.True(result.Created);

        var view = Assert.Single(await board.GetCardsAsync(world.Schedule.Id));
        Assert.Equal(result.CardId, view.CardId);
        Assert.Equal(world.Schedule.Id, view.ScheduleId);
        Assert.Equal(2, view.DayNo);
        Assert.Equal(4, view.PeriodNo);
        Assert.Equal(1, view.Length);
        Assert.False(view.IsLocked);
    }

    // =====================================================================
    // 5-API: xona maydoni (CardWrite / CardCreateRequest)
    // =====================================================================

    /// <summary>
    /// Xonalar ma'lumotnomasi bo'sh maktabda ham xona QO'LDA kiritiladi va
    /// kartochkada ko'rinadi.
    /// </summary>
    [Fact]
    public async Task Yangi_kartochkaga_xona_qolda_kiritiladi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Kimyo", "KIM");
        var cls = world.AddClass("7-A");
        var lesson = world.AddLesson(subject, teacher, cls);

        var board = world.Get<ICardBoardService>();

        // Act
        var result = await board.CreateCardAsync(
            new CardCreateRequest(lesson.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id)
            {
                RoomNumber = "  214-xona  ",
            });

        // Assert
        Assert.True(result.Created);

        var view = Assert.Single(await board.GetCardsAsync(world.Schedule.Id));
        Assert.Equal("214-xona", view.RoomNumber);
    }

    /// <summary>
    /// Ma'lumotnomadagi xona tayinlansa u BANDLIKKA ham tushadi — bir xonaga ikki
    /// dars qo'yib bo'lmaydi.
    /// </summary>
    [Fact]
    public async Task Tayinlangan_xona_bandlikka_tushadi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var first = world.AddTeacher("Aliyev Vali");
        var second = world.AddTeacher("Karimov Anvar");
        var subject = world.AddSubject("Biologiya", "BIO");
        var classA = world.AddClass("8-A");
        var classB = world.AddClass("8-B");
        var room = world.AddClassroom("Laboratoriya");

        var lessonA = world.AddLesson(subject, first, classA);
        var lessonB = world.AddLesson(subject, second, classB);
        var board = world.Get<ICardBoardService>();

        var request = new CardCreateRequest(lessonA.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id)
        {
            ClassroomIds = new[] { room.Id },
        };

        // Act
        var created = await board.CreateCardAsync(request);
        var rejected = await board.CreateCardAsync(
            new CardCreateRequest(lessonB.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id)
            {
                ClassroomIds = new[] { room.Id },
            });

        // Assert
        Assert.True(created.Created);
        Assert.False(rejected.Created);
        Assert.Contains(rejected.Conflicts, c => c.Code == ConflictCodes.RoomBusy);
    }

    // =====================================================================
    // 4-API: UnplacedLessonView da sinf/guruh Id lari
    // =====================================================================

    /// <summary>
    /// Joylashtirilmagan dars sinf va guruh Id sini BERADI — prezentatsiya qatlami
    /// sinfni nom bo'yicha izlashga majbur emas.
    /// </summary>
    [Fact]
    public async Task Joylashtirilmagan_dars_sinf_va_guruh_Id_sini_beradi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Ona tili", "ONA");
        var cls = world.AddClass("9-A");
        var group = world.Group(cls, "1-guruh");
        var lesson = world.AddLesson(subject, teacher, cls, group, periodsPerWeek: 3);

        // Act
        var unplaced = await world.Get<ICardBoardService>().GetUnplacedAsync(world.Schedule.Id);

        // Assert
        var view = Assert.Single(unplaced);
        Assert.Equal(lesson.Id, view.LessonId);
        Assert.Equal(3, view.RemainingPeriods);
        Assert.Equal(new[] { cls.Id }, view.SchoolClassIds.ToArray());
        Assert.Equal(new[] { group.Id }, view.StudentGroupIds.ToArray());
    }

    /// <summary>
    /// Dars FAQAT guruhga bog'langan bo'lsa ham (sinf bog'lanishi yo'q) sinf Id si
    /// guruhdan tiklanadi — nom bo'yicha izlash hech qachon kerak bo'lmaydi.
    /// </summary>
    [Fact]
    public async Task Faqat_guruhga_boglangan_darsda_sinf_Id_si_guruhdan_tiklanadi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Ingliz tili", "ING");
        var cls = world.AddClass("9-B");
        var group = world.Group(cls, "2-guruh");
        var lesson = world.AddLesson(subject, teacher, cls, group, periodsPerWeek: 2);

        // Sinf bog'lanishini olib tashlaymiz — dars faqat guruhga tegishli bo'lib qoladi.
        var link = await world.Context.LessonClasses.FirstAsync(x => x.LessonId == lesson.Id);
        world.Context.LessonClasses.Remove(link);
        await world.Context.SaveChangesAsync();

        // Act
        var view = Assert.Single(await world.Get<ICardBoardService>().GetUnplacedAsync(world.Schedule.Id));

        // Assert
        Assert.Equal(new[] { cls.Id }, view.SchoolClassIds.ToArray());
        Assert.Equal("9-B", view.ClassName);
    }

    // =====================================================================
    // 3-API: sinf smenasini o'zgartirish (IClassShiftService)
    // =====================================================================

    /// <summary>Sinfning smenasi o'zgartiriladi va bazaga saqlanadi.</summary>
    [Fact]
    public async Task Sinf_smenasi_ozgartiriladi()
    {
        // Arrange — ikki smenali dunyo.
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var cls = world.AddClass("5-A", world.Shifts[0]);
        var shifts = world.Get<IClassShiftService>();

        // Act
        var result = await shifts.SetShiftAsync(cls.Id, world.Shifts[1].Id);

        // Assert
        Assert.True(result.Changed);
        Assert.NotEmpty(result.Message);

        var rows = await shifts.GetClassShiftsAsync(world.Schedule.Id);
        var row = Assert.Single(rows, r => r.SchoolClassId == cls.Id);
        Assert.Equal(world.Shifts[1].Id, row.ShiftId);
        Assert.Equal("2-smena", row.ShiftName);

        // Bazaga HAQIQATAN yozildi.
        Assert.Equal(
            world.Shifts[1].Id,
            await world.Context.SchoolClasses.AsNoTracking()
                .Where(c => c.Id == cls.Id).Select(c => c.ShiftId).FirstAsync());
    }

    /// <summary>
    /// Smena tanlagichi "tayinlanmagan" variantidan boshlanadi va o'quv yilining
    /// barcha smenalarini raqam tartibida beradi.
    /// </summary>
    [Fact]
    public async Task Smena_tanlagichi_tayinlanmagan_variantdan_boshlanadi()
    {
        // Arrange
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);

        // Act
        var options = await world.Get<IClassShiftService>().GetShiftsAsync(world.Schedule.Id);

        // Assert
        Assert.Equal(3, options.Count);
        Assert.Null(options[0].ShiftId);
        Assert.Equal(1, options[1].ShiftNo);
        Assert.Equal(2, options[2].ShiftNo);
        Assert.Equal(world.Shifts[0].Id, options[1].ShiftId);
        Assert.Equal(world.Shifts[1].Id, options[2].ShiftId);
    }

    /// <summary>
    /// Sinflar ro'yxati eski <c>ClassGroup.Id</c> ko'prigini ham beradi — sinflar
    /// ekrani hamon eski modelda ishlaydi.
    /// </summary>
    [Fact]
    public async Task Sinflar_royxati_eski_Id_koprigini_beradi()
    {
        // Arrange
        using var world = new GenerationWorld(shiftCount: 2);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        cls.LegacyClassGroupId = 77;
        await world.Context.SaveChangesAsync();

        // Act
        var rows = await world.Get<IClassShiftService>().GetClassShiftsAsync(world.Schedule.Id);

        // Assert
        var row = Assert.Single(rows, r => r.SchoolClassId == cls.Id);
        Assert.Equal(77, row.LegacyClassGroupId);
        Assert.Equal("5-A", row.ClassName);
    }

    /// <summary>
    /// Boshqa o'quv yilining smenasi RAD ETILADI — aks holda sinf hech qachon
    /// ochilmaydigan dars soatlariga bog'lanib qolardi.
    /// </summary>
    [Fact]
    public async Task Boshqa_oquv_yilining_smenasi_rad_etiladi()
    {
        // Arrange
        using var world = new GenerationWorld(shiftCount: 2);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        var otherYear = new Domain.Entities.AcademicYear { Name = "2026–2027", StartYear = 2026 };
        world.Context.AcademicYears.Add(otherYear);
        await world.Context.SaveChangesAsync();

        var foreignShift = new Domain.Entities.Shift
        {
            AcademicYearId = otherYear.Id,
            ShiftNo = 1,
            Name = "Begona smena",
            ShortName = "B",
        };
        world.Context.Shifts.Add(foreignShift);
        await world.Context.SaveChangesAsync();

        // Act
        var result = await world.Get<IClassShiftService>().SetShiftAsync(cls.Id, foreignShift.Id);

        // Assert — sinf o'z smenasida qoldi va sabab tushunarli.
        Assert.False(result.Changed);
        Assert.Contains("o'quv yili", result.Message, StringComparison.Ordinal);

        Assert.Equal(
            world.Shifts[0].Id,
            await world.Context.SchoolClasses.AsNoTracking()
                .Where(c => c.Id == cls.Id).Select(c => c.ShiftId).FirstAsync());
    }

    /// <summary>Yo'q sinfning smenasini o'zgartirib bo'lmaydi.</summary>
    [Fact]
    public async Task Yoq_sinfning_smenasi_ozgartirilmaydi()
    {
        using var world = new GenerationWorld(shiftCount: 2);

        var result = await world.Get<IClassShiftService>()
            .SetShiftAsync(schoolClassId: 4242, shiftId: world.Shifts[1].Id);

        Assert.False(result.Changed);
        Assert.NotEmpty(result.Message);
    }

    /// <summary>Sinfni smenadan chiqarish (<c>null</c>) ham qo'llab-quvvatlanadi.</summary>
    [Fact]
    public async Task Sinf_smenadan_chiqariladi()
    {
        using var world = new GenerationWorld(shiftCount: 2);
        var cls = world.AddClass("5-A", world.Shifts[0]);

        var result = await world.Get<IClassShiftService>().SetShiftAsync(cls.Id, null);

        Assert.True(result.Changed);
        Assert.Null(await world.Context.SchoolClasses.AsNoTracking()
            .Where(c => c.Id == cls.Id).Select(c => c.ShiftId).FirstAsync());
    }

    // =====================================================================
    // CardWrite.RoomNumber — generator yo'li ham xonani yozadi
    // =====================================================================

    /// <summary><c>CardWrite.RoomNumber</c> bazaga yoziladi va DTO'da qaytadi.</summary>
    [Fact]
    public async Task CardWrite_xona_matnini_bazaga_yozadi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Tarix", "TAR");
        var cls = world.AddClass("10-A");
        var lesson = world.AddLesson(subject, teacher, cls);

        var store = world.Store();

        // Act
        var ids = await store.InsertCardsAsync(new[]
        {
            new Application.Scheduling.CardWrite(
                CoreCardId: 0,
                ScheduleId: world.Schedule.Id,
                LessonId: lesson.Id,
                PeriodId: world.PeriodsByNo[1].Id,
                DayNo: 0,
                WeeksMask: 1,
                IsLocked: false,
                ClassroomIds: Array.Empty<int>())
            {
                RoomNumber = "305",
            },
        });

        // Assert
        var view = Assert.Single(await store.LoadCardViewsAsync(world.Schedule.Id));
        Assert.Equal(ids[0], view.CardId);
        Assert.Equal("305", view.RoomNumber);
    }
}
