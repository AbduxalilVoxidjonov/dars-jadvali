using DarsJadvali.Application.Board;
using DarsJadvali.Tests.Generation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// 4-, 5- va 6-API: <see cref="ICardBoardService"/> — joylashtirilmagan darslar ro'yxati,
/// qulfni bazaga saqlash va <c>Card</c>/<c>Lesson</c> asosidagi o'qish DTO'si.
/// </summary>
public class CardBoardServiceTests
{
    /// <summary>
    /// 6-API: DTO haqiqiy maydonlarni beradi — juft dars, hafta maskasi, qulf va
    /// guruh bo'linmasi endi standart qiymat emas.
    /// </summary>
    [Fact]
    public async Task Kartochka_DTO_si_haqiqiy_maydonlarni_beradi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Voxidjonov Abduxalil");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var group = world.Group(cls, "1-guruh");

        var lesson = world.AddLesson(subject, teacher, cls, group, periodsPerWeek: 4, periodsPerCard: 2);
        var card = world.AddCard(lesson, dayNo: 1, periodNo: 3, weeksMask: 0b10, length: 2);
        await world.Get<ICardBoardService>().SetLockAsync(card.Id, true);

        // Act
        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);

        // Assert
        var view = Assert.Single(cards);
        Assert.Equal(card.Id, view.CardId);
        Assert.Equal(lesson.Id, view.LessonId);
        Assert.Equal("Matematika", view.SubjectName);
        Assert.Equal(new[] { "Voxidjonov Abduxalil" }, view.TeacherNames);
        Assert.Equal("5-A", view.ClassName);
        Assert.Equal("1-guruh", view.GroupName);
        Assert.Equal(1, view.DayNo);
        Assert.Equal(3, view.PeriodNo);

        // Aynan shu to'rt maydon eski modelda YO'Q edi:
        Assert.Equal(2, view.Length);
        Assert.True(view.IsDouble);
        Assert.Equal(0b10, view.WeeksMask);
        Assert.True(view.IsLocked);

        // Juft dars ikkala soatni ko'rsatadi.
        Assert.Equal(new[] { 3, 4 }, view.PeriodNumbers.ToArray());
    }

    /// <summary>Butun sinf darsida guruh nomi ko'rsatilmaydi (bo'sh satr).</summary>
    [Fact]
    public async Task Butun_sinf_darsida_guruh_nomi_bosh()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Fizika", "FIZ");
        var cls = world.AddClass("6-B");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);
        world.AddCard(lesson, dayNo: 0, periodNo: 1);

        var view = Assert.Single(await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id));

        Assert.Equal(string.Empty, view.GroupName);
        Assert.Equal("6-B", view.ClassName);
    }

    /// <summary>
    /// 4-API: joylashtirilmagan darslar ANIQ ro'yxati — "me'yor − qo'yilgan" taxmini emas,
    /// kartochka UZUNLIKLARI yig'indisidan hisoblanadi.
    /// </summary>
    [Fact]
    public async Task Joylashtirilmagan_darslar_royxati_aniq_soat_beradi()
    {
        // Arrange — haftasiga 5 soat, qo'yilgani: 2 + 1 = 3 soat.
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var group = world.Group(cls, "1-guruh");
        var lesson = world.AddLesson(subject, teacher, cls, group, periodsPerWeek: 5, periodsPerCard: 2);

        world.AddCard(lesson, dayNo: 0, periodNo: 1, length: 2);
        world.AddCard(lesson, dayNo: 1, periodNo: 1, length: 1);

        // Act
        var unplaced = await world.Get<ICardBoardService>().GetUnplacedAsync(world.Schedule.Id);

        // Assert
        var item = Assert.Single(unplaced);
        Assert.Equal(lesson.Id, item.LessonId);
        Assert.Equal("Matematika", item.SubjectName);
        Assert.Equal("5-A", item.ClassName);
        Assert.Equal("1-guruh", item.GroupName);
        Assert.Equal(new[] { "Aliyev Vali" }, item.TeacherNames);
        Assert.Equal(5, item.PeriodsPerWeek);

        // 2 + 1 = 3 (kartochkalar SONI 2 emas — uzunliklar yig'indisi).
        Assert.Equal(3, item.PlacedPeriods);
        Assert.Equal(2, item.RemainingPeriods);
    }

    /// <summary>To'liq joylashtirilgan dars ro'yxatda ko'rinmaydi.</summary>
    [Fact]
    public async Task Toliq_qoyilgan_dars_royxatda_korinmaydi()
    {
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2, periodsPerCard: 2);
        world.AddCard(lesson, dayNo: 0, periodNo: 1, length: 2);

        Assert.Empty(await world.Get<ICardBoardService>().GetUnplacedAsync(world.Schedule.Id));
    }

    /// <summary>5-API: qulf BAZAGA saqlanadi (dastur qayta ochilganda yo'qolmaydi).</summary>
    [Fact]
    public async Task Qulf_bazaga_saqlanadi()
    {
        // Arrange
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);
        var card = world.AddCard(lesson, dayNo: 0, periodNo: 1);

        var board = world.Get<ICardBoardService>();

        // Act
        Assert.True(await board.SetLockAsync(card.Id, true));

        // Assert — bazadan qayta o'qilganda ham qulflangan.
        world.Context.ChangeTracker.Clear();
        Assert.True(await world.Context.Cards.AsNoTracking()
            .Where(c => c.Id == card.Id).Select(c => c.IsLocked).SingleAsync());

        // Qulfni ochish ham saqlanadi.
        Assert.True(await board.SetLockAsync(card.Id, false));
        world.Context.ChangeTracker.Clear();
        Assert.False(await world.Context.Cards.AsNoTracking()
            .Where(c => c.Id == card.Id).Select(c => c.IsLocked).SingleAsync());

        // Mavjud bo'lmagan kartochka — false.
        Assert.False(await board.SetLockAsync(999_999, true));
    }

    /// <summary>3-API (kartochka varianti): ommaviy ko'chirish bitta tranzaksiyada.</summary>
    [Fact]
    public async Task Ommaviy_kochirish_bitta_tranzaksiyada_bajariladi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);

        var first = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        var second = world.AddCard(lesson, dayNo: 0, periodNo: 2);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var board = world.Get<ICardBoardService>();

        // Act — ikkalasini ham surish (birinchisi ikkinchisining eski o'rniga tushadi).
        var result = await board.PlaceManyAsync(new[]
        {
            new CardPlacement(first.Id, DayNo: 0, PeriodId: world.PeriodsByNo[3].Id),
            new CardPlacement(second.Id, DayNo: 0, PeriodId: world.PeriodsByNo[4].Id),
        });

        // Assert
        Assert.True(result.Applied);
        Assert.All(result.Results, r => Assert.True(r.Placed));

        world.Context.ChangeTracker.Clear();
        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);
        Assert.Equal(new[] { 3, 4 }, cards.Select(c => c.PeriodNo).OrderBy(x => x).ToArray());
    }

    /// <summary>
    /// Ikki kartochka O'RIN ALMASHTIRADI: proyeksiya ikki fazada qayta qurilgani uchun
    /// oraliq holatda unikal indeks noto'g'ri to'smaydi.
    /// </summary>
    [Fact]
    public async Task Ikki_kartochka_orin_almashtira_oladi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);

        var first = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        var second = world.AddCard(lesson, dayNo: 0, periodNo: 2);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        // Act — 1 ↔ 2 almashinuvi.
        var result = await world.Get<ICardBoardService>().PlaceManyAsync(new[]
        {
            new CardPlacement(first.Id, DayNo: 0, PeriodId: world.PeriodsByNo[2].Id),
            new CardPlacement(second.Id, DayNo: 0, PeriodId: world.PeriodsByNo[1].Id),
        });

        // Assert
        Assert.True(result.Applied, string.Join(" | ", result.Rejections.Select(c => c.Message)));

        world.Context.ChangeTracker.Clear();
        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);
        Assert.Equal(2, cards.Single(c => c.CardId == first.Id).PeriodNo);
        Assert.Equal(1, cards.Single(c => c.CardId == second.Id).PeriodNo);
    }

    /// <summary>Band slotga ko'chirish rad etiladi va HECH NARSA o'zgarmaydi.</summary>
    [Fact]
    public async Task Band_slotga_kochirish_rad_etiladi()
    {
        // Arrange
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);

        var first = world.AddCard(lesson, dayNo: 0, periodNo: 1);
        var second = world.AddCard(lesson, dayNo: 0, periodNo: 2);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        // Act — birinchisini ikkinchisining ustiga qo'yish.
        var result = await world.Get<ICardBoardService>().PlaceManyAsync(new[]
        {
            new CardPlacement(first.Id, DayNo: 0, PeriodId: world.PeriodsByNo[2].Id),
        });

        // Assert
        Assert.False(result.Applied);
        Assert.NotEmpty(result.Rejections);

        world.Context.ChangeTracker.Clear();
        var moved = await world.Context.Cards.AsNoTracking().SingleAsync(c => c.Id == first.Id);
        Assert.Equal(world.PeriodsByNo[1].Id, moved.PeriodId);
        Assert.Equal(second.Id, second.Id);
    }

    /// <summary>Qulflangan kartochka ko'chmaydi (force = false).</summary>
    [Fact]
    public async Task Qulflangan_kartochka_kochmaydi()
    {
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);
        var card = world.AddCard(lesson, dayNo: 0, periodNo: 1, isLocked: true);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var board = world.Get<ICardBoardService>();
        var placement = new CardPlacement(card.Id, DayNo: 1, PeriodId: world.PeriodsByNo[2].Id);

        // Qulflangan holda — rad.
        Assert.False((await board.PlaceAsync(placement)).Applied);

        // force = true bilan — ruxsat.
        Assert.True((await board.PlaceAsync(placement, force: true)).Applied);
    }
}
