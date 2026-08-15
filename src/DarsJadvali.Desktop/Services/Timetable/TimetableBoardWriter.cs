using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// Xotiradagi taxta o'zgarishini bazaga yozishning natijasi.
/// </summary>
/// <param name="MovedCards">Ko'chirilgan kartochkalar soni.</param>
/// <param name="CreatedCards">Yangi yaratilgan kartochkalar soni.</param>
/// <param name="LockChanges">Bazaga yozilgan qulf o'zgarishlari soni.</param>
/// <param name="NeedsReload">
/// Taxta qayta yuklanishi kerakmi. <b>V2_08 dan keyin bu deyarli har doim <c>false</c>:</b>
/// kartochka o'chirish va yaratish nuqta API'lari (<see cref="ICardBoardService.DeleteCardAsync"/>,
/// <see cref="ICardBoardService.CreateCardAsync"/>) mavjud <c>Card.Id</c> larni
/// o'zgartirmaydi, shuning uchun taxtani ham, <b>undo tarixini</b> ham tashlab
/// yuborishning hojati yo'q.
/// </param>
/// <param name="Rejections">Rad etilgan joylashtirishlarning sabablari.</param>
/// <param name="DeletedCards">Panelga qaytarilgani uchun o'chirilgan kartochkalar soni.</param>
public sealed record BoardSaveResult(
    int MovedCards,
    int CreatedCards,
    int LockChanges,
    bool NeedsReload,
    IReadOnlyList<Conflict> Rejections,
    int DeletedCards = 0)
{
    /// <summary>Hech narsa yozilmadi.</summary>
    public static BoardSaveResult Empty { get; } =
        new(0, 0, 0, false, Array.Empty<Conflict>());

    /// <summary>Rad etilgan joylashtirish bormi.</summary>
    public bool HasRejections => Rejections.Count > 0;
}

/// <summary>
/// Butun jadvalni bitta tranzaksiyada qaytadan yozish (yoki tozalash).
/// </summary>
/// <remarks>
/// <b>Diqqat.</b> Bu yo'l endi <b>faqat ommaviy</b> amallar uchun: "Jadvalni tozalash"
/// va butun jadvalni almashtirish. Bitta kartochkani yaratish/o'chirish uchun
/// <see cref="ICardBoardService.CreateCardAsync"/> va
/// <see cref="ICardBoardService.DeleteCardAsync"/> ishlatiladi — ular mavjud
/// <c>Card.Id</c> larni saqlaydi va shu sababli undo tarixi ham tirik qoladi.
/// </remarks>
public interface IBoardCardRewriter
{
    /// <summary>Yangi kartochkalarni bitta tranzaksiyada yozadi va bandlikni qayta quradi.</summary>
    Task<int> CreateAsync(IReadOnlyList<CardWrite> cards, CancellationToken ct = default);

    /// <summary>
    /// Jadvalning barcha kartochkalarini bitta tranzaksiyada qaytadan yozadi
    /// (kartochkani o'chirish uchun yagona mavjud yo'l).
    /// </summary>
    Task<int> RewriteAsync(int scheduleId, IReadOnlyList<CardWrite> cards, CancellationToken ct = default);
}

/// <summary><see cref="IBoardCardRewriter"/> ning <c>ISchedulingStore</c> ustidagi implementatsiyasi.</summary>
public sealed class BoardCardRewriter : IBoardCardRewriter
{
    private readonly IUnitOfWork _uow;
    private readonly ISchedulingStore _store;
    private readonly ICardOccurrenceProjector _projector;

    /// <summary>Yangi yozuvchi yaratadi.</summary>
    public BoardCardRewriter(IUnitOfWork uow, ISchedulingStore store, ICardOccurrenceProjector projector)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    /// <inheritdoc />
    public Task<int> CreateAsync(IReadOnlyList<CardWrite> cards, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0)
        {
            return Task.FromResult(0);
        }

