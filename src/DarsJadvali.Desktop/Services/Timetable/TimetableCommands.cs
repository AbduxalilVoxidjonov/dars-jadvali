using DarsJadvali.Desktop.Models;

namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// Kartani ko'chirish/qo'yish/olib qo'yish — bitta komanda bilan.
/// </summary>
/// <remarks>
/// <para><c>from = null</c> — joylashtirilmaganlar panelidan qo'yish.</para>
/// <para><c>to = null</c> — jadvaldan panelga qaytarish.</para>
/// <para>Juft dars yaxlit ko'chadi: <see cref="TimetableCard.Length"/> ni board hisobga oladi.</para>
/// </remarks>
public sealed class MoveCardCommand : IUndoableCommand
{
    private readonly TimetableBoard _board;
    private readonly TimetableCard _card;
    private readonly SlotPosition? _from;
    private readonly SlotPosition? _to;

    /// <summary>Ko'chirish komandasini yaratadi.</summary>
    /// <param name="board">Jadval taxtasi.</param>
    /// <param name="card">Ko'chiriladigan karta.</param>
    /// <param name="to">Yangi pozitsiya (<c>null</c> — panelga qaytarish).</param>
    public MoveCardCommand(TimetableBoard board, TimetableCard card, SlotPosition? to)
    {
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _from = card.Position;
        _to = to;

        Title = (_from, _to) switch
        {
            (null, not null) => $"«{card.SubjectName}» kartasini qo'yish",
            (not null, null) => $"«{card.SubjectName}» kartasini olib qo'yish",
            _ => $"«{card.SubjectName}» kartasini ko'chirish",
        };
    }

    /// <inheritdoc />
    public string Title { get; }

    /// <summary>Ko'chirilayotgan karta.</summary>
    public TimetableCard Card => _card;

    /// <inheritdoc />
    public void Execute() => _board.MoveCard(_card, _to);

    /// <inheritdoc />
    public void Undo() => _board.MoveCard(_card, _from);
}

/// <summary>Kartani jadvaldan butunlay o'chiradi.</summary>
public sealed class RemoveCardCommand : IUndoableCommand
{
    private readonly TimetableBoard _board;
    private readonly TimetableCard _card;

    /// <summary>O'chirish komandasini yaratadi.</summary>
    public RemoveCardCommand(TimetableBoard board, TimetableCard card)
    {
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _card = card ?? throw new ArgumentNullException(nameof(card));
        Title = $"«{card.SubjectName}» kartasini o'chirish";
    }

    /// <inheritdoc />
    public string Title { get; }

    /// <summary>O'chirilayotgan karta.</summary>
    public TimetableCard Card => _card;

    /// <inheritdoc />
    public void Execute() => _board.RemoveCard(_card);

    /// <inheritdoc />
    public void Undo() => _board.AddCard(_card);
}

/// <summary>Kartani qulflaydi yoki qulfdan chiqaradi (aSc §4.5).</summary>
public sealed class SetLockCommand : IUndoableCommand
{
    private readonly TimetableBoard _board;
    private readonly TimetableCard _card;
    private readonly bool _newValue;
    private readonly bool _oldValue;

    /// <summary>Qulflash komandasini yaratadi.</summary>
    public SetLockCommand(TimetableBoard board, TimetableCard card, bool locked)
    {
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _newValue = locked;
        _oldValue = card.IsLocked;
        Title = locked ? $"«{card.SubjectName}» kartasini qulflash" : $"«{card.SubjectName}» qulfini ochish";
    }

    /// <inheritdoc />
    public string Title { get; }

    /// <summary>Qulflanayotgan karta.</summary>
    public TimetableCard Card => _card;

    /// <inheritdoc />
    public void Execute() => _board.SetLock(_card, _newValue);

    /// <inheritdoc />
    public void Undo() => _board.SetLock(_card, _oldValue);
}

/// <summary>
/// Bir nechta amalni <b>bitta undo qadami</b> sifatida birlashtiradi.
/// </summary>
/// <remarks>
/// <c>CTRL</c> bilan guruh kartalarini birga ko'chirish va ommaviy amallar (sinf jadvalini
/// tozalash, bir nechta kartani qulflash) shu orqali bajariladi — foydalanuvchi bitta
/// <c>Ctrl+Z</c> bilan hammasini qaytaradi.
/// </remarks>
public sealed class CompositeCommand : IUndoableCommand
{
    private readonly IReadOnlyList<IUndoableCommand> _commands;

    /// <summary>Birlashtirilgan komanda yaratadi.</summary>
    /// <param name="title">Umumiy nom.</param>
    /// <param name="commands">Ichki komandalar (bajarilish tartibida).</param>
    public CompositeCommand(string title, IReadOnlyList<IUndoableCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            throw new ArgumentException("Birlashtirilgan komanda bo'sh bo'lolmaydi.", nameof(commands));
        }

        Title = title;
        _commands = commands;
    }

    /// <inheritdoc />
    public string Title { get; }

    /// <summary>Ichki komandalar soni.</summary>
    public int Count => _commands.Count;

    /// <summary>Ichki komandalar.</summary>
    public IReadOnlyList<IUndoableCommand> Commands => _commands;

    /// <inheritdoc />
    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        // Teskari tartibda — aks holda oraliq holatlar to'g'ri tiklanmaydi.
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
