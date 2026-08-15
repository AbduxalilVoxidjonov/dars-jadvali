using DarsJadvali.Desktop.Services.Timetable;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// aSc: "Undo / Redo — oxirgi 100 amal" (03-asc-features-ux.md §4.4).
/// Tarix mantiqi UI'dan mustaqil — shuning uchun XAML'siz sinovdan o'tadi.
/// </summary>
public sealed class CommandHistoryTests
{
    [Fact]
    public void Standart_chegara_100_qadam()
    {
        var history = new CommandHistory();
        Assert.Equal(100, history.Limit);
    }

    [Fact]
    public void Chegaradan_oshganda_eng_eski_qadam_unutiladi()
    {
        var history = new CommandHistory();
        var log = new List<int>();

        // 150 ta amal — faqat oxirgi 100 tasi tarixda qolishi kerak.
        for (var i = 0; i < 150; i++)
        {
            history.Execute(new FakeCommand(i, log));
        }

        Assert.Equal(100, history.UndoCount);

        log.Clear();

        while (history.Undo())
        {
        }

        Assert.Equal(100, log.Count);

        // Eng eskisi 50-amal bo'lishi kerak (0..49 tashlab yuborilgan).
        Assert.Equal(149, log[0]);
        Assert.Equal(50, log[^1]);
    }

    [Fact]
    public void Undo_va_Redo_aynan_avvalgi_holatni_tiklaydi()
    {
        var history = new CommandHistory();
        var state = new Box();

        history.Execute(new AddCommand(state, 5));
        history.Execute(new AddCommand(state, 7));
        history.Execute(new AddCommand(state, 11));

        Assert.Equal(23, state.Value);

        Assert.True(history.Undo());
        Assert.Equal(12, state.Value);

        Assert.True(history.Undo());
        Assert.Equal(5, state.Value);

        Assert.True(history.Redo());
        Assert.Equal(12, state.Value);

        Assert.True(history.Redo());
        Assert.Equal(23, state.Value);

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Yangi_amal_redo_stekini_tozalaydi()
    {
        var history = new CommandHistory();
        var state = new Box();

        history.Execute(new AddCommand(state, 1));
        history.Execute(new AddCommand(state, 2));

        Assert.True(history.Undo());
        Assert.True(history.CanRedo);
        Assert.Equal(1, history.RedoCount);

        // Yangi amal — tarix shoxlanmasligi uchun "qaytarish" yo'li o'chadi.
        history.Execute(new AddCommand(state, 100));

        Assert.False(history.CanRedo);
        Assert.Equal(0, history.RedoCount);
        Assert.Equal(101, state.Value);
    }

    [Fact]
    public void Bosh_tarixda_Undo_va_Redo_hech_narsa_qilmaydi()
    {
        var history = new CommandHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.False(history.Undo());
        Assert.False(history.Redo());
        Assert.Null(history.NextUndoTitle);
        Assert.Null(history.NextRedoTitle);
    }

    [Fact]
    public void Chegara_moslashtirilishi_mumkin()
    {
        var history = new CommandHistory(3);
        var log = new List<int>();

        for (var i = 0; i < 10; i++)
        {
            history.Execute(new FakeCommand(i, log));
        }

        Assert.Equal(3, history.UndoCount);
    }

    [Fact]
    public void Nolinchi_chegara_qabul_qilinmaydi()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CommandHistory(0));

    [Fact]
    public void Birlashtirilgan_komanda_bitta_qadam_hisoblanadi()
    {
        var history = new CommandHistory();
        var state = new Box();

        var composite = new CompositeCommand("Uchtasi birga", new IUndoableCommand[]
        {
            new AddCommand(state, 1),
            new AddCommand(state, 2),
            new AddCommand(state, 4),
        });

        history.Execute(composite);

        Assert.Equal(7, state.Value);
        Assert.Equal(1, history.UndoCount);

        history.Undo();

        Assert.Equal(0, state.Value);
        Assert.Equal(0, history.UndoCount);
    }

    private sealed class Box
    {
        public int Value { get; set; }
    }

    private sealed class AddCommand : IUndoableCommand
    {
        private readonly Box _box;
        private readonly int _delta;

        public AddCommand(Box box, int delta)
        {
            _box = box;
            _delta = delta;
        }

        public string Title => "+" + _delta;

        public void Execute() => _box.Value += _delta;

        public void Undo() => _box.Value -= _delta;
    }

    private sealed class FakeCommand : IUndoableCommand
    {
        private readonly int _id;
        private readonly List<int> _log;

        public FakeCommand(int id, List<int> log)
        {
            _id = id;
            _log = log;
        }

        public string Title => "Amal " + _id;

        public void Execute()
        {
        }

        public void Undo() => _log.Add(_id);
    }
}
