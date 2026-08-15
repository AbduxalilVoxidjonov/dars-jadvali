using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace DarsJadvali.Scheduling.Tests;

public class VerifierRelaxerTests
{
    private readonly ITestOutputHelper _out;

    public VerifierRelaxerTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Clean_Problem_Passes_Verification()
    {
        var report = Verifier.Verify(TestProblems.SmallSchool());
        Assert.True(report.IsOk, report.ToString());
    }

    /// <summary>T-I-04: ataylab imkonsiz — o'qituvchi haddan tashqari yuklangan.</summary>
    [Fact]
    public void Overloaded_Teacher_Is_Detected()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var s = b.AddSubject("Matematika");
        for (int i = 0; i < 8; i++)
        {
            var cls = b.AddClass($"5-{i}", 25);
            var g = b.AddEntireClassGroup(cls);
            b.AddLesson(s, new[] { t }, new[] { g }, 5);   // 40 soat, 30 slot
        }
        var p = b.Build();

        var report = Verifier.Verify(p);
        Assert.False(report.IsOk);
        Assert.Contains(report.Faults, f => f.Code == "TEACHER_OVERLOADED");
        _out.WriteLine(report.ToString());
    }

    [Fact]
    public void Room_Shortage_Message_Is_Explicit()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var gym = b.AddRoom("Sport zali", 40);
        var pe = b.AddSubject("Jismoniy tarbiya");
        for (int i = 0; i < 20; i++)
        {
            var t = b.AddTeacher($"JT-{i}");
            var cls = b.AddClass($"6-{i}", 25);
            var g = b.AddEntireClassGroup(cls);
            var l = b.AddLesson(pe, new[] { t }, new[] { g }, 3);
            l.AllowedRoomIds = new[] { gym.Id };
        }
        var p = b.Build();

        var report = Verifier.Verify(p);
        Assert.Contains(report.Faults, f => f.Code == "ROOM_SHORTAGE");
        var fault = report.Faults.First(f => f.Code == "ROOM_SHORTAGE");
        Assert.StartsWith("Xona yetishmaydi:", fault.Message);
        _out.WriteLine(fault.Message);
    }

    [Fact]
    public void Too_Frequent_Subject_Is_Detected()
    {
        var b = new ProblemBuilder(new TimeGrid(3, 8));
        var t = b.AddTeacher("Ali");
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s = b.AddSubject("Matematika");
        b.AddLesson(s, new[] { t }, new[] { g }, 5);      // 5 marta, 3 kun
        var p = b.Build();

        var report = Verifier.Verify(p);
        Assert.Contains(report.Faults, f => f.Code == "TOO_FREQUENT");
    }

    [Fact]
    public void Conflicting_Locked_Cards_Are_Detected()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s1 = b.AddSubject("Matematika");
        var s2 = b.AddSubject("Tarix");
        var l1 = b.AddLesson(s1, new[] { t }, new[] { g }, 1);
        l1.Locked.Add(new FixedPlacement(1, 1));
        var l2 = b.AddLesson(s2, new[] { t }, new[] { g }, 1);
        l2.Locked.Add(new FixedPlacement(1, 1));
        var p = b.Build();

        var report = Verifier.Verify(p);
        Assert.Contains(report.Faults, f => f.Code == "LOCKED_CONFLICT");
    }

    /// <summary>T-I-05: yechim topilmasa — qaysi cheklovni yumshatish kerakligi hisobot qilinadi.</summary>
    [Fact]
    public void Relaxer_Reports_Blocking_Constraint()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var grid = b.Grid;
        // O'qituvchi faqat dushanba ishlaydi.
        for (int d = 1; d < 5; d++) t.Availability.SetDay(grid, d, AvailabilityState.Forbidden);
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s = b.AddSubject("Matematika");
        b.AddLesson(s, new[] { t }, new[] { g }, 10);      // 10 soat, faqat 6 slot
        var p = b.Build();

        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 3, Complexity = Complexity.Small });
        Assert.False(result.IsComplete);
        Assert.NotNull(result.Relaxation);
        Assert.False(result.Relaxation!.IsEmpty);
        Assert.Contains(result.Relaxation.Suggestions, x => x.ConstraintId == "C-AVL-01");
        _out.WriteLine(result.Relaxation.ToString());
    }

    [Fact]
    public void Propagation_Reduces_Domains_From_Locked_Cards()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var t = b.AddTeacher("Ali");
        var cls = b.AddClass("5-A", 25);
        var g = b.AddEntireClassGroup(cls);
        var s1 = b.AddSubject("Matematika");
        var s2 = b.AddSubject("Tarix");
        var l1 = b.AddLesson(s1, new[] { t }, new[] { g }, 1);
        l1.Locked.Add(new FixedPlacement(0, 0));
        b.AddLesson(s2, new[] { t }, new[] { g }, 1);
        var p = b.Build();

        Propagator.ResetDomains(p);
        var res = Propagator.Propagate(p);
        Assert.True(res.Feasible);
        Assert.True(res.RemovedSlots >= 1);
        Assert.False(p.Cards[1].Domain.Test(0), "Qulflangan karta slotini boshqa karta egallay olmaydi");
    }
}
