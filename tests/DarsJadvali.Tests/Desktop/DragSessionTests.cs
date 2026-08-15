using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// aSc'dagi "karta qo'lda" (card-in-hand) modeli: olish → jonli baholash → qo'yish,
/// <c>ESC</c> bilan bekor qilish, <c>SHIFT</c> yoritish, <c>CTRL</c> guruh ko'chishi
/// (03-asc-features-ux.md §4.1, §4.2, §4.4).
/// </summary>
public sealed class DragSessionTests
{
    private static TimetableRuleSet Rules(int maxPeriod = 8, int dayCount = 5)
    {
        var days = Enumerable.Range(1, dayCount).Select(i => (WeekDay)i).ToList();
        return new TimetableRuleSet(days, days.ToDictionary(d => d, _ => maxPeriod));
    }

    private static TimetableCard Card(
        int id,
        int classId = 1,
        int subjectId = 1,
        int teacherId = 1,
        WeekDay? day = null,
        int? period = null,
        int length = 1,
        string group = "",
        string lessonKey = "") => new()
        {
            Id = id,
            ClassGroupId = classId,
            SubjectId = subjectId,
            TeacherIds = new[] { teacherId },
            SubjectName = "Fan" + subjectId,
            TeacherNames = new[] { "O'qituvchi" + teacherId },
            ClassName = "Sinf" + classId,
            GroupName = group,
            LessonKey = string.IsNullOrEmpty(lessonKey)
                ? CardViewAdapter.LessonKeyOf(classId * 1000 + subjectId * 10 + teacherId)
                : lessonKey,
            Day = day,
            Period = period,
            Length = length,
        };

    [Fact]
    public void Kartani_olish_va_qoyish_ishlaydi()
    {
        var board = new TimetableBoard();
        var card = Card(1);
        board.Load(new[] { card }, Rules());

        var drag = new DragSession(board);

        Assert.True(drag.TryPickUp(card));
        Assert.True(drag.IsActive);
        Assert.Equal(TimetableCardState.InHand, card.State);

        var command = drag.BuildDropCommand(WeekDay.Seshanba, 3);
        Assert.NotNull(command);

        drag.Complete();
        command!.Execute();

        Assert.Equal(WeekDay.Seshanba, card.Day);
        Assert.Equal(3, card.Period);
        Assert.False(drag.IsActive);
        Assert.Equal(TimetableCardState.Normal, card.State);
    }

    [Fact]
    public void ESC_kartani_qoldan_qoyib_yuboradi_va_hech_narsa_ozgarmaydi()
    {
        var board = new TimetableBoard();
        var card = Card(1, day: WeekDay.Dushanba, period: 2);
        board.Load(new[] { card }, Rules());

        var drag = new DragSession(board);
        drag.TryPickUp(card);
        drag.Hover(WeekDay.Juma, 4);

        drag.Cancel();

        Assert.False(drag.IsActive);
        Assert.Null(drag.HoverPosition);
        Assert.Equal(WeekDay.Dushanba, card.Day);
        Assert.Equal(2, card.Period);
    }

    [Fact]
    public void Bloklangan_karta_qolga_olinmaydi()
    {
        var board = new TimetableBoard();
        var card = Card(1, day: WeekDay.Dushanba, period: 1);
        card.IsLocked = true;
        board.Load(new[] { card }, Rules());

        var drag = new DragSession(board);

        Assert.False(drag.TryPickUp(card));
        Assert.False(drag.IsActive);
        Assert.Equal(TimetableCardState.Normal, card.State);
    }

    [Fact]
    public void Taqiqlangan_pozitsiyaga_qoyish_komandasi_yasalmaydi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var moving = Card(2, classId: 1, subjectId: 2, teacherId: 2);
        board.Load(new[] { busy, moving }, Rules());

        var drag = new DragSession(board);
        drag.TryPickUp(moving);