        return _uow.ExecuteInTransactionAsync(async token =>
        {
            var ids = await _store.InsertCardsAsync(cards, token).ConfigureAwait(false);
            await _projector.RebuildForCardsAsync(ids.ToList(), token).ConfigureAwait(false);
            return ids.Count;
        }, ct);
    }

    /// <inheritdoc />
    public Task<int> RewriteAsync(
        int scheduleId, IReadOnlyList<CardWrite> cards, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cards);

        return _uow.ExecuteInTransactionAsync(async token =>
        {
            await _store.DeleteCardsAsync(scheduleId, keepLocked: false, token).ConfigureAwait(false);

            if (cards.Count == 0)
            {
                return 0;
            }

            var ids = await _store.InsertCardsAsync(cards, token).ConfigureAwait(false);
            await _projector.RebuildForCardsAsync(ids.ToList(), token).ConfigureAwait(false);
            return ids.Count;
        }, ct);
    }
}

/// <summary>
/// Xotiradagi <see cref="TimetableBoard"/> o'zgarishlarini bazaga yozadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ommaviy yozish (00 §6.4).</b> Ilgari har bir "iflos" karta uchun alohida
/// <c>PlaceAsync</c> chaqirilardi: <c>CTRL</c> bilan 6 ta kartani ko'chirish = 6 ta
/// alohida <c>SaveChanges</c> va o'rtada xato chiqsa <b>yarim ko'chirilgan</b> jadval.
/// Endi barcha ko'chirish BITTA <see cref="ICardBoardService.PlaceManyAsync"/> chaqiruviga
/// yig'iladi — u ishni bitta tranzaksiyada bajaradi ("hammasi yoki hech narsa").
/// </para>
/// <para>
/// <b>Qulf</b> alohida <see cref="ICardBoardService.SetLockAsync"/> bilan bazaga yoziladi —
/// ilgari u faqat sessiya ichida yashardi va dastur qayta ochilganda yo'qolardi.
/// </para>
/// <para>
/// <b>Nuqta API'lari (V2_08).</b> Kartani panelga qaytarish endi
/// <see cref="ICardBoardService.DeleteCardAsync"/>, yangi karta qo'yish esa
/// <see cref="ICardBoardService.CreateCardAsync"/> bilan bajariladi. Ilgari ikkala
/// holatda ham BUTUN jadval qayta yozilardi (<c>DeleteCardsAsync</c> +
/// <c>InsertCardsAsync</c>): barcha <c>Card.Id</c> lar o'zgarardi, taxta to'liq qayta
/// yuklanardi va <b>undo tarixi tozalanardi</b>. Endi Id lar joyida qoladi —
/// yaratilgan kartaning yangi Id si to'g'ridan-to'g'ri
/// <see cref="TimetableCard.EntityId"/> ga yoziladi va tarix saqlanadi.
/// </para>
/// </remarks>
public sealed class TimetableBoardWriter
{
    private readonly ICardBoardService _cards;

