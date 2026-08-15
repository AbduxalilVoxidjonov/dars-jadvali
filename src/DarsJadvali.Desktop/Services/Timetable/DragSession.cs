using DarsJadvali.Desktop.Models;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// aSc'dagi <b>"karta qo'lda" (card-in-hand)</b> modeli — HTML5 drag-drop emas,
/// balki <i>bosib olish → kursorni yurgizish → bosib qo'yish</i>
/// (<c>03-asc-features-ux.md</c> §4.1).
/// </summary>
/// <remarks>
/// <para>
/// Bu sinf Avalonia'ga bog'liq emas: sichqoncha/klaviatura hodisalari View'da tutiladi,
/// mantiq esa shu yerda — shuning uchun to'liq sinovdan o'tkaziladi.
/// </para>
/// <para>
/// Baholash har kursor harakatida chaqiriladi, lekin faqat xotiradagi
/// <see cref="TimetableBoard"/> ustida — bazaga murojaat yo'q.
/// </para>
/// </remarks>
public sealed class DragSession
{
    private readonly TimetableBoard _board;
    private readonly List<TimetableCard> _inHand = new();
    private readonly HashSet<TimetableCard> _inHandSet = new();

    /// <summary>Yangi sessiya yaratadi.</summary>
    /// <param name="board">Jadval taxtasi.</param>
    public DragSession(TimetableBoard board)
    {
        _board = board ?? throw new ArgumentNullException(nameof(board));
    }

    /// <summary>Sessiya holati o'zgarganda ko'tariladi.</summary>
    public event EventHandler? Changed;

    /// <summary>Qo'lda karta bormi.</summary>
    public bool IsActive => _inHand.Count > 0;

    /// <summary>Asosiy (bosilgan) karta.</summary>
    public TimetableCard? PrimaryCard => _inHand.Count > 0 ? _inHand[0] : null;

    /// <summary>Qo'ldagi barcha kartalar (CTRL rejimida — butun guruh).</summary>
    public IReadOnlyList<TimetableCard> CardsInHand => _inHand;

    /// <summary>CTRL rejimi: bog'liq kartalar birga ko'chirilmoqda.</summary>
    public bool IsGroupMove { get; private set; }

    /// <summary>SHIFT bosilgan — mumkin pozitsiyalar yoritiladi.</summary>
    public bool IsHighlighting { get; private set; }

    /// <summary>Kursor ostidagi pozitsiya.</summary>
    public SlotPosition? HoverPosition { get; private set; }

    /// <summary>Kursor ostidagi pozitsiyaning bahosi (jonli fikr-mulohaza).</summary>
    public PlacementEvaluation? HoverEvaluation { get; private set; }

    /// <summary>SHIFT bosilganda yoritiladigan pozitsiyalar.</summary>
    public IReadOnlyList<SlotPosition> HighlightedPositions { get; private set; } = Array.Empty<SlotPosition>();