        Assert.Null(drag.BuildDropCommand(WeekDay.Dushanba, 1));
    }

    [Fact]
    public void Hover_jonli_baho_beradi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var moving = Card(2, classId: 1, subjectId: 2, teacherId: 2);
        board.Load(new[] { busy, moving }, Rules());

        var drag = new DragSession(board);
        drag.TryPickUp(moving);

        drag.Hover(WeekDay.Dushanba, 1);
        Assert.Equal(PlacementRating.Forbidden, drag.HoverEvaluation!.Rating);

        drag.Hover(WeekDay.Dushanba, 2);
        Assert.Equal(PlacementRating.Preferred, drag.HoverEvaluation!.Rating);
    }

    [Fact]
    public void SHIFT_mumkin_pozitsiyalarni_yoritadi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var moving = Card(2, classId: 1, subjectId: 2, teacherId: 2);
        board.Load(new[] { busy, moving }, Rules(maxPeriod: 3, dayCount: 2));

        var drag = new DragSession(board);
        drag.TryPickUp(moving);

        Assert.Empty(drag.HighlightedPositions);

        drag.SetHighlighting(true);

        Assert.NotEmpty(drag.HighlightedPositions);
        Assert.False(drag.IsHighlighted(WeekDay.Dushanba, 1));
        Assert.True(drag.IsHighlighted(WeekDay.Seshanba, 1));

        drag.SetHighlighting(false);
        Assert.Empty(drag.HighlightedPositions);
    }

    [Fact]
    public void SHIFT_qolda_karta_bolmasa_kursor_ostidagi_karta_uchun_ishlaydi()
    {
        var board = new TimetableBoard();
        var card = Card(1, day: WeekDay.Dushanba, period: 1);
        board.Load(new[] { card }, Rules(maxPeriod: 3, dayCount: 2));

        var drag = new DragSession(board);
        drag.SetHighlightCard(card);
        drag.SetHighlighting(true);

        Assert.False(drag.IsActive);
        Assert.NotEmpty(drag.HighlightedPositions);
    }

    // ================= CTRL: guruh kartalarini birga ko'chirish =================

    [Fact]
    public void CTRL_bilan_bir_darsning_barcha_kartalari_birga_kochadi()
    {
        var board = new TimetableBoard();

        // Bitta dars ikki guruhga bo'lingan — ikkala karta bir pozitsiyada.
        var first = Card(1, classId: 1, subjectId: 1, teacherId: 1,
            day: WeekDay.Dushanba, period: 2, group: "1-guruh", lessonKey: "dars-A");
        var second = Card(2, classId: 1, subjectId: 1, teacherId: 2,
            day: WeekDay.Dushanba, period: 2, group: "2-guruh", lessonKey: "dars-A");
        var other = Card(3, classId: 2, subjectId: 3, teacherId: 3,
            day: WeekDay.Dushanba, period: 2, lessonKey: "dars-B");

        board.Load(new[] { first, second, other }, Rules());

        var drag = new DragSession(board);

        Assert.True(drag.TryPickUp(first, groupMove: true));
        Assert.True(drag.IsGroupMove);
        Assert.Equal(2, drag.CardsInHand.Count);
        Assert.Contains(second, drag.CardsInHand);
        Assert.DoesNotContain(other, drag.CardsInHand);

        var command = drag.BuildDropCommand(WeekDay.Payshanba, 5);
        Assert.NotNull(command);
        Assert.IsType<CompositeCommand>(command);

        var history = new CommandHistory();
        drag.Complete();
        history.Execute(command!);

        Assert.Equal(WeekDay.Payshanba, first.Day);
        Assert.Equal(WeekDay.Payshanba, second.Day);
        Assert.Equal(5, first.Period);
        Assert.Equal(5, second.Period);
        Assert.Equal(WeekDay.Dushanba, other.Day);

        // Bitta Ctrl+Z ikkala kartani ham qaytaradi.
        history.Undo();

        Assert.Equal(WeekDay.Dushanba, first.Day);
        Assert.Equal(WeekDay.Dushanba, second.Day);
        Assert.Equal(2, first.Period);
        Assert.Equal(2, second.Period);
    }

    [Fact]
    public void CTRLsiz_faqat_bosilgan_karta_kochadi()
    {
        var board = new TimetableBoard();
        var first = Card(1, classId: 1, subjectId: 1, teacherId: 1,
            day: WeekDay.Dushanba, period: 2, group: "1-guruh", lessonKey: "dars-A");
        var second = Card(2, classId: 1, subjectId: 1, teacherId: 2,
            day: WeekDay.Dushanba, period: 2, group: "2-guruh", lessonKey: "dars-A");

        board.Load(new[] { first, second }, Rules());

        var drag = new DragSession(board);
        drag.TryPickUp(first);

        Assert.False(drag.IsGroupMove);
        Assert.Single(drag.CardsInHand);
    }

    [Fact]
    public void Guruhda_qulflangan_karta_bolsa_guruh_kochmaydi()
    {
        var board = new TimetableBoard();
        var first = Card(1, classId: 1, subjectId: 1, teacherId: 1,
            day: WeekDay.Dushanba, period: 2, group: "1-guruh", lessonKey: "dars-A");
        var locked = Card(2, classId: 1, subjectId: 1, teacherId: 2,
            day: WeekDay.Dushanba, period: 2, group: "2-guruh", lessonKey: "dars-A");
        locked.IsLocked = true;

        board.Load(new[] { first, locked }, Rules());

        var drag = new DragSession(board);

        Assert.False(drag.TryPickUp(first, groupMove: true));
        Assert.False(drag.IsActive);
    }

    [Fact]
    public void Guruh_kochayotganda_ozaro_toqnashuv_hisobga_olinmaydi()
    {
        var board = new TimetableBoard();
        var first = Card(1, classId: 1, subjectId: 1, teacherId: 1,
            day: WeekDay.Dushanba, period: 2, group: "1-guruh", lessonKey: "dars-A");
        var second = Card(2, classId: 1, subjectId: 1, teacherId: 2,
            day: WeekDay.Dushanba, period: 2, group: "2-guruh", lessonKey: "dars-A");

        board.Load(new[] { first, second }, Rules());

        var drag = new DragSession(board);
        drag.TryPickUp(first, groupMove: true);

        // Ikkinchi karta qo'lda — u to'siq bo'lib hisoblanmasligi kerak.
        drag.Hover(WeekDay.Seshanba, 1);

        Assert.Equal(PlacementRating.Preferred, drag.HoverEvaluation!.Rating);
    }

    [Fact]
    public void Panelga_qaytarish_komandasi_kartani_joylashtirilmaganga_aylantiradi()
    {
        var board = new TimetableBoard();
        var card = Card(1, day: WeekDay.Dushanba, period: 1);
        board.Load(new[] { card }, Rules());

        var drag = new DragSession(board);
        drag.TryPickUp(card);

        var command = drag.BuildReturnCommand();
        Assert.NotNull(command);

        drag.Complete();
        var history = new CommandHistory();
        history.Execute(command!);

        Assert.False(card.IsPlaced);
        Assert.Contains(card, board.UnplacedCards);

        history.Undo();

        Assert.True(card.IsPlaced);
        Assert.Equal(WeekDay.Dushanba, card.Day);
    }
}
