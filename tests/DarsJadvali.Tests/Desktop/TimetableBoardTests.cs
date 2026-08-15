using System.Diagnostics;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Drag paytidagi <b>pozitsiya baholash</b> mantiqi — aSc'dagi kulrang/ko'k/yashil
/// (03-asc-features-ux.md §4.1, §4.6). Baza chaqirilmaydi: hammasi xotirada.
/// </summary>
public sealed class TimetableBoardTests
{
    private static TimetableRuleSet Rules(int maxPeriod = 8, int dayCount = 5)
    {
        var days = Enumerable.Range(1, dayCount).Select(i => (WeekDay)i).ToList();
        var max = days.ToDictionary(d => d, _ => maxPeriod);
        return new TimetableRuleSet(days, max);
    }

    private static TimetableCard Card(
        int id,
        int classId = 1,
        int subjectId = 1,
        int teacherId = 1,
        WeekDay? day = null,
        int? period = null,
        int length = 1,
        int weeksMask = TimetableCard.AllWeeks,
        string group = "",
        string? room = null) => new()
        {
            Id = id,
            ClassGroupId = classId,
            SubjectId = subjectId,
            TeacherIds = new[] { teacherId },
            SubjectName = "Fan" + subjectId,
            TeacherNames = new[] { "O'qituvchi" + teacherId },
            ClassName = "Sinf" + classId,
            GroupName = group,
            LessonKey = CardViewAdapter.LessonKeyOf(classId * 1000 + subjectId * 10 + teacherId),
            Day = day,
            Period = period,
            Length = length,
            WeeksMask = weeksMask,
            RoomNumber = room,
        };

    // ================= Uch daraja: ruxsat / ogohlantirish / taqiq =================

    [Fact]
    public void Bosh_katak_yashil_baholanadi()
    {
        var board = new TimetableBoard();
        var card = Card(1);
        board.Load(new[] { card }, Rules());

        var result = board.Evaluate(card, WeekDay.Dushanba, 1);

        Assert.Equal(PlacementRating.Preferred, result.Rating);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void Sinf_band_bolsa_taqiqlanadi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 3);
        var moving = Card(2, classId: 1, subjectId: 2, teacherId: 2);
        board.Load(new[] { busy, moving }, Rules());

        var result = board.Evaluate(moving, WeekDay.Dushanba, 3);

        Assert.Equal(PlacementRating.Forbidden, result.Rating);
        Assert.False(result.IsAllowed);
        Assert.Contains("band", result.ReasonText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Oqituvchi_band_bolsa_taqiqlanadi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, teacherId: 7, day: WeekDay.Seshanba, period: 2);
        var moving = Card(2, classId: 2, teacherId: 7);
        board.Load(new[] { busy, moving }, Rules());

