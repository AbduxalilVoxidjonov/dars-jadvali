using DarsJadvali.Application.Board;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Tests.Generation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// <c>V2_07</c> dan keyin xona bandligi TO'LIQ ko'rinadi: <c>CardClassroom</c>
/// bandlik proyeksiyasiga tushadi va jadval to'ri servisi ham uni hisobga oladi.
/// </summary>
/// <remarks>
/// Ilgari xona faqat <c>Card.LegacyRoomNumber</c> matni edi: u <c>CardOccurrence</c> ga
/// umuman tushmasdi, ya'ni "bitta xonada ikki dars" holati na baza, na
/// <see cref="ICardBoardService"/> tomonidan ushlanardi.
/// </remarks>
public class ClassroomOccupancyTests
{
    /// <summary>Xona endi bandlik qatori sifatida yoziladi.</summary>
    [Fact]
    public async Task Tayinlangan_xona_bandlik_qatoriga_tushadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 1);

        var card = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        var room = world.AddClassroom("101-xona");
        world.AssignRoom(card, room);

        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var rows = await world.Context.CardOccurrences
            .Where(o => o.ResourceKind == ResourceKind.Classroom)
            .ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(room.Id, row.ResourceId);
        Assert.Equal(card.Id, row.CardId);
        Assert.Equal(0, row.DayNo);
        Assert.Equal(1, row.PeriodNo);
    }

    /// <summary>Kartochka DTO'si xona Id'sini va nomini haqiqiy manbadan beradi.</summary>
    [Fact]
    public async Task Kartochka_DTO_si_tayinlangan_xonani_koradi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 1);

        var card = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        var room = world.AddClassroom("101-xona");
        world.AssignRoom(card, room);

        var view = Assert.Single(await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id));

        Assert.Equal(new[] { room.Id }, view.ClassroomIds.ToArray());
        Assert.Equal(room.ShortName, view.RoomNumber);
    }

    /// <summary>Xonasiz kartochkada ro'yxat bo'sh — xona ishlatilmaydigan maktab holati.</summary>
    [Fact]
    public async Task Xonasiz_kartochkada_xona_royxati_bosh()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 1);
        world.AddCard(lesson, dayNo: 0, periodNo: 1);

        var view = Assert.Single(await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id));

        Assert.Empty(view.ClassroomIds);
        Assert.Null(view.RoomNumber);
    }

    /// <summary>
    /// <b>Asosiy natija:</b> kartochkani xonasi band bo'lgan slotga ko'chirish
    /// <c>ROOM_BUSY</c> bilan rad etiladi — sinf va o'qituvchi butunlay boshqa bo'lsa ham.
    /// </summary>
    [Fact]
    public async Task Bir_xonaga_ikkinchi_darsni_kochirish_rad_etiladi()
    {
        using var world = new GenerationWorld();
        var room = world.AddClassroom("101-xona");

        var first = world.AddClass("5-A");
        var second = world.AddClass("5-B");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");

        var lessonA = world.AddLesson(world.AddSubject("Matematika", "MAT"), t1, first, periodsPerWeek: 1);
        var lessonB = world.AddLesson(world.AddSubject("Fizika", "FIZ"), t2, second, periodsPerWeek: 1);

        var cardA = world.AddCard(lessonA, dayNo: 0, periodNo: 1);
        var cardB = world.AddCard(lessonB, dayNo: 0, periodNo: 2);

        world.AssignRoom(cardA, room);
        world.AssignRoom(cardB, room);

        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        // Act — B ni A ning soatiga ko'chiramiz.
        var result = await world.Get<ICardBoardService>().PlaceAsync(
            new CardPlacement(cardB.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id),
            scheduleId: world.Schedule.Id);

        // Assert
        Assert.False(result.Applied);
        var conflict = Assert.Single(result.Rejections);
        Assert.Equal(ConflictCodes.RoomBusy, conflict.Code);

        // Baza tegilmagan.
        world.Context.ChangeTracker.Clear();
        Assert.Equal(world.PeriodsByNo[2].Id,
            (await world.Context.Cards.AsNoTracking().SingleAsync(c => c.Id == cardB.Id)).PeriodId);
    }

    /// <summary>
    /// Bandlik qatorlari hali qurilmagan kartochkaning xonasi ham uning RESURSLARI
    /// qatoriga qo'shiladi — aks holda u ko'chirilganda xona to'qnashuvi umuman
    /// hisoblanmasdi.
    /// </summary>
    /// <remarks>
    /// Bu yerda "band" holat proyeksiyadan keladi (u yagona manba), resurslar ro'yxati
    /// esa kartochka DTO'sidan tiklanadi: shuning uchun proyeksiyasiz kartochkani
    /// PROYEKSIYALANGAN kartochkaning xonasiga ko'chirish rad etiladi.
    /// </remarks>
    [Fact]
    public async Task Proyeksiyasiz_kartochka_ham_xona_bandligini_koradi()
    {
        using var world = new GenerationWorld();
        var room = world.AddClassroom("101-xona");

        var first = world.AddClass("5-A");
        var second = world.AddClass("5-B");
        var t1 = world.AddTeacher("Aliyev Vali");
        var t2 = world.AddTeacher("Karimova Nodira");

        var lessonA = world.AddLesson(world.AddSubject("Matematika", "MAT"), t1, first, periodsPerWeek: 1);
        var lessonB = world.AddLesson(world.AddSubject("Fizika", "FIZ"), t2, second, periodsPerWeek: 1);

        var cardA = world.AddCard(lessonA, dayNo: 0, periodNo: 1);
        world.AssignRoom(cardA, room);

        // A proyeksiyalanadi, B esa YO'Q.
        await world.Projector().RebuildForCardAsync(cardA.Id);

        var cardB = world.AddCard(lessonB, dayNo: 0, periodNo: 2);
        world.AssignRoom(cardB, room);

        Assert.Empty(await world.Context.CardOccurrences.Where(o => o.CardId == cardB.Id).ToListAsync());

        var result = await world.Get<ICardBoardService>().PlaceAsync(
            new CardPlacement(cardB.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id),
            scheduleId: world.Schedule.Id);

        Assert.False(result.Applied);
        Assert.Equal(ConflictCodes.RoomBusy, Assert.Single(result.Rejections).Code);
    }

    /// <summary>Xona bo'sh slotga ko'chirish avvalgidek ishlaydi.</summary>
    [Fact]
    public async Task Bosh_xona_slotiga_kochirish_ishlaydi()
    {
        using var world = new GenerationWorld();
        var room = world.AddClassroom("101-xona");

        var cls = world.AddClass("5-A");
        var teacher = world.AddTeacher("Aliyev Vali");
        var lesson = world.AddLesson(world.AddSubject("Matematika", "MAT"), teacher, cls, periodsPerWeek: 1);

        var card = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        world.AssignRoom(card, room);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var result = await world.Get<ICardBoardService>().PlaceAsync(
            new CardPlacement(card.Id, DayNo: 2, PeriodId: world.PeriodsByNo[4].Id),
            scheduleId: world.Schedule.Id);

        Assert.True(result.Applied, string.Join(" | ", result.Rejections.Select(c => c.Message)));

        world.Context.ChangeTracker.Clear();
        var moved = await world.Context.Cards.AsNoTracking().SingleAsync(c => c.Id == card.Id);
        Assert.Equal(2, moved.DayNo);
        Assert.Equal(world.PeriodsByNo[4].Id, moved.PeriodId);

        // Xona bandligi ham ko'chdi.
        var row = Assert.Single(await world.Context.CardOccurrences
            .AsNoTracking()
            .Where(o => o.ResourceKind == ResourceKind.Classroom)
            .ToListAsync());

        Assert.Equal(2, row.DayNo);
        Assert.Equal(4, row.PeriodNo);
    }
}
