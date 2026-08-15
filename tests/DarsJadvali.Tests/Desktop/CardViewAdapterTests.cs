using DarsJadvali.Application.Board;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Application (<c>Card</c>/<c>Lesson</c>) ↔ UI karta modeli adapteri — <b>yagona</b>
/// bog'lanish nuqtasi. Eski <c>ScheduleEntryCardAdapter</c> ning o'rnini bosdi.
/// </summary>
/// <remarks>
/// Asosiy tekshiruv: <c>Length</c>, <c>WeeksMask</c>, <c>GroupName</c> va <c>IsLocked</c>
/// endi <b>haqiqiy</b> manbadan keladi — ilgari ular sun'iy ravishda
/// <c>1</c> / "har hafta" / bo'sh / <c>false</c> edi.
/// </remarks>
public sealed class CardViewAdapterTests
{
    private static (List<Teacher> Teachers, List<Subject> Subjects) World()
    {
        var teachers = new List<Teacher>
        {
            new() { Id = 1, FullName = "Voxidjonov Abduxalil", ColorCode = "#1976D2", IsActive = true },
            new() { Id = 2, FullName = "Karimova Nodira", ColorCode = "#C62828", IsActive = true },
        };

        var subjects = new List<Subject>
        {
            new() { Id = 10, Name = "Matematika", Code = "MAT", ColorCode = "#455A64" },
            new() { Id = 11, Name = "Fizika", Code = "FIZ", ColorCode = "#00695C" },
        };

        return (teachers, subjects);
    }

    private static CardView View(
        int cardId = 500,
        int lessonId = 77,
        int subjectId = 10,
        string subjectName = "Matematika",
        int teacherId = 1,
        string teacherName = "Voxidjonov Abduxalil",
        int classId = 100,
        string className = "5-A",
        string groupName = "",
        int dayNo = 1,
        int periodNo = 3,
        int length = 1,
        int weeksMask = 3,
        bool isLocked = false,
        string? room = "310")
        => new(
            CardId: cardId,
            ScheduleId: 1,
            LessonId: lessonId,
            SubjectId: subjectId,
            SubjectName: subjectName,
            TeacherIds: new[] { teacherId },
            TeacherNames: new[] { teacherName },
            SchoolClassIds: new[] { classId },
            ClassName: className,
            StudentGroupIds: new[] { 900 },
            GroupName: groupName,
            DayNo: dayNo,
            PeriodId: 3000 + periodNo,
            PeriodNo: periodNo,
            Length: length,
            WeeksMask: weeksMask,
            IsLocked: isLocked,
            RoomNumber: room);

    [Fact]
    public void Kartochka_UI_kartasiga_togri_ogiriladi()
    {
        var (teachers, subjects) = World();

        var card = Assert.Single(
            CardViewAdapter.ToCards(new[] { View() }, teachers, subjects));

        Assert.Equal(500, card.EntityId);
        Assert.Equal(77, card.LessonId);
        Assert.Equal("Matematika", card.SubjectName);
        Assert.Equal("5-A", card.ClassName);
        Assert.Equal("Voxidjonov A.", Assert.Single(card.TeacherNames));

        // DayNo 0-based (dushanba = 0) → WeekDay 1-based.
        Assert.Equal(WeekDay.Seshanba, card.Day);
        Assert.Equal(3, card.Period);
        Assert.Equal("310", card.RoomNumber);
        Assert.Equal("#1976D2", card.ColorCode);
        Assert.True(card.IsPlaced);
        Assert.Equal(100, card.ClassGroupId);
    }

    [Fact]
    public void Juft_dars_uzunligi_haqiqiy_maydondan_keladi()
    {
        var (teachers, subjects) = World();

        var card = Assert.Single(
            CardViewAdapter.ToCards(new[] { View(length: 2) }, teachers, subjects));

        Assert.Equal(2, card.Length);
        Assert.True(card.IsDouble);
        Assert.Equal(new[] { 3, 4 }, card.OccupiedPeriods);
    }

    [Fact]
    public void Hafta_maskasi_haqiqiy_maydondan_keladi()
    {
        var (teachers, subjects) = World();

        var odd = Assert.Single(
            CardViewAdapter.ToCards(new[] { View(weeksMask: 0b01) }, teachers, subjects));
        var even = Assert.Single(
            CardViewAdapter.ToCards(new[] { View(cardId: 501, weeksMask: 0b10) }, teachers, subjects));

        Assert.Equal(0b01, odd.WeeksMask);
        Assert.Equal(0b10, even.WeeksMask);

        // Turli haftadagi kartalar to'qnashmaydi.
        Assert.False(odd.OverlapsWeeks(even));
    }

    [Fact]
    public void Guruh_bolinmasi_haqiqiy_maydondan_keladi()
    {
        var (teachers, subjects) = World();

        var card = Assert.Single(
            CardViewAdapter.ToCards(new[] { View(groupName: "1-guruh") }, teachers, subjects));

        Assert.Equal("1-guruh", card.GroupName);
        Assert.Equal("5-A / 1-guruh", card.ScopeText);
    }

