namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// Masalani qurish uchun API. Keyingi bosqichda EF entity'laridan shu builder'ga mapper yoziladi.
/// Barcha Id'lar 0 dan boshlanuvchi ketma-ket indekslar (massiv indeksatsiyasi uchun).
/// </summary>
public sealed class ProblemBuilder
{
    private readonly TimeGrid _grid;
    private readonly List<TeacherDef> _teachers = new();
    private readonly List<ClassDef> _classes = new();
    private readonly List<GroupDef> _groups = new();
    private readonly List<RoomDef> _rooms = new();
    private readonly List<SubjectDef> _subjects = new();
    private readonly List<LessonDef> _lessons = new();

    public ProblemBuilder(TimeGrid grid) => _grid = grid ?? throw new ArgumentNullException(nameof(grid));

    public TimeGrid Grid => _grid;

    /// <summary>C-ROM-03 — xona sig'imlarini hisobga olish.</summary>
    public bool UseRoomCapacities { get; set; } = true;

    public TeacherDef AddTeacher(string name)
    {
        var t = new TeacherDef { Id = _teachers.Count, Name = name };
        _teachers.Add(t);
        return t;
    }

    public ClassDef AddClass(string name, int studentCount = 0)
    {
        var c = new ClassDef { Id = _classes.Count, Name = name, StudentCount = studentCount };
        _classes.Add(c);
        return c;
    }

    /// <summary>
    /// Guruh qo'shish. <paramref name="divisionTag"/> = 0 — butun sinf.
    /// Bir xil tag'li guruhlar bir vaqtda dars o'ta oladi, turli tag'lilar yo'q (C-GBL-08).
    /// </summary>
    public GroupDef AddGroup(ClassDef cls, string name, int divisionTag, int studentCount = 0)
    {
        if (divisionTag < 0) throw new ArgumentOutOfRangeException(nameof(divisionTag));
        var g = new GroupDef
        {
            Id = _groups.Count,
            Name = name,
            ClassId = cls.Id,
            DivisionTag = divisionTag,
            StudentCount = studentCount,
        };
        _groups.Add(g);
        return g;
    }

    /// <summary>Butun sinf guruhi (aSc: <c>entireclass=1, divisiontag=0</c>).</summary>
    public GroupDef AddEntireClassGroup(ClassDef cls, string? name = null)
        => AddGroup(cls, name ?? $"{cls.Name} (butun sinf)", 0, cls.StudentCount);

    public RoomDef AddRoom(string name, int capacity = 0)
    {
        var r = new RoomDef { Id = _rooms.Count, Name = name, Capacity = capacity };
        _rooms.Add(r);
        return r;
    }

    public SubjectDef AddSubject(string name)
    {
        var s = new SubjectDef { Id = _subjects.Count, Name = name };
        _subjects.Add(s);
        return s;
    }

    /// <summary>Dars talabini qo'shish.</summary>
    public LessonDef AddLesson(
        SubjectDef subject,
        IEnumerable<TeacherDef> teachers,
        IEnumerable<GroupDef> groups,
        int periodsPerWeek,
        int periodsPerCard = 1)
    {
        if (periodsPerWeek <= 0) throw new ArgumentOutOfRangeException(nameof(periodsPerWeek));
        if (periodsPerCard <= 0) throw new ArgumentOutOfRangeException(nameof(periodsPerCard));

        var l = new LessonDef
        {
            Id = _lessons.Count,
            SubjectId = subject.Id,
            Name = subject.Name,
            TeacherIds = teachers.Select(t => t.Id).Distinct().OrderBy(x => x).ToArray(),
            GroupIds = groups.Select(g => g.Id).Distinct().OrderBy(x => x).ToArray(),
            PeriodsPerWeek = periodsPerWeek,
            PeriodsPerCard = periodsPerCard,
        };
        if (l.GroupIds.Length == 0)
            throw new ArgumentException("Dars kamida bitta guruhga biriktirilishi kerak.", nameof(groups));
        _lessons.Add(l);
        return l;
    }

    /// <summary>Masalani quradi: kartalarni hosil qiladi va statik domain'larni hisoblaydi (Faza 1).</summary>
    public Problem Build()
    {
        var teachers = _teachers.ToArray();
        var classes = _classes.ToArray();
        var groups = _groups.ToArray();
        var rooms = _rooms.ToArray();
        var subjects = _subjects.ToArray();
        var lessons = _lessons.ToArray();

        var cards = new List<Card>();
        foreach (var l in lessons)
            BuildCards(l, groups, rooms, subjects, teachers, classes, cards);

        var problem = new Problem(_grid, teachers, classes, groups, rooms, subjects, lessons,
                                  cards.ToArray(), UseRoomCapacities);
        ComputeDegrees(problem);
        return problem;
    }

