using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Pipeline;
using Xunit;

namespace DarsJadvali.Scheduling.Tests;

/// <summary>
/// divisiontag semantikasi (C-GBL-08, 01-asc-data-model.md 3.2):
/// bir xil tag'li guruhlar birga o'ta oladi, turli tag'lilar — yo'q.
/// </summary>
public class DivisionTagTests
{
    private static (Problem, SolutionState) Setup()
    {
        var p = TestProblems.DividedClass(out _, out _, out _, out _, out _);
        return (p, new SolutionState(p));
    }

    [Fact]
    public void Same_DivisionTag_Groups_Can_Share_A_Slot()
    {
        var (p, state) = Setup();
        // 0- va 1- kartalar: "1-guruh" va "2-guruh" (ikkalasi ham divisiontag = 1)
        var a = p.Cards.First(c => p.Groups[c.GroupIds[0]].Name == "1-guruh");
        var b = p.Cards.First(c => p.Groups[c.GroupIds[0]].Name == "2-guruh");

        Assert.True(state.CanPlace(a, 0, -1));
        state.Place(a, 0, -1);
        Assert.True(state.CanPlace(b, 0, -1), "Bir xil divisiontag'li guruhlar bir vaqtda dars o'ta oladi");
        state.Place(b, 0, -1);

        Assert.Equal(0, HardRules.Check(state.Snapshot()));
    }

    [Fact]
    public void Different_DivisionTag_Groups_Cannot_Share_A_Slot()
    {
        var (p, state) = Setup();
        var a = p.Cards.First(c => p.Groups[c.GroupIds[0]].Name == "1-guruh");     // tag 1
        var b = p.Cards.First(c => p.Groups[c.GroupIds[0]].Name == "O'g'illar");   // tag 2

        state.Place(a, 0, -1);
        Assert.False(state.CanPlace(b, 0, -1), "Turli divisiontag'lar bir slotda bo'la olmaydi");
        Assert.True(state.CanPlace(b, 1, -1));
    }

    [Fact]
    public void EntireClass_Blocks_Every_Division()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var cls = b.AddClass("8-A", 30);
        var whole = b.AddEntireClassGroup(cls);
        var g1 = b.AddGroup(cls, "1-guruh", 1, 15);
        var math = b.AddSubject("Matematika");
        var eng = b.AddSubject("Ingliz tili");
        var t1 = b.AddTeacher("T1");
        var t2 = b.AddTeacher("T2");
        b.AddLesson(math, new[] { t1 }, new[] { whole }, 1);
        b.AddLesson(eng, new[] { t2 }, new[] { g1 }, 1);
        var p = b.Build();

        var state = new SolutionState(p);
        state.Place(p.Cards[0], 0, -1);      // butun sinf (tag 0)
        Assert.False(state.CanPlace(p.Cards[1], 0, -1));
    }

    [Fact]
    public void Slot_Frees_Up_After_Unplacing_All_Cards()
    {
        var (p, state) = Setup();
        var a = p.Cards.First(c => p.Groups[c.GroupIds[0]].Name == "1-guruh");
        var b = p.Cards.First(c => p.Groups[c.GroupIds[0]].Name == "O'g'illar");

        state.Place(a, 0, -1);
        Assert.Equal(1, state.ClassTagAt(0, 0));
        state.Unplace(a);
        Assert.Equal(-1, state.ClassTagAt(0, 0));
        Assert.True(state.CanPlace(b, 0, -1));
    }

    [Fact]
    public void Lesson_Mixing_Two_Divisions_Is_Rejected_At_Build()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var cls = b.AddClass("9-A", 30);
        var g1 = b.AddGroup(cls, "1-guruh", 1, 15);
        var g2 = b.AddGroup(cls, "O'g'illar", 2, 16);
        var subj = b.AddSubject("Fan");
        var t = b.AddTeacher("T");
        b.AddLesson(subj, new[] { t }, new[] { g1, g2 }, 1);

        var ex = Assert.Throws<ArgumentException>(() => b.Build());
        Assert.Contains("divisiontag", ex.Message);
    }

    [Fact]
    public void Generated_Timetable_Respects_Division_Semantics()
    {
        var p = TestProblems.DividedClass(out _, out _, out _, out _, out _);
        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 7, Complexity = Complexity.Small });

        Assert.True(result.IsComplete, result.ToString());
        Assert.Empty(result.HardViolations);

        // Har slotda sinfda faqat bitta bo'linish faol.
        var state = new SolutionState(p);
        state.RestoreFrom(result.Solution);
        for (int slot = 0; slot < p.Grid.SlotCount; slot++)
        {
            var tags = p.Cards
                .Where(c => result.Solution.CardSlots[c.Id] == slot)
                .Select(c => c.ClassDivisionTags[0])
                .Distinct()
                .ToList();
            Assert.True(tags.Count <= 1, $"slot {slot} da {tags.Count} xil bo'linish bor");
        }
    }
}