    /// <summary>
    /// Kartani "qo'lga oladi". Qulflangan karta olinmaydi (aSc §4.5).
    /// </summary>
    /// <param name="card">Bosilgan karta.</param>
    /// <param name="groupMove">CTRL bosilganmi — bir darsning barcha kartalari birga olinadi.</param>
    /// <returns>Karta qo'lga olindimi.</returns>
    public bool TryPickUp(TimetableCard card, bool groupMove = false)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.IsLocked)
        {
            return false;
        }

        _inHand.Clear();
        _inHandSet.Clear();

        _inHand.Add(card);

        if (groupMove)
        {
            // Bir darsga (LessonKey) tegishli, shu pozitsiyadagi qulflanmagan kartalar birga ketadi.
            foreach (var other in _board.Cards)
            {
                if (ReferenceEquals(other, card) ||
                    other.IsLocked ||
                    string.IsNullOrWhiteSpace(card.LessonKey) ||
                    !string.Equals(other.LessonKey, card.LessonKey, StringComparison.Ordinal) ||
                    other.Position != card.Position)
                {
                    continue;
                }

                _inHand.Add(other);
            }
        }

        // Qulflangan karta guruhda bo'lsa — butun guruh ko'chmaydi (yaxlitlik buzilmasin).
        if (groupMove && HasLockedSibling(card))
        {
            _inHand.Clear();
            return false;
        }

        IsGroupMove = groupMove && _inHand.Count > 1;

        foreach (var item in _inHand)
        {
            _inHandSet.Add(item);
            item.State = TimetableCardState.InHand;
        }

        RefreshHighlight();
        Raise();
        return true;
    }

    /// <summary>Kursor yangi katak ustiga kelganda — pozitsiyani jonli baholaydi.</summary>
    public void Hover(WeekDay day, int period)
    {
        if (!IsActive)
        {
            return;
        }

        HoverPosition = new SlotPosition(day, period);
        HoverEvaluation = _board.Evaluate(PrimaryCard!, day, period, _inHandSet);
        Raise();
    }

    /// <summary>Kursor to'rdan chiqib ketdi.</summary>
    public void ClearHover()
    {
        if (HoverPosition is null)
        {
            return;
        }

        HoverPosition = null;
        HoverEvaluation = null;
        Raise();
    }

    /// <summary>
    /// SHIFT bosilganda yoritish uchun tanlangan karta.
    /// aSc'da SHIFT <b>qo'lda bo'lmagan</b>, kursor ostidagi karta uchun ham ishlaydi (§4.2).
    /// </summary>
    public TimetableCard? HighlightCard { get; private set; }

    /// <summary>SHIFT holatini o'zgartiradi — mumkin pozitsiyalar yoritilishi shunga bog'liq.</summary>
    public void SetHighlighting(bool value)
    {
        if (IsHighlighting == value)
        {
            return;
        }

        IsHighlighting = value;
        RefreshHighlight();
        Raise();
    }

    /// <summary>
    /// Kursor ostidagi kartani yoritish nishoni qilib belgilaydi (qo'lda karta bo'lmaganda).
    /// </summary>
    public void SetHighlightCard(TimetableCard? card)
    {
        if (ReferenceEquals(HighlightCard, card))
        {
            return;
        }

        HighlightCard = card;
        RefreshHighlight();
        Raise();
    }

    /// <summary>Pozitsiya SHIFT yoritishiga tushadimi.</summary>
    public bool IsHighlighted(WeekDay day, int period)
        => IsHighlighting && HighlightedPositions.Contains(new SlotPosition(day, period));

    /// <summary>
    /// Kartani (yoki butun guruhni) berilgan pozitsiyaga qo'yish komandasini yasaydi.
    /// Pozitsiya taqiqlangan bo'lsa <c>null</c> qaytaradi — hech narsa o'zgarmaydi.
    /// </summary>
    public IUndoableCommand? BuildDropCommand(WeekDay day, int period)
    {
        if (!IsActive)
        {
            return null;
        }

        var evaluation = _board.Evaluate(PrimaryCard!, day, period, _inHandSet);
        if (!evaluation.IsAllowed)
        {
            return null;
        }

        var target = new SlotPosition(day, period);
        var commands = _inHand
            .Select(card => (IUndoableCommand)new MoveCardCommand(_board, card, target))
            .ToList();

        return commands.Count == 1
            ? commands[0]
            : new CompositeCommand($"{commands.Count} ta kartani birga ko'chirish", commands);
    }

    /// <summary>Kartani joylashtirilmaganlar paneliga qaytarish komandasi.</summary>
    public IUndoableCommand? BuildReturnCommand()
    {
        if (!IsActive)
        {
            return null;
        }

        var commands = _inHand
            .Where(c => c.IsPlaced)
            .Select(card => (IUndoableCommand)new MoveCardCommand(_board, card, null))
            .ToList();

        return commands.Count switch
        {
            0 => null,
            1 => commands[0],
            _ => new CompositeCommand($"{commands.Count} ta kartani olib qo'yish", commands),
        };
    }

    /// <summary>Sessiyani bekor qiladi (<c>ESC</c> yoki jadvaldan tashqariga bosish).</summary>
    public void Cancel()
    {
        if (!IsActive && HoverPosition is null)
        {
            return;
        }

        foreach (var card in _inHand)
        {
            card.State = TimetableCardState.Normal;
        }

        _inHand.Clear();
        _inHandSet.Clear();
        IsGroupMove = false;
        HoverPosition = null;
        HoverEvaluation = null;
        RefreshHighlight();
        Raise();
    }

    /// <summary>Qo'yish muvaffaqiyatli tugagach sessiyani yopadi.</summary>
    public void Complete() => Cancel();

    private bool HasLockedSibling(TimetableCard card)
        => !string.IsNullOrWhiteSpace(card.LessonKey) &&
           _board.Cards.Any(c =>
               c.IsLocked &&
               c.Position == card.Position &&
               string.Equals(c.LessonKey, card.LessonKey, StringComparison.Ordinal));

    private void RefreshHighlight()
    {
        // Qo'ldagi karta ustun; qo'lda karta bo'lmasa — kursor ostidagi karta yoritiladi.
        var target = PrimaryCard ?? HighlightCard;

        HighlightedPositions = IsHighlighting && target is not null
            ? _board.PossiblePositions(target, _inHandSet)
            : Array.Empty<SlotPosition>();
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
