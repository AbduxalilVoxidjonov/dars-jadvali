using DarsJadvali.Application.Board;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Taxta o'zgarishlarini bazaga yozish: <b>ommaviy</b> (bitta tranzaksiya) va
/// qulfning bazada saqlanishi.
/// </summary>
public sealed class TimetableBoardWriterTests
{
    // ---------------------------------------------------------------------
    // Soxta servislar — chaqiruvlar sonini aniq o'lchash uchun.
    // ---------------------------------------------------------------------

    private sealed class FakeCardBoardService : ICardBoardService
    {
        public List<IReadOnlyList<CardPlacement>> PlaceManyCalls { get; } = new();

        public List<(int CardId, bool IsLocked)> LockCalls { get; } = new();

        public bool NextPlaceApplied { get; set; } = true;

        public Task<IReadOnlyList<CardView>> GetCardsAsync(
            int? scheduleId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CardView>>(Array.Empty<CardView>());

        public Task<IReadOnlyList<UnplacedLessonView>> GetUnplacedAsync(
            int? scheduleId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UnplacedLessonView>>(Array.Empty<UnplacedLessonView>());

        public Task<CardBulkResult> PlaceAsync(
            CardPlacement placement, bool force = false, int? scheduleId = null, CancellationToken ct = default)
            => PlaceManyAsync(new[] { placement }, force, scheduleId, ct);

        public Task<CardBulkResult> PlaceManyAsync(
            IReadOnlyList<CardPlacement> placements,
            bool force = false,
            int? scheduleId = null,
            CancellationToken ct = default)
        {
            PlaceManyCalls.Add(placements);

            var results = placements
                .Select(p => new CardPlacementResult(
                    p.CardId,
                    NextPlaceApplied,
                    NextPlaceApplied
                        ? Array.Empty<DarsJadvali.Application.Validation.Conflict>()
                        : new[]
                        {
                            new DarsJadvali.Application.Validation.Conflict(
                                DarsJadvali.Application.Validation.ConflictSeverity.Error,
                                DarsJadvali.Application.Validation.ConflictCodes.ClassBusy,
                                "Band."),
                        }))
                .ToList();

            return Task.FromResult(new CardBulkResult(NextPlaceApplied, results, placements.Count));
        }

        public Task<bool> SetLockAsync(int cardId, bool isLocked, CancellationToken ct = default)
        {
            LockCalls.Add((cardId, isLocked));
            return Task.FromResult(true);
        }

        /// <summary>O'chirilgan kartochkalar — chaqiruv tartibida.</summary>
        public List<int> DeleteCalls { get; } = new();

        /// <summary>Yaratish so'rovlari — chaqiruv tartibida.</summary>
        public List<CardCreateRequest> CreateCalls { get; } = new();

        /// <summary>Yaratish rad etilsinmi (to'qnashuv yo'lini sinash uchun).</summary>
        public bool NextCreateApplied { get; set; } = true;

        /// <summary>Keyingi <c>CreateCardAsync</c> qaytaradigan Id (har chaqiruvda oshadi).</summary>
        public int NextCardId { get; set; } = 900;

        public Task<bool> DeleteCardAsync(int cardId, CancellationToken ct = default)
        {
            DeleteCalls.Add(cardId);
            return Task.FromResult(true);
        }

        public Task<CardCreateResult> CreateCardAsync(
            CardCreateRequest request, int? scheduleId = null, CancellationToken ct = default)
        {
            CreateCalls.Add(request);

            if (!NextCreateApplied)
            {
                return Task.FromResult(new CardCreateResult(false, 0, new[]
                {
                    new DarsJadvali.Application.Validation.Conflict(
                        DarsJadvali.Application.Validation.ConflictSeverity.Error,
                        DarsJadvali.Application.Validation.ConflictCodes.ClassBusy,
                        "Band."),
                }));
            }

            return Task.FromResult(new CardCreateResult(
                true, NextCardId++, Array.Empty<DarsJadvali.Application.Validation.Conflict>(), 1));
        }
    }

    // ---------------------------------------------------------------------

    private static TimetableRuleSet Rules(int maxPeriod = 8, int dayCount = 5)
    {
        var days = Enumerable.Range(1, dayCount).Select(i => (WeekDay)i).ToList();
        return new TimetableRuleSet(days, days.ToDictionary(d => d, _ => maxPeriod));
    }

    private static TimetableCard Card(int id, int? cardId, WeekDay? day, int? period, int lessonId = 5)
        => new()
        {
            Id = id,
            EntityId = cardId,
            LessonId = lessonId,
            ClassGroupId = 1,
            ClassIds = new[] { 1 },
            SubjectId = 1,
            TeacherIds = new[] { 1 },
            SubjectName = "Matematika",
            TeacherNames = new[] { "Aliyev A." },
            ClassName = "5-A",
            LessonKey = CardViewAdapter.LessonKeyOf(lessonId),
            Day = day,
            Period = period,
            Length = 1,
        };

    private static BoardWriteContext Context()
        => new(1, Enumerable.Range(1, 12).ToDictionary(n => n, n => 4000 + n));

    [Fact]
    public async Task CompositeCommand_bitta_ommaviy_chaqiruv_qiladi()
    {
        var board = new TimetableBoard();

        var a = Card(1, 101, WeekDay.Dushanba, 1, lessonId: 5);
        var b = Card(2, 102, WeekDay.Dushanba, 2, lessonId: 5);
        var c = Card(3, 103, WeekDay.Dushanba, 3, lessonId: 5);

        board.Load(new[] { a, b, c }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();

        // CTRL guruh ko'chishi — uchta karta BITTA undo qadamida ko'chadi.
        history.Execute(new CompositeCommand("3 ta kartani birga ko'chirish", new IUndoableCommand[]
        {
            new MoveCardCommand(board, a, new SlotPosition(WeekDay.Juma, 5)),
            new MoveCardCommand(board, b, new SlotPosition(WeekDay.Juma, 6)),
            new MoveCardCommand(board, c, new SlotPosition(WeekDay.Juma, 7)),
        }));

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        var result = await writer.SaveAsync(board, Context());

        // AYNAN BITTA chaqiruv — ilgari har karta uchun alohida SaveChanges ketardi.
        var call = Assert.Single(cards.PlaceManyCalls);
        Assert.Equal(3, call.Count);
        Assert.Equal(new[] { 101, 102, 103 }, call.Select(p => p.CardId).OrderBy(x => x));
        Assert.Equal(3, result.MovedCards);
        Assert.False(result.HasRejections);
    }

    [Fact]
    public async Task Undo_ham_bitta_ommaviy_chaqiruv_qiladi()
    {
        var board = new TimetableBoard();

        var a = Card(1, 101, WeekDay.Dushanba, 1);
        var b = Card(2, 102, WeekDay.Dushanba, 2);

        board.Load(new[] { a, b }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();
        history.Execute(new CompositeCommand("2 ta karta", new IUndoableCommand[]
        {
            new MoveCardCommand(board, a, new SlotPosition(WeekDay.Juma, 5)),
            new MoveCardCommand(board, b, new SlotPosition(WeekDay.Juma, 6)),
        }));

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        await writer.SaveAsync(board, Context());
        board.ClearDirty();

        history.Undo();

        await writer.SaveAsync(board, Context());

        Assert.Equal(2, cards.PlaceManyCalls.Count);
        Assert.Equal(2, cards.PlaceManyCalls[1].Count);
    }

    [Fact]
    public async Task Qulf_bazaga_yoziladi()
    {
        var board = new TimetableBoard();
        var card = Card(1, 101, WeekDay.Dushanba, 1);

        board.Load(new[] { card }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();
        history.Execute(new SetLockCommand(board, card, true));

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        var result = await writer.SaveAsync(board, Context());

        Assert.Equal((101, true), Assert.Single(cards.LockCalls));
        Assert.Equal(1, result.LockChanges);
    }

    [Fact]
    public async Task Qulfni_ochish_ham_bazaga_yoziladi()
    {
        var board = new TimetableBoard();
        var card = Card(1, 101, WeekDay.Dushanba, 1);
        card.IsLocked = true;

        board.Load(new[] { card }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();
        history.Execute(new SetLockCommand(board, card, false));

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        await writer.SaveAsync(board, Context());

        Assert.Equal((101, false), Assert.Single(cards.LockCalls));

        // Undo qulfni qaytaradi va u ham bazaga yoziladi.
        board.ClearDirty();
        history.Undo();
        await writer.SaveAsync(board, Context());

        Assert.Equal(2, cards.LockCalls.Count);
        Assert.Equal((101, true), cards.LockCalls[1]);
    }

    [Fact]
    public async Task Yangi_karta_yaratish_alohida_yol_bilan_ketadi()
    {
        var board = new TimetableBoard();

        // Kartochkasi yo'q (rejada bor, joylashtirilmagan) dars.
        var fresh = Card(1, null, null, null, lessonId: 42);

        board.Load(new[] { fresh }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();
        history.Execute(new MoveCardCommand(board, fresh, new SlotPosition(WeekDay.Seshanba, 4)));

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        var result = await writer.SaveAsync(board, Context());

        Assert.Empty(cards.PlaceManyCalls);

        // Butun jadval qayta yozilmaydi — AYNAN bitta yaratish so'rovi ketadi.
        var request = Assert.Single(cards.CreateCalls);
        Assert.Equal(42, request.LessonId);
        Assert.Equal(1, request.DayNo);
        Assert.Equal(4004, request.PeriodId);
        Assert.Equal(1, result.CreatedCards);

        // Qaytgan Id darhol taxtaga biriktiriladi — qayta yuklashning hojati yo'q.
        Assert.Equal(900, fresh.EntityId);
        Assert.False(result.NeedsReload);
    }

    [Fact]
    public async Task Kartani_panelga_qaytarish_faqat_ushanga_kartochkani_ochiradi()
    {
        var board = new TimetableBoard();

        var a = Card(1, 101, WeekDay.Dushanba, 1);
        var b = Card(2, 102, WeekDay.Dushanba, 2);

        board.Load(new[] { a, b }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();
        history.Execute(new MoveCardCommand(board, a, null));

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        var result = await writer.SaveAsync(board, Context());

        // Ilgari bu yerda BUTUN jadval qayta yozilardi (DeleteCardsAsync + InsertCardsAsync),
        // barcha Card.Id lar o'zgarardi va undo tarixi tozalanardi. Endi — nuqta o'chirish.
        Assert.Equal(101, Assert.Single(cards.DeleteCalls));
        Assert.Empty(cards.CreateCalls);
        Assert.Equal(1, result.DeletedCards);

        // Boshqa kartochkaga tegilmadi va uning Id si joyida qoldi.
        Assert.Equal(102, b.EntityId);
        Assert.Null(a.EntityId);

        // Eng muhimi: taxta qayta yuklanmaydi → undo tarixi saqlanadi.
        Assert.False(result.NeedsReload);
    }

    [Fact]
    public async Task Panelga_qaytarilgan_karta_saqlangach_undo_tarixi_saqlanadi()
    {
        var board = new TimetableBoard();

        var a = Card(1, 101, WeekDay.Dushanba, 1);
        var b = Card(2, 102, WeekDay.Dushanba, 2);

        board.Load(new[] { a, b }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();
        history.Execute(new MoveCardCommand(board, a, null));
        Assert.Equal(1, history.UndoCount);

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        var result = await writer.SaveAsync(board, Context());
        board.ClearDirty();

        // NeedsReload = false bo'lgani uchun ViewModel LoadCoreAsync ni CHAQIRMAYDI,
        // demak _history.Clear() ham bo'lmaydi — qadam joyida turadi.
        Assert.False(result.NeedsReload);
        Assert.Equal(1, history.UndoCount);
        Assert.True(history.CanUndo);

        // Undo kartani jadvalga qaytaradi va u YANGI kartochka sifatida yoziladi.
        history.Undo();
        await writer.SaveAsync(board, Context());

        var request = Assert.Single(cards.CreateCalls);
        Assert.Equal(5, request.LessonId);
        Assert.Equal(900, a.EntityId);
    }

    [Fact]
    public async Task Rad_etilgan_joylashtirish_hisobotda_korinadi()
    {
        var board = new TimetableBoard();
        var card = Card(1, 101, WeekDay.Dushanba, 1);

        board.Load(new[] { card }, Rules());
        board.ClearDirty();

        var history = new CommandHistory();
        history.Execute(new MoveCardCommand(board, card, new SlotPosition(WeekDay.Juma, 3)));

        var cards = new FakeCardBoardService { NextPlaceApplied = false };
        var writer = new TimetableBoardWriter(cards);

        var result = await writer.SaveAsync(board, Context());

        Assert.True(result.HasRejections);
        Assert.Equal(0, result.MovedCards);
    }

    [Fact]
    public async Task Ozgarish_bolmasa_hech_narsa_yozilmaydi()
    {
        var board = new TimetableBoard();
        board.Load(new[] { Card(1, 101, WeekDay.Dushanba, 1) }, Rules());
        board.ClearDirty();

        var cards = new FakeCardBoardService();
        var writer = new TimetableBoardWriter(cards);

        var result = await writer.SaveAsync(board, Context());

        Assert.Empty(cards.PlaceManyCalls);
        Assert.Empty(cards.LockCalls);
        Assert.Equal(BoardSaveResult.Empty, result);
    }
}
