namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// <see cref="ICommandHistory"/> ning standart implementatsiyasi — <b>100 qadam</b>.
/// </summary>
/// <remarks>
/// Chegaradan oshganda eng eski amal tashlab yuboriladi (aSc ham shunday ishlaydi:
/// "Undo / Redo — oxirgi 100 amal"). Yangi amal bajarilganda <c>redo</c> steki tozalanadi —
/// aks holda tarix shoxlanib ketardi va "qaytarish" boshqa tarmoqqa olib borardi.
/// </remarks>
public sealed class CommandHistory : ICommandHistory
{
    /// <summary>Standart chegara — aSc bilan bir xil.</summary>
    public const int DefaultLimit = 100;

    private readonly LinkedList<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    /// <summary>Yangi tarix yaratadi.</summary>
    /// <param name="limit">Qadamlar chegarasi (standart — 100).</param>
    public CommandHistory(int limit = DefaultLimit)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Tarix chegarasi 1 dan kichik bo'lolmaydi.");
        }

        Limit = limit;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public int Limit { get; }

    /// <inheritdoc />
    public bool CanUndo => _undo.Count > 0;

    /// <inheritdoc />
    public bool CanRedo => _redo.Count > 0;

    /// <inheritdoc />
    public int UndoCount => _undo.Count;

    /// <inheritdoc />
    public int RedoCount => _redo.Count;

    /// <inheritdoc />
    public string? NextUndoTitle => _undo.Last?.Value.Title;

    /// <inheritdoc />
    public string? NextRedoTitle => _redo.Count > 0 ? _redo.Peek().Title : null;

    /// <inheritdoc />
    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();

        _undo.AddLast(command);

        // Chegaradan oshganda eng eski qadam unutiladi.
        while (_undo.Count > Limit)
        {
            _undo.RemoveFirst();
        }

        // Yangi amal — eski "qaytarish" yo'li endi mavjud emas.
        _redo.Clear();

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public bool Undo()
    {
        var node = _undo.Last;
        if (node is null)
        {
            return false;
        }

        _undo.RemoveLast();
        node.Value.Undo();
        _redo.Push(node.Value);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var command = _redo.Pop();
        command.Execute();
        _undo.AddLast(command);

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
