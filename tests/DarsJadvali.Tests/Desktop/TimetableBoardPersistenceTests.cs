using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Desktop taxtasining <b>haqiqiy</b> baza bilan uchidan-uchiga tekshiruvi:
/// kartochkalar o'qiladi, qulf bazaga yoziladi, ommaviy ko'chirish bitta
/// tranzaksiyada ketadi.
/// </summary>
/// <remarks>
/// Ma'lumot eski (v1) modeldan <c>LegacyToV2Backfill</c> bilan ko'chiriladi —
/// dastur ham xuddi shu yo'ldan foydalanadi.
/// </remarks>
public sealed class TimetableBoardPersistenceTests
{
    private static async Task<TestDbFactory> SeededAsync()
    {
        var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 7);

        var teacher = db.AddTeacher("Voxidjonov Abduxalil");
        var other = db.AddTeacher("Karimova Nodira");
        var math = db.AddSubject("Matematika");
        var physics = db.AddSubject("Fizika");
        var groupA = db.AddClassGroup("5-A", room: "201");
        var groupB = db.AddClassGroup("5-B");

        db.AddAssignment(teacher, math, groupA, weeklyHours: 4);
        db.AddAssignment(other, physics, groupB, weeklyHours: 3);

        db.EnsureActiveSchedule();
        db.AddEntry(groupA, math, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(groupA, math, teacher, WeekDay.Seshanba, 2);
        db.AddEntry(groupB, physics, other, WeekDay.Chorshanba, 3);

        var backfill = new LegacyToV2Backfill(
            db.Context, db.Get<ICardOccurrenceProjector>());

        await backfill.RunAsync();

        return db;
    }

    [Fact]
    public async Task Kartochkalar_bazadan_oqilib_UI_kartasiga_ogiriladi()
    {
        using var db = await SeededAsync();

        var views = await db.Get<ICardBoardService>().GetCardsAsync();
        Assert.Equal(3, views.Count);

        var teachers = await db.Get<ITeacherService>().GetAllAsync();
        var subjects = await db.Get<ISubjectService>().GetAllAsync();

        var cards = CardViewAdapter.ToCards(views, teachers, subjects);

        Assert.All(cards, c => Assert.True(c.IsPlaced));
        Assert.All(cards, c => Assert.NotNull(c.EntityId));
        Assert.All(cards, c => Assert.True(c.LessonId > 0));
        Assert.All(cards, c => Assert.NotEmpty(c.ClassIds));

        var monday = Assert.Single(cards, c => c.Day == WeekDay.Dushanba);
        Assert.Equal(1, monday.Period);
        Assert.Equal("Matematika", monday.SubjectName);
        Assert.Equal("5-A", monday.ClassName);

        // Butun sinf darsi — guruh nomi bo'sh (kartada ortiqcha yozuv chiqmaydi).
        Assert.Equal(string.Empty, monday.GroupName);
    }

    [Fact]
    public async Task Joylashtirilmagan_darslar_aniq_royxatdan_keladi()
    {
        using var db = await SeededAsync();

        var unplaced = await db.Get<ICardBoardService>().GetUnplacedAsync();

        // Matematika 4 soat, 2 tasi qo'yilgan → 2 qoldi; fizika 3 soat, 1 tasi → 2 qoldi.
        Assert.Equal(2, unplaced.Count);
        Assert.All(unplaced, u => Assert.Equal(2, u.RemainingPeriods));

        var teachers = await db.Get<ITeacherService>().GetAllAsync();
        var subjects = await db.Get<ISubjectService>().GetAllAsync();

        var cards = CardViewAdapter.ToUnplacedCards(unplaced, teachers, subjects, 1_000_000);

        Assert.Equal(4, cards.Count);
        Assert.All(cards, c => Assert.Null(c.EntityId));
    }

    [Fact]
    public async Task Qulf_bazada_saqlanadi_va_qayta_oqilganda_qoladi()
    {
        using var db = await SeededAsync();

        var board = db.Get<ICardBoardService>();
        var first = (await board.GetCardsAsync()).First();

        Assert.False(first.IsLocked);
        Assert.True(await board.SetLockAsync(first.CardId, true));

        // Yangi skop = yangi DbContext: qiymat haqiqatan bazadan o'qiladi.
        var reread = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync();
        Assert.True(reread.Single(c => c.CardId == first.CardId).IsLocked);

        var teachers = await db.Get<ITeacherService>().GetAllAsync();
        var subjects = await db.Get<ISubjectService>().GetAllAsync();
        var card = CardViewAdapter.ToCards(reread, teachers, subjects)
            .Single(c => c.EntityId == first.CardId);

        Assert.True(card.IsLocked);
    }