        Assert.Equal(PlacementRating.Forbidden, board.Evaluate(moving, WeekDay.Seshanba, 2).Rating);
    }

    [Fact]
    public void Xona_band_bolsa_taqiqlanadi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, teacherId: 1, day: WeekDay.Chorshanba, period: 4, room: "205");
        var moving = Card(2, classId: 2, teacherId: 2, room: "205");
        board.Load(new[] { busy, moving }, Rules());

        Assert.Equal(PlacementRating.Forbidden, board.Evaluate(moving, WeekDay.Chorshanba, 4).Rating);
    }

    [Fact]
    public void Faol_bolmagan_kun_taqiqlanadi()
    {
        var board = new TimetableBoard();
        var card = Card(1);
        board.Load(new[] { card }, Rules(dayCount: 5));

        // Shanba faol kunlar ro'yxatida yo'q.
        Assert.Equal(PlacementRating.Forbidden, board.Evaluate(card, WeekDay.Shanba, 1).Rating);
    }

    [Fact]
    public void Chegaradan_tashqaridagi_soat_taqiqlanadi()
    {
        var board = new TimetableBoard();
        var card = Card(1);
        board.Load(new[] { card }, Rules(maxPeriod: 6));

        Assert.Equal(PlacementRating.Forbidden, board.Evaluate(card, WeekDay.Dushanba, 7).Rating);
        Assert.Equal(PlacementRating.Forbidden, board.Evaluate(card, WeekDay.Dushanba, 0).Rating);
    }

    [Fact]
    public void Oqituvchi_ishlamaydigan_soat_taqiqlanadi()
    {
        var days = new[] { WeekDay.Dushanba };
        var rules = new TimetableRuleSet(
            days,
            new Dictionary<WeekDay, int> { [WeekDay.Dushanba] = 8 },
            new[] { (TeacherId: 1, Day: WeekDay.Dushanba, Period: 5) });

        var board = new TimetableBoard();
        var card = Card(1, teacherId: 1);
        board.Load(new[] { card }, rules);

        Assert.Equal(PlacementRating.Forbidden, board.Evaluate(card, WeekDay.Dushanba, 5).Rating);
        Assert.Equal(PlacementRating.Preferred, board.Evaluate(card, WeekDay.Dushanba, 4).Rating);
    }

    [Fact]
    public void Fan_kun_ichida_takrorlansa_ogohlantirish_beriladi()
    {
        var board = new TimetableBoard();
        var placed = Card(1, classId: 1, subjectId: 5, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var moving = Card(2, classId: 1, subjectId: 5, teacherId: 1);
        board.Load(new[] { placed, moving }, Rules());

        var result = board.Evaluate(moving, WeekDay.Dushanba, 2);

        Assert.Equal(PlacementRating.Allowed, result.Rating);
        Assert.True(result.IsAllowed);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void Oyna_hosil_bolsa_ogohlantirish_beriladi()
    {
        var board = new TimetableBoard();
        var placed = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var moving = Card(2, classId: 1, subjectId: 2, teacherId: 2);
        board.Load(new[] { placed, moving }, Rules());

        // 1-soat band, 4-soatga qo'yilsa 2-3 soatlar bo'sh qoladi — oyna.
        var result = board.Evaluate(moving, WeekDay.Dushanba, 4);

        Assert.Equal(PlacementRating.Allowed, result.Rating);
        Assert.Contains("oyna", result.ReasonText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Haftalik_meyordan_oshsa_ogohlantirish_beriladi()
    {
        var quota = new Dictionary<(int, int, int), int> { [(1, 1, 1)] = 1 };
        var rules = new TimetableRuleSet(
            new[] { WeekDay.Dushanba, WeekDay.Seshanba },
            new Dictionary<WeekDay, int> { [WeekDay.Dushanba] = 8, [WeekDay.Seshanba] = 8 },
            null,
            quota);

        var board = new TimetableBoard();
        var placed = Card(1, day: WeekDay.Dushanba, period: 1);
        var moving = Card(2);
        board.Load(new[] { placed, moving }, rules);

        var result = board.Evaluate(moving, WeekDay.Seshanba, 1);

        Assert.Equal(PlacementRating.Allowed, result.Rating);
        Assert.Contains("me'yor", result.ReasonText, StringComparison.OrdinalIgnoreCase);
    }

    // ================= Bloklangan karta =================

    [Fact]
    public void Bloklangan_karta_kochmaydi()
    {
        var board = new TimetableBoard();
        var card = Card(1, day: WeekDay.Dushanba, period: 1);
        card.IsLocked = true;
        board.Load(new[] { card }, Rules());

        var result = board.Evaluate(card, WeekDay.Seshanba, 1);

        Assert.Equal(PlacementRating.Forbidden, result.Rating);
        Assert.Contains("qulflangan", result.ReasonText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(board.PossiblePositions(card));
    }

    [Fact]
    public void Qulf_ochilgach_karta_yana_kochadi()
    {
        var board = new TimetableBoard();
        var card = Card(1, day: WeekDay.Dushanba, period: 1);
        board.Load(new[] { card }, Rules());

        var history = new CommandHistory();
        history.Execute(new SetLockCommand(board, card, true));

        Assert.True(card.IsLocked);
        Assert.False(board.Evaluate(card, WeekDay.Seshanba, 1).IsAllowed);

        history.Undo();

        Assert.False(card.IsLocked);
        Assert.True(board.Evaluate(card, WeekDay.Seshanba, 1).IsAllowed);
    }

    // ================= Juft dars yaxlitligi =================

    [Fact]
    public void Juft_dars_ikkala_soatni_egallaydi()
    {
        var board = new TimetableBoard();
        var doubleCard = Card(1, length: 2, day: WeekDay.Dushanba, period: 3);
        board.Load(new[] { doubleCard }, Rules());

        Assert.Contains(doubleCard, board.CardsAt(WeekDay.Dushanba, 3));
        Assert.Contains(doubleCard, board.CardsAt(WeekDay.Dushanba, 4));
        Assert.DoesNotContain(doubleCard, board.CardsAt(WeekDay.Dushanba, 5));
    }

    [Fact]
    public void Juft_dars_ikkinchi_soatiga_ham_boshqa_karta_qoyilmaydi()
    {
        var board = new TimetableBoard();
        var doubleCard = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 3, length: 2);
        var other = Card(2, classId: 1, subjectId: 2, teacherId: 2);
        board.Load(new[] { doubleCard, other }, Rules());

        Assert.False(board.Evaluate(other, WeekDay.Dushanba, 3).IsAllowed);
        Assert.False(board.Evaluate(other, WeekDay.Dushanba, 4).IsAllowed);
        Assert.True(board.Evaluate(other, WeekDay.Dushanba, 5).IsAllowed);
    }

    [Fact]
    public void Juft_dars_kun_oxiriga_sigmasa_taqiqlanadi()
    {
        var board = new TimetableBoard();
        var doubleCard = Card(1, length: 2);
        board.Load(new[] { doubleCard }, Rules(maxPeriod: 6));

        Assert.False(board.Evaluate(doubleCard, WeekDay.Dushanba, 6).IsAllowed);
        Assert.True(board.Evaluate(doubleCard, WeekDay.Dushanba, 5).IsAllowed);
    }

    [Fact]
    public void Juft_dars_yaxlit_kochadi_va_yaxlit_qaytadi()
    {
        var board = new TimetableBoard();
        var doubleCard = Card(1, length: 2, day: WeekDay.Dushanba, period: 1);
        board.Load(new[] { doubleCard }, Rules());

        var history = new CommandHistory();
        history.Execute(new MoveCardCommand(board, doubleCard, new SlotPosition(WeekDay.Juma, 5)));

        Assert.Contains(doubleCard, board.CardsAt(WeekDay.Juma, 5));
        Assert.Contains(doubleCard, board.CardsAt(WeekDay.Juma, 6));
        Assert.Empty(board.CardsAt(WeekDay.Dushanba, 1));
        Assert.Empty(board.CardsAt(WeekDay.Dushanba, 2));

        history.Undo();

        Assert.Contains(doubleCard, board.CardsAt(WeekDay.Dushanba, 1));
        Assert.Contains(doubleCard, board.CardsAt(WeekDay.Dushanba, 2));
        Assert.Empty(board.CardsAt(WeekDay.Juma, 5));
        Assert.Empty(board.CardsAt(WeekDay.Juma, 6));
    }

    // ================= Hafta maskasi va guruh bo'linmasi =================

    [Fact]
    public void Turli_haftalardagi_kartalar_toqnashmaydi()
    {
        var board = new TimetableBoard();
        var odd = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1, weeksMask: 0b01);
        var even = Card(2, classId: 1, subjectId: 2, teacherId: 2, weeksMask: 0b10);
        board.Load(new[] { odd, even }, Rules());

        Assert.True(board.Evaluate(even, WeekDay.Dushanba, 1).IsAllowed);
    }

    [Fact]
    public void Turli_guruhlar_bir_vaqtda_dars_otishi_mumkin()
    {
        var board = new TimetableBoard();
        var first = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1, group: "1-guruh");
        var second = Card(2, classId: 1, subjectId: 2, teacherId: 2, group: "2-guruh");
        board.Load(new[] { first, second }, Rules());

        Assert.True(board.Evaluate(second, WeekDay.Dushanba, 1).IsAllowed);
    }

    [Fact]
    public void Butun_sinf_darsi_guruh_darsi_bilan_toqnashadi()
    {
        var board = new TimetableBoard();
        var whole = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var group = Card(2, classId: 1, subjectId: 2, teacherId: 2, group: "1-guruh");
        board.Load(new[] { whole, group }, Rules());

        Assert.False(board.Evaluate(group, WeekDay.Dushanba, 1).IsAllowed);
    }

    // ================= SHIFT: mumkin pozitsiyalar va teskari qidiruv =================

    [Fact]
    public void Mumkin_pozitsiyalar_bandlarni_chetlab_otadi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var moving = Card(2, classId: 1, subjectId: 2, teacherId: 2);
        board.Load(new[] { busy, moving }, Rules(maxPeriod: 4, dayCount: 2));

        var positions = board.PossiblePositions(moving);

        Assert.DoesNotContain(new SlotPosition(WeekDay.Dushanba, 1), positions);
        Assert.Contains(new SlotPosition(WeekDay.Seshanba, 1), positions);
    }

    [Fact]
    public void Teskari_qidiruv_faqat_mos_kartalarni_qaytaradi()
    {
        var board = new TimetableBoard();
        var busy = Card(1, classId: 1, subjectId: 1, teacherId: 1, day: WeekDay.Dushanba, period: 1);
        var free = Card(2, classId: 2, subjectId: 2, teacherId: 2);
        board.Load(new[] { busy, free }, Rules());

        var candidates = board.CandidatesFor(WeekDay.Dushanba, 1);

        Assert.Contains(free, candidates);
        Assert.DoesNotContain(busy, candidates);
    }

    // ================= Ishlash o'lchovi =================

    [Fact]
    public void Katta_jadval_baholashi_tez_bajariladi()
    {
        // 30 sinf × 12 soat × 5 kun (ikki smena) — vazifadagi maqsad masshtab.
        const int classCount = 30;
        const int periodCount = 12;
        const int dayCount = 5;

        var days = Enumerable.Range(1, dayCount).Select(i => (WeekDay)i).ToList();
        var rules = new TimetableRuleSet(days, days.ToDictionary(d => d, _ => periodCount));

        var cards = new List<TimetableCard>();
        var id = 1;

        for (var c = 1; c <= classCount; c++)
        {
            for (var p = 1; p <= periodCount; p++)
            {
                foreach (var day in days)
                {
                    cards.Add(Card(id++, classId: c, subjectId: p, teacherId: ((c * p) % 40) + 1,
                        day: day, period: p));
                }
            }
        }

        var board = new TimetableBoard();

        var load = Stopwatch.StartNew();
        board.Load(cards, rules);
        load.Stop();

        Assert.Equal(classCount * periodCount * dayCount, board.Cards.Count);

        var probe = cards[0];
        var evaluate = Stopwatch.StartNew();

        for (var i = 0; i < 1000; i++)
        {
            board.Evaluate(probe, WeekDay.Juma, 12);
        }

        evaluate.Stop();

        // 60 fps uchun bitta baholash <16 ms bo'lishi shart (03-asc-features-ux.md §4.6).
        var perCall = evaluate.Elapsed.TotalMilliseconds / 1000;
        Assert.True(perCall < 16, $"Bitta baholash {perCall:0.###} ms — 16 ms dan katta.");
        Assert.True(load.Elapsed.TotalMilliseconds < 2000, $"Yuklash {load.ElapsedMilliseconds} ms.");
    }
}
