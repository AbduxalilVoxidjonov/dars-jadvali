using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Pipeline;
using Xunit;

namespace DarsJadvali.Scheduling.Tests;

public class HardConstraintTests
{
    /// <summary>T-H-01: bitta o'qituvchi bir vaqtda ikki sinfda bo'la olmaydi (C-GBL-01).</summary>
    [Fact]
    public void Teacher_Cannot_Be_In_Two_Places()
    {
        var p = TestProblems.OneTeacherTwoClasses();
        var state = new SolutionState(p);
        var a = p.Cards[0];
        var b = p.Cards.First(c => c.ClassIds[0] != a.ClassIds[0]);

        state.Place(a, 0, -1);
        Assert.False(state.CanPlace(b, 0, -1));
        Assert.True(state.CanPlace(b, 1, -1));
    }

    [Fact]
    public void Generated_Timetable_Has_No_Teacher_Collision()
    {
        var p = TestProblems.OneTeacherTwoClasses(periodsEach: 5);
        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 3, Complexity = Complexity.Small });
        Assert.True(result.IsComplete, result.ToString());
        Assert.Equal(0, HardRules.Check(result.Solution));
    }

    /// <summary>T-H-07: taqiqlangan (qizil) pozitsiya domain'da bo'lmaydi (C-AVL-01).</summary>
    [Fact]
    public void Forbidden_TimeOff_Is_Removed_From_Domain()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var grid = b.Grid;
        for (int pr = 0; pr < 6; pr++) t.Availability.Set(grid, 0, pr, AvailabilityState.Forbidden);
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s = b.AddSubject("Matematika");
        b.AddLesson(s, new[] { t }, new[] { g }, 4);
        var p = b.Build();

        foreach (var card in p.Cards)
            for (int pr = 0; pr < 6; pr++)
                Assert.False(card.Domain.Test(grid.SlotOf(0, pr)), "Dushanba taqiqlangan bo'lishi kerak");

        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 1, Complexity = Complexity.Small });
        Assert.True(result.IsComplete);
        foreach (var pl in result.Solution.Placements) Assert.NotEqual(0, pl.DayIndex);
    }

    /// <summary>T-H-04: qo'sh dars kun oxiridan oshib keta olmaydi (C-DBL-01).</summary>
    [Fact]
    public void Double_Lesson_Cannot_Start_At_Last_Period()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s = b.AddSubject("Fizika");
        b.AddLesson(s, new[] { t }, new[] { g }, 4, periodsPerCard: 2);
        var p = b.Build();

        Assert.Equal(2, p.Cards.Length);
        foreach (var card in p.Cards)
        {
            Assert.Equal(2, card.Length);
            for (int d = 0; d < 5; d++)
                Assert.False(card.Domain.Test(p.Grid.SlotOf(d, 5)), "Oxirgi darsdan qo'sh dars boshlana olmaydi");
        }
    }

    [Fact]
    public void Double_Lesson_Occupies_Consecutive_Periods()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s = b.AddSubject("Fizika");
        b.AddLesson(s, new[] { t }, new[] { g }, 2, periodsPerCard: 2);
        var p = b.Build();

        var state = new SolutionState(p);
        state.Place(p.Cards[0], 0, -1);
        Assert.True(state.TeacherBusy(0).Test(0));
        Assert.True(state.TeacherBusy(0).Test(1));
        Assert.False(state.TeacherBusy(0).Test(2));
    }

    /// <summary>T-I-09 / C-GBL-06: qulflangan karta ko'chmaydi.</summary>
    [Fact]
    public void Locked_Card_Never_Moves()
    {
        var p = BuildLockedProblem(out int lockedSlot);
        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 42, Complexity = Complexity.Normal });

        Assert.True(result.IsComplete, result.ToString());
        var locked = p.Cards.First(c => c.IsLocked);
        Assert.Equal(lockedSlot, result.Solution.CardSlots[locked.Id]);
    }

    [Fact]
    public void Locked_Card_Cannot_Be_Placed_Elsewhere()
    {
        var p = BuildLockedProblem(out int lockedSlot);
        var state = new SolutionState(p);
        var locked = p.Cards.First(c => c.IsLocked);
        Assert.False(state.CanPlace(locked, lockedSlot + 1, -1));
        Assert.True(state.CanPlace(locked, lockedSlot, -1));
    }

    private static Problem BuildLockedProblem(out int lockedSlot)
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s1 = b.AddSubject("Matematika");
        var s2 = b.AddSubject("Tarix");
        var l = b.AddLesson(s1, new[] { t }, new[] { g }, 3);
        l.Locked.Add(new FixedPlacement(2, 4));
        b.AddLesson(s2, new[] { t }, new[] { g }, 3);
        var p = b.Build();
        lockedSlot = p.Grid.SlotOf(2, 4);
        return p;
    }

    /// <summary>T-H-02: 3 ta jismoniy tarbiya, bitta sport zali → uch xil slotda (C-ROM-01).</summary>
    [Fact]
    public void Single_Gym_Forces_Different_Slots()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var gym = b.AddRoom("Sport zali", 40);
        var pe = b.AddSubject("Jismoniy tarbiya");
        for (int i = 0; i < 3; i++)
        {
            var t = b.AddTeacher($"JT-{i}");
            var cls = b.AddClass($"6-{(char)('A' + i)}", 25);
            var g = b.AddEntireClassGroup(cls);
            var l = b.AddLesson(pe, new[] { t }, new[] { g }, 2);
            l.AllowedRoomIds = new[] { gym.Id };
        }
        var p = b.Build();

        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 11, Complexity = Complexity.Normal });
        Assert.True(result.IsComplete, result.ToString());

        var occupied = new HashSet<int>();
        foreach (var pl in result.Solution.Placements)
        {
            int slot = p.Grid.SlotOf(pl.DayIndex, pl.Period);
            Assert.True(occupied.Add(slot), "Bitta zalda ikki dars bo'lmasligi kerak");
        }
    }

    /// <summary>T-H-09: sig'imi kichik xona nomzodlardan chiqariladi (C-ROM-02).</summary>
    [Fact]
    public void Room_Capacity_Filters_Candidates()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var small = b.AddRoom("Kichik", 20);
        var big = b.AddRoom("Katta", 40);
        var t = b.AddTeacher("T");
        var cls = b.AddClass("7-A", 30);
        var g = b.AddEntireClassGroup(cls);
        var s = b.AddSubject("Matematika");
        var l = b.AddLesson(s, new[] { t }, new[] { g }, 2);
        l.AllowedRoomIds = new[] { small.Id, big.Id };
        var p = b.Build();

        foreach (var card in p.Cards)
            Assert.Equal(new[] { big.Id }, card.AllowedRoomIds);
    }

    /// <summary>Xonasiz maktab: bo'sh xona ro'yxati = xona cheklovi yo'q.</summary>
    [Fact]
    public void Works_Without_Any_Rooms()
    {
        var p = TestProblems.SmallSchool();
        Assert.Empty(p.Rooms);
        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 5, Complexity = Complexity.Small });
        Assert.True(result.IsComplete, result.ToString());
        Assert.All(result.Solution.Placements, pl => Assert.Equal(-1, pl.RoomId));
    }

    /// <summary>T-A-04: optimizatsiya davomida hard invariant buzilmaydi.</summary>
    [Fact]
    public void Optimization_Never_Breaks_Hard_Constraints()
    {
        var p = TestProblems.SmallSchool();
        var result = new Scheduler().Generate(p, new GenerationOptions
        {
            Seed = 99,
            Complexity = Complexity.Small,
            MaxOptimizeIterations = 50_000,
        });
        Assert.Empty(result.HardViolations);
        Assert.Equal(0, result.Cost.HardViolations);
    }
}