    private void BuildCards(LessonDef l, GroupDef[] groups, RoomDef[] rooms, SubjectDef[] subjects,
                            TeacherDef[] teachers, ClassDef[] classes, List<Card> outCards)
    {
        // --- divisiontag tekshiruvi: bitta sinfda bitta bo'linish ---
        var classIds = new List<int>();
        var classTags = new List<int>();
        foreach (var gid in l.GroupIds)
        {
            var g = groups[gid];
            int idx = classIds.IndexOf(g.ClassId);
            if (idx < 0)
            {
                classIds.Add(g.ClassId);
                classTags.Add(g.DivisionTag);
            }
            else if (classTags[idx] != g.DivisionTag)
            {
                throw new ArgumentException(
                    $"Dars '{l.Name}' (#{l.Id}) '{classes[g.ClassId].Name}' sinfida turli bo'linishlarni " +
                    $"(divisiontag {classTags[idx]} va {g.DivisionTag}) birlashtiryapti — bu mumkin emas.");
            }
        }

        int studentCount = l.StudentCount;
        if (studentCount <= 0)
        {
            foreach (var gid in l.GroupIds) studentCount += groups[gid].StudentCount;
            if (studentCount <= 0)
                foreach (var cid in classIds) studentCount += classes[cid].StudentCount;
        }

        // --- ruxsat etilgan xonalar: sig'im bo'yicha filtr (C-ROM-02) ---
        int[] allowedRooms = l.AllowedRoomIds;
        if (allowedRooms.Length > 0 && UseRoomCapacities && studentCount > 0)
        {
            allowedRooms = allowedRooms
                .Where(r => rooms[r].Capacity <= 0 || rooms[r].Capacity >= studentCount)
                .ToArray();
        }

        // --- karta uzunliklari ---
        var lengths = new List<int>();
        int remaining = l.PeriodsPerWeek;
        while (remaining >= l.PeriodsPerCard && l.PeriodsPerCard > 0)
        {
            lengths.Add(l.PeriodsPerCard);
            remaining -= l.PeriodsPerCard;
        }
        if (remaining > 0) lengths.Add(remaining);

        // --- kunlar cheklovi (C-CYC-03) ---
        SlotMask dayMask = l.AllowedDays is null ? _grid.FullMask : _grid.MaskForDays(l.AllowedDays);

        // --- resurs mavjudligi (C-AVL-01..05: qizil ✗ hard) ---
        SlotMask allowedSlots = _grid.FullMask;
        SlotMask questioned = SlotMask.Empty;
        foreach (var t in l.TeacherIds)
        {
            allowedSlots = allowedSlots.AndNot(teachers[t].Availability.Forbidden);
            questioned |= teachers[t].Availability.Questioned;
        }
        foreach (var cid in classIds)
        {
            allowedSlots = allowedSlots.AndNot(classes[cid].Availability.Forbidden);
            questioned |= classes[cid].Availability.Questioned;
        }
        foreach (var gid in l.GroupIds)
        {
            allowedSlots = allowedSlots.AndNot(groups[gid].Availability.Forbidden);
            questioned |= groups[gid].Availability.Questioned;
        }
        var subj = subjects[l.SubjectId];
        allowedSlots = allowedSlots.AndNot(subj.Availability.Forbidden);
        questioned |= subj.Availability.Questioned;

        if (allowedRooms.Length > 0)
        {
            // Kamida bitta ruxsat etilgan xona shu slotda ochiq bo'lishi kerak (C-AVL-04).
            SlotMask anyRoom = SlotMask.Empty;
            foreach (var r in allowedRooms) anyRoom |= _grid.FullMask.AndNot(rooms[r].Availability.Forbidden);
            allowedSlots &= anyRoom;
        }

        allowedSlots &= dayMask;

        foreach (var len in lengths)
        {
            var card = new Card
            {
                Id = outCards.Count,
                LessonId = l.Id,
                SubjectId = l.SubjectId,
                Length = len,
                TeacherIds = l.TeacherIds,
                GroupIds = l.GroupIds,
                ClassIds = classIds.ToArray(),
                ClassDivisionTags = classTags.ToArray(),
                AllowedRoomIds = allowedRooms,
                StudentCount = studentCount,
                QuestionMarked = questioned,
                SkipDistribution = l.SkipDistribution,
            };

            // C-DBL-01: len ta ketma-ket soat bitta kun ichida bo'lishi shart.
            var starts = Erode(allowedSlots, len, _grid.SlotCount) & _grid.StartMaskForLength(len);
            card.BaseDomain = starts;
            card.Domain = starts;
            outCards.Add(card);
        }

        // --- C-GBL-06: qulflangan kartalar ---
        var lessonCards = outCards.Skip(outCards.Count - lengths.Count).ToArray();
        for (int i = 0; i < l.Locked.Count && i < lessonCards.Length; i++)
        {
            var fp = l.Locked[i];
            int slot = _grid.SlotOf(fp.DayIndex, fp.Period);
            var c = lessonCards[i];
            c.IsLocked = true;
            c.LockedSlot = slot;
            c.LockedRoom = fp.RoomId;
            c.BaseDomain = SlotMask.Empty.Set(slot);
            c.Domain = c.BaseDomain;
        }
    }

    /// <summary>
    /// <c>len</c> uzunlikdagi kartaning boshlanish nuqtalari: s..s+len-1 barcha slotlari ruxsat etilgan bo'lishi kerak.
    /// </summary>
    internal static SlotMask Erode(SlotMask allowed, int len, int slotCount)
    {
        if (len <= 1) return allowed;
        var r = SlotMask.Empty;
        for (int s = 0; s + len <= slotCount; s++)
        {
            bool ok = true;
            for (int i = 0; i < len; i++)
            {
                if (!allowed.Test(s + i)) { ok = false; break; }
            }
            if (ok) r = r.Set(s);
        }
        return r;
    }

    private static void ComputeDegrees(Problem p)
    {
        foreach (var c in p.Cards)
        {
            int deg = 0;
            foreach (var t in c.TeacherIds) deg += p.CardsOfTeacher[t].Length - 1;
            foreach (var g in c.GroupIds) deg += p.CardsOfGroup[g].Length - 1;
            foreach (var cl in c.ClassIds) deg += p.CardsOfClass[cl].Length - 1;
            c.Degree = deg;
        }
    }
}
