namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// Qaytarib bo'ladigan amal. Har bir amal o'z holatini o'zi eslab qoladi va
/// <see cref="Undo"/> da aynan avvalgi holatni tiklaydi.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Foydalanuvchiga ko'rsatiladigan nom ("Kartani ko'chirish").</summary>
    string Title { get; }

    /// <summary>Amalni bajaradi (yoki <c>redo</c> da qaytadan bajaradi).</summary>
    void Execute();

    /// <summary>Amalni bekor qiladi — holat <see cref="Execute"/> dan oldingi holga qaytadi.</summary>
    void Undo();
}

/// <summary>
/// Komandalar tarixi — aSc'dagi <b>100 qadamli undo/redo</b> (03-asc-features-ux.md §4.4).
/// </summary>
public interface ICommandHistory
{
    /// <summary>Tarix chegarasi (qadamlar soni).</summary>
    int Limit { get; }

    /// <summary>Bekor qilish mumkinmi.</summary>
    bool CanUndo { get; }

    /// <summary>Qaytarish mumkinmi.</summary>
    bool CanRedo { get; }

    /// <summary>Undo stekidagi amallar soni.</summary>
    int UndoCount { get; }

    /// <summary>Redo stekidagi amallar soni.</summary>
    int RedoCount { get; }

    /// <summary>Keyingi bekor qilinadigan amal nomi.</summary>
    string? NextUndoTitle { get; }

    /// <summary>Keyingi qaytariladigan amal nomi.</summary>
    string? NextRedoTitle { get; }

    /// <summary>Tarix o'zgarganda ko'tariladi.</summary>
    event EventHandler? Changed;

    /// <summary>Amalni bajaradi va tarixga qo'shadi; <c>redo</c> steki tozalanadi.</summary>
    void Execute(IUndoableCommand command);

    /// <summary>Oxirgi amalni bekor qiladi.</summary>
    bool Undo();

    /// <summary>Bekor qilingan amalni qaytaradi.</summary>
    bool Redo();

    /// <summary>Tarixni butunlay tozalaydi.</summary>
    void Clear();
}