    /// <summary>Yangi yozuvchi yaratadi.</summary>
    /// <param name="cards">Kartochka servisi.</param>
    public TimetableBoardWriter(ICardBoardService cards)
    {
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    /// <summary>
    /// Taxtadagi "iflos" kartalarni bazaga yozadi.
    /// </summary>
    /// <param name="board">Xotiradagi taxta.</param>
    /// <param name="context">Jadval varianti va dars soati raqamlari xaritasi.</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    public async Task<BoardSaveResult> SaveAsync(
        TimetableBoard board, BoardWriteContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(context);

        var dirty = board.DirtyCardIds
            .Select(board.FindById)
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        if (dirty.Count == 0)
        {
            return BoardSaveResult.Empty;
        }

        // 1) Qulf o'zgarishlari — bazaga yoziladi (sessiya ichida qolib ketmaydi).
        var lockChanges = 0;
        foreach (var card in dirty)
        {
            if (card.EntityId is not { } cardId || !board.IsLockDirty(card.Id))
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();

            if (await _cards.SetLockAsync(cardId, card.IsLocked, ct).ConfigureAwait(false))
            {
                lockChanges++;
            }
        }

        // 2) Ko'chish / o'chirish / yaratish uchun kartalarni ajratamiz.
        var placements = new List<CardPlacement>();
        var creations = new List<TimetableCard>();
        var deletions = new List<TimetableCard>();

        foreach (var card in dirty)
        {
            // Faqat qulfi o'zgargan karta ko'chirilmaydi — u yuqorida yozildi.
            if (!board.IsMoveDirty(card.Id))
            {
                continue;
            }

            if (card.EntityId is { })
            {
                if (!card.IsPlaced)
                {
                    // Panelga qaytarildi — AYNAN SHU kartochka o'chiriladi (butun jadval emas).
                    deletions.Add(card);
                    continue;
                }

                if (CardViewAdapter.ToPlacement(card, context.PeriodIdByNumber) is { } placement)
                {
                    placements.Add(placement);
                }

                continue;
            }

            if (card.IsPlaced)
            {
                creations.Add(card);
            }
        }

        var rejections = new List<Conflict>();

        // 3) O'chirish AVVAL bajariladi: bo'shagan slot shu saqlashning o'zida boshqa
        //    kartaga berilishi mumkin.
        var deleted = 0;
        foreach (var card in deletions)
        {
            ct.ThrowIfCancellationRequested();

            if (await _cards.DeleteCardAsync(card.EntityId!.Value, ct).ConfigureAwait(false))
            {
                // Karta endi bazada yo'q — keyin qayta qo'yilsa YANGI kartochka yaratiladi.
                card.EntityId = null;
                deleted++;
            }
        }

        // 4) Mavjud kartochkalarning ko'chishi — BITTA ommaviy chaqiruv.
        var moved = 0;
        if (placements.Count > 0)
        {
            // force: true — baholash UI tomonda allaqachon bajarilgan (drag paytida).
            var result = await _cards
                .PlaceManyAsync(placements, force: true, context.ScheduleId, ct)
                .ConfigureAwait(false);

            if (result.Applied)
            {
                moved = placements.Count;
            }
            else
            {
                rejections.AddRange(result.Rejections);
            }
        }

        // 5) Yangi kartochkalar — har biri nuqta API bilan; qaytgan Id darhol taxtaga yoziladi.
        var created = 0;
        foreach (var card in creations)
        {
            ct.ThrowIfCancellationRequested();

            if (ToCreateRequest(card, context) is not { } request)
            {
                continue;
            }

            var result = await _cards
                .CreateCardAsync(request, context.ScheduleId, ct)
                .ConfigureAwait(false);

            if (result.Created)
            {
                // Taxta qayta yuklanmaydi: yangi Id joyida biriktiriladi va undo tarixi tirik qoladi.
                card.EntityId = result.CardId;
                created++;
            }
            else
            {
                rejections.AddRange(result.Conflicts);
            }
        }

        return new BoardSaveResult(
            moved, created, lockChanges, NeedsReload: false, rejections, deleted);
    }

    /// <summary>Taxtadagi kartani yaratish so'roviga o'giradi.</summary>
    private static CardCreateRequest? ToCreateRequest(TimetableCard card, BoardWriteContext context)
    {
        if (!card.IsPlaced || card.LessonId <= 0)
        {
            return null;
        }

        if (!context.PeriodIdByNumber.TryGetValue(card.Period!.Value, out var periodId))
        {
            return null;
        }

        return new CardCreateRequest(
            LessonId: card.LessonId,
            DayNo: DayNumbering.ToDayNo(card.Day!.Value),
            PeriodId: periodId,
            Length: Math.Max(1, card.Length),
            WeeksMask: card.WeeksMask)
        {
            IsLocked = card.IsLocked,
        };
    }
}

/// <summary>Yozish uchun kerak bo'ladigan kontekst.</summary>
/// <param name="ScheduleId">Jadval varianti.</param>
/// <param name="PeriodIdByNumber">Dars soati raqami → <c>Period.Id</c>.</param>
public sealed record BoardWriteContext(int ScheduleId, IReadOnlyDictionary<int, int> PeriodIdByNumber);
