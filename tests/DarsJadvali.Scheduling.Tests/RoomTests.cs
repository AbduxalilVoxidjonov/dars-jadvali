using DarsJadvali.Scheduling.Constraints;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Pipeline;
using DarsJadvali.Scheduling.Rooms;
using DarsJadvali.Scheduling.Util;
using Xunit;

namespace DarsJadvali.Scheduling.Tests;

public class RoomTests
{
    /// <summary>T-A-08: Hopcroft–Karp natijasi brute-force maksimal moslik bilan mos.</summary>
    [Fact]
    public void HopcroftKarp_Matches_BruteForce_On_Random_Graphs()
    {
        var rng = new Xoshiro256SS(4242);
        for (int iter = 0; iter < 300; iter++)
        {
            int left = 1 + rng.Next(6);
            int right = 1 + rng.Next(6);
            var adj = new IReadOnlyList<int>[left];
            for (int u = 0; u < left; u++)
            {
                var list = new List<int>();
                for (int v = 0; v < right; v++)
                    if (rng.NextDouble() < 0.45) list.Add(v);
                adj[u] = list;
            }

            HopcroftKarp.Match(left, right, adj, out int matched);
            int expected = BruteForceMatching(left, right, adj);
            Assert.Equal(expected, matched);
        }
    }

    private static int BruteForceMatching(int left, int right, IReadOnlyList<int>[] adj)
    {
        var usedRight = new bool[right];
        int best = 0;
        void Recurse(int u, int count)
        {
            if (count + (left - u) <= best) return;
            if (u == left) { best = Math.Max(best, count); return; }
            Recurse(u + 1, count);                       // u ni qoldirish
            foreach (var v in adj[u])
            {
                if (usedRight[v]) continue;
                usedRight[v] = true;
                Recurse(u + 1, count + 1);
                usedRight[v] = false;
            }
        }
        Recurse(0, 0);
        return best;
    }

    [Fact]
    public void RoomAssigner_Produces_Valid_Assignment()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var labs = new[] { b.AddRoom("Lab-1", 40), b.AddRoom("Lab-2", 40) };
        var subj = b.AddSubject("Kimyo");
        for (int i = 0; i < 4; i++)
        {
            var t = b.AddTeacher($"K-{i}");
            var cls = b.AddClass($"9-{(char)('A' + i)}", 28);
            var g = b.AddEntireClassGroup(cls);
            var l = b.AddLesson(subj, new[] { t }, new[] { g }, 3);
            l.AllowedRoomIds = labs.Select(x => x.Id).ToArray();
        }
        var p = b.Build();

        var result = new Scheduler().Generate(p, new GenerationOptions { Seed = 8, Complexity = Complexity.Normal });
        Assert.True(result.IsComplete, result.ToString());

        var state = new SolutionState(p);
        state.RestoreFrom(result.Solution);
        Assert.Equal(0, RoomAssigner.AssignAll(state));
        Assert.Equal(0, HardRules.Check(state.Snapshot()));

        // Har slotda bitta xonada bitta karta.
        var seen = new HashSet<(int, int)>();
        foreach (var pl in state.Snapshot().Placements)
        {
            int slot = p.Grid.SlotOf(pl.DayIndex, pl.Period);
            Assert.True(pl.RoomId >= 0);
            Assert.True(seen.Add((slot, pl.RoomId)));
        }
    }

    [Fact]
    public void Parallel_Lessons_Room_Allows_Multiple_Cards()
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var hall = b.AddRoom("Katta zal", 100);
        hall.ParallelLessons = 2;
        var subj = b.AddSubject("Jismoniy tarbiya");
        for (int i = 0; i < 2; i++)
        {
            var t = b.AddTeacher($"JT-{i}");
            var cls = b.AddClass($"6-{(char)('A' + i)}", 25);
            var g = b.AddEntireClassGroup(cls);
            var l = b.AddLesson(subj, new[] { t }, new[] { g }, 1);
            l.AllowedRoomIds = new[] { hall.Id };
        }
        var p = b.Build();

        var state = new SolutionState(p);
        Assert.True(state.CanPlace(p.Cards[0], 0, hall.Id));
        state.Place(p.Cards[0], 0, hall.Id);
        Assert.True(state.CanPlace(p.Cards[1], 0, hall.Id));
        state.Place(p.Cards[1], 0, hall.Id);
        Assert.Equal(0, HardRules.Check(state.Snapshot()));
    }
}
