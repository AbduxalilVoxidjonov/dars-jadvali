namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// Generatsiya uchun to'liq masala. DB/EF'dan mustaqil — sof POCO.
/// <see cref="ProblemBuilder"/> orqali quriladi.
/// </summary>
public sealed class Problem
{
    internal Problem(
        TimeGrid grid,
        TeacherDef[] teachers,
        ClassDef[] classes,
        GroupDef[] groups,
        RoomDef[] rooms,
        SubjectDef[] subjects,
        LessonDef[] lessons,
        Card[] cards,
        bool useRoomCapacities)
    {
        Grid = grid;
        Teachers = teachers;
        Classes = classes;
        Groups = groups;
        Rooms = rooms;
        Subjects = subjects;
        Lessons = lessons;
        Cards = cards;
        UseRoomCapacities = useRoomCapacities;

        GroupsOfClass = new int[classes.Length][];
        var tmp = new List<int>[classes.Length];
        for (int i = 0; i < classes.Length; i++) tmp[i] = new List<int>();
        foreach (var g in groups) tmp[g.ClassId].Add(g.Id);
        for (int i = 0; i < classes.Length; i++) GroupsOfClass[i] = tmp[i].ToArray();

        CardsOfLesson = BuildCardsOfLesson(lessons.Length, cards);
        (ClassSubjectPairs, CardsOfClassSubject) = BuildClassSubjectIndex(cards);
        CardsOfTeacher = BuildInverse(teachers.Length, cards, c => c.TeacherIds);
        CardsOfClass = BuildInverse(classes.Length, cards, c => c.ClassIds);
        CardsOfGroup = BuildInverse(groups.Length, cards, c => c.GroupIds);
    }

    public TimeGrid Grid { get; }
    public TeacherDef[] Teachers { get; }
    public ClassDef[] Classes { get; }
    public GroupDef[] Groups { get; }
    public RoomDef[] Rooms { get; }
    public SubjectDef[] Subjects { get; }
    public LessonDef[] Lessons { get; }
    public Card[] Cards { get; }

    /// <summary>C-ROM-03 — xona sig'imlarini tekshirish global kaliti.</summary>
    public bool UseRoomCapacities { get; }

    public int[][] GroupsOfClass { get; }
    public int[][] CardsOfLesson { get; }
    public int[][] CardsOfTeacher { get; }
    public int[][] CardsOfClass { get; }
    public int[][] CardsOfGroup { get; }

    /// <summary>C-DST uchun (classId, subjectId) juftliklari.</summary>
    public (int ClassId, int SubjectId)[] ClassSubjectPairs { get; }

    /// <summary><see cref="ClassSubjectPairs"/> bilan parallel: shu juftlikning kartalari.</summary>
    public int[][] CardsOfClassSubject { get; }

    private int[][]? _neighbors;

    /// <summary>
    /// Konflikt grafi: resurs (o'qituvchi / guruh / mos kelmaydigan bo'linish) baham ko'radigan kartalar.
    /// Propagation, ejection chain va Kempe chain uchun.
    /// </summary>
    public int[][] Neighbors
    {
        get
        {
            if (_neighbors is not null) return _neighbors;
            int n = Cards.Length;
            var sets = new HashSet<int>[n];
            for (int i = 0; i < n; i++) sets[i] = new HashSet<int>();

            void Link(int[] cardIds)
            {
                for (int i = 0; i < cardIds.Length; i++)
                    for (int j = i + 1; j < cardIds.Length; j++)
                    {
                        sets[cardIds[i]].Add(cardIds[j]);
                        sets[cardIds[j]].Add(cardIds[i]);
                    }
            }

            foreach (var t in CardsOfTeacher) Link(t);
            foreach (var g in CardsOfGroup) Link(g);
            // Sinf ichida turli bo'linishlar ham to'qnashadi (C-GBL-08).
            for (int c = 0; c < Classes.Length; c++)
            {
                var list = CardsOfClass[c];
                for (int i = 0; i < list.Length; i++)
                    for (int j = i + 1; j < list.Length; j++)
                    {
                        var a = Cards[list[i]];
                        var b = Cards[list[j]];
                        int ta = a.ClassDivisionTags[Array.IndexOf(a.ClassIds, c)];
                        int tb = b.ClassDivisionTags[Array.IndexOf(b.ClassIds, c)];
                        if (ta != tb)
                        {
                            sets[a.Id].Add(b.Id);
                            sets[b.Id].Add(a.Id);
                        }
                    }
            }

            var res = new int[n][];
            for (int i = 0; i < n; i++)
            {
                var arr = sets[i].ToArray();
                Array.Sort(arr);   // determinizm
                res[i] = arr;
            }
            _neighbors = res;
            return res;
        }
    }

    private Dictionary<(int, int), int[]>? _classSubjectMap;

    /// <summary>(sinf, fan) juftligining kartalari. C-DST-01/05 uchun.</summary>
    public int[] CardsForClassSubject(int classId, int subjectId)
    {
        if (_classSubjectMap is null)
        {
            var map = new Dictionary<(int, int), int[]>(ClassSubjectPairs.Length);
            for (int i = 0; i < ClassSubjectPairs.Length; i++)
                map[ClassSubjectPairs[i]] = CardsOfClassSubject[i];
            _classSubjectMap = map;
        }
        return _classSubjectMap.TryGetValue((classId, subjectId), out var arr) ? arr : Array.Empty<int>();
    }

    public int TotalPeriodsDemand
    {
        get
        {
            int n = 0;
            foreach (var c in Cards) n += c.Length;
            return n;
        }
    }

    private static int[][] BuildCardsOfLesson(int lessonCount, Card[] cards)
    {
        var lists = new List<int>[lessonCount];
        for (int i = 0; i < lessonCount; i++) lists[i] = new List<int>();
        foreach (var c in cards) lists[c.LessonId].Add(c.Id);
        var res = new int[lessonCount][];
        for (int i = 0; i < lessonCount; i++) res[i] = lists[i].ToArray();
        return res;
    }

    private static int[][] BuildInverse(int count, Card[] cards, Func<Card, int[]> selector)
    {
        var lists = new List<int>[count];
        for (int i = 0; i < count; i++) lists[i] = new List<int>();
        foreach (var c in cards)
            foreach (var id in selector(c))
                lists[id].Add(c.Id);
        var res = new int[count][];
        for (int i = 0; i < count; i++) res[i] = lists[i].ToArray();
        return res;
    }

    private static ((int, int)[], int[][]) BuildClassSubjectIndex(Card[] cards)
    {
        var order = new List<(int, int)>();
        var map = new Dictionary<(int, int), List<int>>();
        foreach (var c in cards)
        {
            foreach (var cls in c.ClassIds)
            {
                var key = (cls, c.SubjectId);
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    map[key] = list;
                    order.Add(key);
                }
                list.Add(c.Id);
            }
        }
        // Determinizm: kartalar tartibida paydo bo'lish tartibi saqlanadi (Dictionary tartibiga tayanmaymiz).
        var pairs = order.ToArray();
        var arr = new int[pairs.Length][];
        for (int i = 0; i < pairs.Length; i++) arr[i] = map[pairs[i]].ToArray();
        return (pairs, arr);
    }
}