    [Fact]
    public void Qulf_haqiqiy_maydondan_keladi()
    {
        var (teachers, subjects) = World();

        var locked = Assert.Single(
            CardViewAdapter.ToCards(new[] { View(isLocked: true) }, teachers, subjects));

        Assert.True(locked.IsLocked);
    }

    [Fact]
    public void Bitta_darsning_kartalari_bitta_LessonKey_oladi()
    {
        var (teachers, subjects) = World();

        var cards = CardViewAdapter.ToCards(
            new[]
            {
                View(cardId: 1, lessonId: 77, periodNo: 1),
                View(cardId: 2, lessonId: 77, periodNo: 5),
                View(cardId: 3, lessonId: 78, periodNo: 5),
            },
            teachers,
            subjects);

        Assert.Equal(cards[0].LessonKey, cards[1].LessonKey);
        Assert.NotEqual(cards[0].LessonKey, cards[2].LessonKey);
    }

    [Fact]
    public void Smena_raqami_sinfdan_olinadi()
    {
        var (teachers, subjects) = World();
        var shifts = new Dictionary<int, int> { [100] = 2 };

        var card = Assert.Single(
            CardViewAdapter.ToCards(new[] { View() }, teachers, subjects, shifts));

        Assert.Equal(2, card.ShiftNo);
    }

    [Fact]
    public void Joylashtirilmagan_darslar_aniq_royxatdan_yasaladi()
    {
        var (teachers, subjects) = World();

        var lessons = new[]
        {
            new UnplacedLessonView(
                LessonId: 77,
                SubjectId: 10,
                SubjectName: "Matematika",
                ClassName: "5-A",
                GroupName: string.Empty,
                TeacherIds: new[] { 1 },
                TeacherNames: new[] { "Voxidjonov Abduxalil" },
                PeriodsPerWeek: 5,
                PlacedPeriods: 2,
                PeriodsPerCard: 1),
            new UnplacedLessonView(
                LessonId: 78,
                SubjectId: 11,
                SubjectName: "Fizika",
                ClassName: "5-B",
                GroupName: "2-guruh",
                TeacherIds: new[] { 2 },
                TeacherNames: new[] { "Karimova Nodira" },
                PeriodsPerWeek: 2,
                PlacedPeriods: 2,
                PeriodsPerCard: 1),
        };

        var cards = CardViewAdapter.ToUnplacedCards(
            lessons, teachers, subjects, 1000, new Dictionary<string, int> { ["5-A"] = 100 });

        // 5 − 2 = 3 ta matematika; fizika me'yori to'lgan — karta bermaydi.
        Assert.Equal(3, cards.Count);
        Assert.All(cards, c => Assert.Equal("Matematika", c.SubjectName));
        Assert.All(cards, c => Assert.False(c.IsPlaced));
        Assert.All(cards, c => Assert.Null(c.EntityId));
        Assert.All(cards, c => Assert.Equal(100, c.ClassGroupId));
        Assert.All(cards, c => Assert.Equal(77, c.LessonId));
    }

    [Fact]
    public void Juft_dars_istagi_joylashtirilmagan_kartalarga_ham_tarqaladi()
    {
        var (teachers, subjects) = World();

        var lessons = new[]
        {
            new UnplacedLessonView(
                LessonId: 77,
                SubjectId: 10,
                SubjectName: "Matematika",
                ClassName: "5-A",
                GroupName: string.Empty,
                TeacherIds: new[] { 1 },
                TeacherNames: new[] { "Voxidjonov Abduxalil" },
                PeriodsPerWeek: 5,
                PlacedPeriods: 0,
                PeriodsPerCard: 2),
        };

        var cards = CardViewAdapter.ToUnplacedCards(lessons, teachers, subjects, 1000);

        // 5 soat = 2 + 2 + 1.
        Assert.Equal(new[] { 2, 2, 1 }, cards.Select(c => c.Length));
    }

    [Fact]
    public void Kochirish_sorovi_kun_va_soatdan_yasaladi()
    {
        var (teachers, subjects) = World();
        var periodIds = new Dictionary<int, int> { [3] = 3003 };

        var card = Assert.Single(
            CardViewAdapter.ToCards(new[] { View() }, teachers, subjects));

        var placement = CardViewAdapter.ToPlacement(card, periodIds);

        Assert.NotNull(placement);
        Assert.Equal(500, placement!.CardId);
        Assert.Equal(1, placement.DayNo);
        Assert.Equal(3003, placement.PeriodId);
        Assert.Equal(3, placement.WeeksMask);
    }

    [Fact]
    public void Kartochkasiz_karta_kochirish_sorovi_bermaydi()
    {
        var (teachers, subjects) = World();

        var lessons = new[]
        {
            new UnplacedLessonView(77, 10, "Matematika", "5-A", string.Empty,
                new[] { 1 }, new[] { "Voxidjonov Abduxalil" }, 1, 0, 1),
        };

        var card = Assert.Single(CardViewAdapter.ToUnplacedCards(lessons, teachers, subjects, 1000));
        card.MoveTo(new DarsJadvali.Desktop.Models.SlotPosition(WeekDay.Dushanba, 1));

        Assert.Null(CardViewAdapter.ToPlacement(card, new Dictionary<int, int> { [1] = 1001 }));
    }
}