    [Fact]
    public async Task Ommaviy_kochirish_bitta_tranzaksiyada_yoziladi()
    {
        using var db = await SeededAsync();

        var scheduleId = await db.Get<IScheduleSetService>().GetActiveIdAsync();
        var boardService = db.Get<ICardBoardService>();

        var views = await boardService.GetCardsAsync(scheduleId);
        var teachers = await db.Get<ITeacherService>().GetAllAsync();
        var subjects = await db.Get<ISubjectService>().GetAllAsync();

        var periodIdByNumber = await db.Context.Periods
            .AsNoTracking()
            .Where(p => !p.IsBreak)
            .ToDictionaryAsync(p => p.PeriodNo, p => p.Id);

        var snapshot = await db.Get<DarsJadvali.Application.Validation.IScheduleSnapshotProvider>()
            .LoadAsync(scheduleId);

        var rules = TimetableRuleSet.FromSnapshot(
            snapshot, periodIdByNumber.Keys.OrderBy(n => n).ToList());

        var board = new TimetableBoard();
        var cards = CardViewAdapter.ToCards(views, teachers, subjects).ToList();
        board.Load(cards, rules);
        board.ClearDirty();

        // Ikki kartani BITTA komandada (CTRL guruh ko'chishi kabi) ko'chiramiz.
        var movable = cards.Where(c => c.Day == WeekDay.Dushanba || c.Day == WeekDay.Seshanba).ToList();
        Assert.Equal(2, movable.Count);

        var history = new CommandHistory();
        history.Execute(new CompositeCommand("2 ta karta", new IUndoableCommand[]
        {
            new MoveCardCommand(board, movable[0], new SlotPosition(WeekDay.Juma, 6)),
            new MoveCardCommand(board, movable[1], new SlotPosition(WeekDay.Juma, 7)),
        }));

        var writer = new TimetableBoardWriter(boardService);

        var result = await writer.SaveAsync(board, new BoardWriteContext(scheduleId, periodIdByNumber));

        Assert.Equal(2, result.MovedCards);
        Assert.False(result.HasRejections);

        var after = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync(scheduleId);
        Assert.Equal(2, after.Count(c => c.DayNo == DayNumbering.ToDayNo(WeekDay.Juma)));
    }

    /// <summary>
    /// Kartani panelga qaytarish HAQIQIY bazada faqat o'sha kartochkani o'chiradi:
    /// qolgan kartochkalarning <c>Card.Id</c> lari o'zgarmaydi (undo tarixi shu tufayli tirik qoladi).
    /// </summary>
    [Fact]
    public async Task Panelga_qaytarish_faqat_bitta_kartochkani_ochiradi_va_Idlar_saqlanadi()
    {
        using var db = await SeededAsync();

        var scheduleId = await db.Get<IScheduleSetService>().GetActiveIdAsync();
        var boardService = db.Get<ICardBoardService>();

        var views = await boardService.GetCardsAsync(scheduleId);
        var teachers = await db.Get<ITeacherService>().GetAllAsync();
        var subjects = await db.Get<ISubjectService>().GetAllAsync();

        var periodIdByNumber = await db.Context.Periods
            .AsNoTracking()
            .Where(p => !p.IsBreak)
            .ToDictionaryAsync(p => p.PeriodNo, p => p.Id);

        var snapshot = await db.Get<DarsJadvali.Application.Validation.IScheduleSnapshotProvider>()
            .LoadAsync(scheduleId);

        var rules = TimetableRuleSet.FromSnapshot(
            snapshot, periodIdByNumber.Keys.OrderBy(n => n).ToList());

        var board = new TimetableBoard();
        var cards = CardViewAdapter.ToCards(views, teachers, subjects).ToList();
        board.Load(cards, rules);
        board.ClearDirty();

        var removed = cards.First(c => c.IsPlaced);
        var removedId = removed.EntityId!.Value;
        var survivors = cards.Where(c => c.IsPlaced && c.EntityId != removedId)
            .Select(c => c.EntityId!.Value)
            .OrderBy(x => x)
            .ToList();

        var history = new CommandHistory();
        history.Execute(new MoveCardCommand(board, removed, null));

        var writer = new TimetableBoardWriter(boardService);
        var result = await writer.SaveAsync(board, new BoardWriteContext(scheduleId, periodIdByNumber));

        Assert.Equal(1, result.DeletedCards);
        Assert.False(result.HasRejections);

        // Jadval qayta yozilmadi — demak taxta ham, undo tarixi ham tashlanmaydi.
        Assert.False(result.NeedsReload);
        Assert.Equal(1, history.UndoCount);

        var after = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync(scheduleId);

        Assert.DoesNotContain(after, c => c.CardId == removedId);
        Assert.Equal(survivors, after.Select(c => c.CardId).OrderBy(x => x));

        // Bandlik qatorlari ham faqat o'chirilgan kartochka uchun yo'qoldi.
        Assert.Empty(await db.Context.CardOccurrences.AsNoTracking()
            .Where(o => o.CardId == removedId)
            .ToListAsync());
    }

