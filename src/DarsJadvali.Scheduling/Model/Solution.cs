namespace DarsJadvali.Scheduling.Model;

/// <summary>Bitta kartaning yakuniy joylashuvi.</summary>
public readonly record struct Placement(
    int CardId,
    int LessonId,
    int SubjectId,
    int DayIndex,
    int Period,
    int Length,
    int RoomId);

/// <summary>O'zgarmas yechim tasviri (snapshot). Determinizm testi shu ustida solishtiriladi.</summary>
public sealed class Solution
{
    internal Solution(Problem problem, int[] cardSlots, int[] cardRooms)
    {
        Problem = problem;
        CardSlots = cardSlots;
        CardRooms = cardRooms;
    }

    public Problem Problem { get; }

    /// <summary>Karta -> slot (-1 = joylanmagan).</summary>
    public int[] CardSlots { get; }

    /// <summary>Karta -> xona (-1 = xona yo'q).</summary>
    public int[] CardRooms { get; }

    public int PlacedCount
    {
        get
        {
            int n = 0;
            foreach (var s in CardSlots) if (s >= 0) n++;
            return n;
        }
    }

    public int UnplacedCount => CardSlots.Length - PlacedCount;

    public IEnumerable<int> UnplacedCardIds
    {
        get
        {
            for (int i = 0; i < CardSlots.Length; i++)
                if (CardSlots[i] < 0) yield return i;
        }
    }

    public IReadOnlyList<Placement> Placements
    {
        get
        {
            var list = new List<Placement>(CardSlots.Length);
            for (int i = 0; i < CardSlots.Length; i++)
            {
                if (CardSlots[i] < 0) continue;
                var c = Problem.Cards[i];
                list.Add(new Placement(
                    c.Id, c.LessonId, c.SubjectId,
                    Problem.Grid.DayOfSlot(CardSlots[i]),
                    Problem.Grid.PeriodOfSlot(CardSlots[i]),
                    c.Length, CardRooms[i]));
            }
            return list;
        }
    }

    /// <summary>Determinizm testi uchun barqaror imzo.</summary>
    public string Fingerprint()
    {
        var sb = new System.Text.StringBuilder(CardSlots.Length * 8);
        for (int i = 0; i < CardSlots.Length; i++)
        {
            sb.Append(CardSlots[i]);
            sb.Append(':');
            sb.Append(CardRooms[i]);
            sb.Append(';');
        }
        return sb.ToString();
    }

    public Solution Clone()
        => new(Problem, (int[])CardSlots.Clone(), (int[])CardRooms.Clone());
}