    /// <summary>
    /// Undo kartani jadvalga qaytaradi: yangi kartochka nuqta API bilan yaratiladi va
    /// uning Id si darrov taxtaga biriktiriladi (qayta yuklash yo'q).
    /// </summary>
    [Fact]
    public async Task Undo_kartani_qaytaradi_va_yangi_Id_taxtaga_yoziladi()
    {
        using var db = await SeededAsync();

        var scheduleId = await db.Get<IScheduleSetService>().GetActiveIdAsync();
        var boardService = db.Get<ICardBoardService>();

        var views = await boardService.GetCardsAsync(scheduleId);
        var teachers = await db.Get<ITeacherService>().GetAllAsync();
        var subjects = await db.Get<ISubjectService>().GetAllAsync();

        var periodIdByNumber = await db.Context.Periods
            .AsNoTracking()
            .Where(p => !p.IsBreak)
            .ToDictionaryAsync(p => p.PeriodNo, p => p.Id);

        var snapshot = await db.Get<DarsJadvali.Application.Validation.IScheduleSnapshotProvider>()
            .LoadAsync(scheduleId);

        var rules = TimetableRuleSet.FromSnapshot(
            snapshot, periodIdByNumber.Keys.OrderBy(n => n).ToList());

        var board = new TimetableBoard();
        var cards = CardViewAdapter.ToCards(views, teachers, subjects).ToList();
        board.Load(cards, rules);
        board.ClearDirty();

        var card = cards.First(c => c.IsPlaced);
        var context = new BoardWriteContext(scheduleId, periodIdByNumber);
        var writer = new TimetableBoardWriter(boardService);

        var history = new CommandHistory();
        history.Execute(new MoveCardCommand(board, card, null));

        await writer.SaveAsync(board, context);
        board.ClearDirty();
        Assert.Null(card.EntityId);

        // Undo — karta o'z joyiga qaytadi va bazaga YANGI kartochka bo'lib yoziladi.
        history.Undo();
        var result = await writer.SaveAsync(board, context);

        Assert.Equal(1, result.CreatedCards);
        Assert.False(result.HasRejections);
        Assert.NotNull(card.EntityId);

        var after = await db.GetFromNewScope<ICardBoardService>().GetCardsAsync(scheduleId);
        Assert.Equal(cards.Count(c => c.IsPlaced), after.Count);
        Assert.Contains(after, c => c.CardId == card.EntityId!.Value);
    }

    [Fact]
    public async Task Smenalar_va_uzluksiz_soat_raqamlari_bazadan_keladi()
    {
        using var db = await SeededAsync();

        var scheduleId = await db.Get<IScheduleSetService>().GetActiveIdAsync();
        var input = await db.Get<ISchedulingStore>().LoadAsync(scheduleId);

        // Backfill maktabda ikki smena yaratadi.
        Assert.Equal(2, input.Shifts.Count);
        Assert.Equal(new[] { 1, 2 }, input.Shifts.OrderBy(s => s.ShiftNo).Select(s => s.ShiftNo));

        // Soat raqamlari o'quv yili ichida uzluksiz va takrorlanmaydi.
        var numbers = input.Periods.Where(p => !p.IsBreak).Select(p => p.PeriodNo).OrderBy(n => n).ToList();
        Assert.Equal(numbers.Distinct().Count(), numbers.Count);
        Assert.All(input.Classes, c => Assert.NotNull(c.ShiftId));
    }
}
