namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// Generatsiya davomidagi o'zgaruvchan holat: kartalarning joylashuvi + resurs bandlik bitmask'lari.
/// Barcha hard cheklovlar (C-GBL-01/02/03/08, C-AVL-01..05, C-ROM-01/02, C-DBL-01, C-GBL-06)
/// shu yerda O(1) tekshiriladi — <see cref="CanPlace"/>.
/// </summary>
public sealed class SolutionState
{
    private readonly Problem _p;
    private readonly int _slots;

    private readonly int[] _cardSlot;      // -1 = joylanmagan
    private readonly int[] _cardRoom;      // -1 = xona yo'q

    private readonly SlotMask[] _teacherBusy;
    private readonly SlotMask[] _groupBusy;
    private readonly SlotMask[] _classBusy;
    private readonly SlotMask[] _roomBusy;

    // divisiontag semantikasi: har (sinf, slot) uchun faol bo'linish tag'i va sanoq.
    private readonly int[] _classTagAt;
    private readonly short[] _classCountAt;
    private readonly short[] _roomLoadAt;

    private int _placedCount;

    // [uzunlik][slot] -> ketma-ket bitlar maskasi (qayta-qayta qurmaslik uchun oldindan hisoblangan).
    private readonly SlotMask[][] _extents;

    public SolutionState(Problem problem)
    {
        _p = problem;
        _slots = problem.Grid.SlotCount;

        int maxLen = 1;
        foreach (var c in problem.Cards) if (c.Length > maxLen) maxLen = c.Length;
        _extents = new SlotMask[maxLen + 1][];
        for (int len = 1; len <= maxLen; len++)
        {
            var arr = new SlotMask[_slots];
            for (int s = 0; s < _slots; s++)
                arr[s] = SlotMask.Range(s, Math.Min(len, _slots - s));
            _extents[len] = arr;
        }

        _cardSlot = new int[problem.Cards.Length];
        _cardRoom = new int[problem.Cards.Length];
        Array.Fill(_cardSlot, -1);
        Array.Fill(_cardRoom, -1);

        _teacherBusy = new SlotMask[problem.Teachers.Length];
        _groupBusy = new SlotMask[problem.Groups.Length];
        _classBusy = new SlotMask[problem.Classes.Length];
        _roomBusy = new SlotMask[problem.Rooms.Length];

        _classTagAt = new int[problem.Classes.Length * _slots];
        Array.Fill(_classTagAt, -1);
        _classCountAt = new short[problem.Classes.Length * _slots];
        _roomLoadAt = new short[Math.Max(1, problem.Rooms.Length) * _slots];
    }

    /// <summary>[slot, slot+len) ketma-ket bitlar maskasi.</summary>
    public SlotMask Extent(int slot, int length) => _extents[length][slot];

    public Problem Problem => _p;
    public TimeGrid Grid => _p.Grid;
    public int CardCount => _cardSlot.Length;
    public int PlacedCount => _placedCount;
    public int UnplacedCount => _cardSlot.Length - _placedCount;

    public int SlotOfCard(int cardId) => _cardSlot[cardId];
    public int RoomOfCard(int cardId) => _cardRoom[cardId];
    public bool IsPlaced(int cardId) => _cardSlot[cardId] >= 0;

    public SlotMask TeacherBusy(int teacherId) => _teacherBusy[teacherId];
    public SlotMask GroupBusy(int groupId) => _groupBusy[groupId];
    public SlotMask ClassBusy(int classId) => _classBusy[classId];
    public SlotMask RoomBusy(int roomId) => _roomBusy[roomId];

    /// <summary>Berilgan sinfda berilgan slotdagi faol bo'linish tag'i (-1 = bo'sh).</summary>
    public int ClassTagAt(int classId, int slot) => _classTagAt[classId * _slots + slot];

    /// <summary>O'qituvchining bir kundagi band-bitlari.</summary>
    public ulong TeacherDayBits(int teacherId, int dayIndex)
        => _teacherBusy[teacherId].Extract(_p.Grid.DayStart(dayIndex), _p.Grid.Periods);

    /// <summary>Sinfning bir kundagi band-bitlari.</summary>
    public ulong ClassDayBits(int classId, int dayIndex)
        => _classBusy[classId].Extract(_p.Grid.DayStart(dayIndex), _p.Grid.Periods);

    /// <summary>Guruhning bir kundagi band-bitlari.</summary>
    public ulong GroupDayBits(int groupId, int dayIndex)
        => _groupBusy[groupId].Extract(_p.Grid.DayStart(dayIndex), _p.Grid.Periods);

    // ------------------------------------------------------------------
    // Hard feasibility
    // ------------------------------------------------------------------

    /// <summary>
    /// Kartani <paramref name="slot"/> ga (va <paramref name="roomId"/> xonasiga) qo'yish mumkinmi.
    /// C-GBL-01 (teacher), C-GBL-02 (class/group), C-GBL-03 (room), C-GBL-08 (division),
    /// C-AVL-01..05 va C-DBL-01 (domain orqali), C-ROM-01/02.
    /// </summary>
    public bool CanPlace(Card card, int slot, int roomId)
    {
        if (slot < 0 || !card.Domain.Test(slot)) return false;
        if (card.IsLocked && slot != card.LockedSlot) return false;

        var extent = Extent(slot, card.Length);

        foreach (var t in card.TeacherIds)
            if (_teacherBusy[t].Intersects(extent)) return false;          // C-GBL-01

        foreach (var g in card.GroupIds)
            if (_groupBusy[g].Intersects(extent)) return false;            // C-GBL-02

        // C-GBL-08: turli divisiontag'lar bir slotda bo'la olmaydi.
        for (int ci = 0; ci < card.ClassIds.Length; ci++)
        {
            int cls = card.ClassIds[ci];
            int tag = card.ClassDivisionTags[ci];
            int baseIdx = cls * _slots;
            for (int i = 0; i < card.Length; i++)
            {
                int idx = baseIdx + slot + i;
                if (_classCountAt[idx] > 0 && _classTagAt[idx] != tag) return false;
            }
        }

        if (card.NeedsRoom)
        {
            if (roomId < 0) return false;                                   // C-GBL-07
            if (Array.IndexOf(card.AllowedRoomIds, roomId) < 0) return false; // C-ROM-01
            var room = _p.Rooms[roomId];
            if (_p.UseRoomCapacities && room.Capacity > 0 && card.StudentCount > room.Capacity)
                return false;                                               // C-ROM-02
            int baseIdx = roomId * _slots;
            for (int i = 0; i < card.Length; i++)
                if (_roomLoadAt[baseIdx + slot + i] >= room.ParallelLessons) return false; // C-GBL-03 / C-ROM-05
        }

        return true;
    }

    /// <summary>Kartaga <paramref name="slot"/> uchun mos bo'sh xona topadi (-1 = topilmadi / kerak emas).</summary>
    public int FindRoom(Card card, int slot, int preferred = -1)
    {
        if (!card.NeedsRoom) return -1;
        if (card.IsLocked && card.LockedRoom >= 0)
            return CanPlace(card, slot, card.LockedRoom) ? card.LockedRoom : -1;

        if (preferred >= 0 && Array.IndexOf(card.AllowedRoomIds, preferred) >= 0 && RoomFree(card, slot, preferred))
            return preferred;

        foreach (var r in card.AllowedRoomIds)
            if (RoomFree(card, slot, r)) return r;
        return -1;
    }

    /// <summary>Xona shu karta uchun shu slotda bo'shmi (C-ROM-01/02/05).</summary>
    public bool IsRoomAvailable(Card card, int slot, int roomId) => RoomFree(card, slot, roomId);

    private bool RoomFree(Card card, int slot, int roomId)
    {
        var room = _p.Rooms[roomId];
        if (_p.UseRoomCapacities && room.Capacity > 0 && card.StudentCount > room.Capacity) return false;
        if (room.Availability.Forbidden.Intersects(Extent(slot, card.Length))) return false;
        int baseIdx = roomId * _slots;
        for (int i = 0; i < card.Length; i++)
            if (_roomLoadAt[baseIdx + slot + i] >= room.ParallelLessons) return false;
        return true;
    }

    /// <summary>Kartaning joriy holatida joylashtirish mumkin bo'lgan barcha slotlar.</summary>
    public SlotMask FeasibleSlots(Card card)
    {
        var res = SlotMask.Empty;
        for (int s = card.Domain.FirstSet(); s >= 0; s = card.Domain.FirstSet(s + 1))
        {
            int room = card.NeedsRoom ? FindRoom(card, s) : -1;
            if (card.NeedsRoom && room < 0) continue;
            if (CanPlace(card, s, room)) res = res.Set(s);
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Place / Unplace
    // ------------------------------------------------------------------

    public void Place(Card card, int slot, int roomId)
    {
        var extent = Extent(slot, card.Length);

        foreach (var t in card.TeacherIds) _teacherBusy[t] |= extent;
        foreach (var g in card.GroupIds) _groupBusy[g] |= extent;

        for (int ci = 0; ci < card.ClassIds.Length; ci++)
        {
            int cls = card.ClassIds[ci];
            int tag = card.ClassDivisionTags[ci];
            int baseIdx = cls * _slots;
            for (int i = 0; i < card.Length; i++)
            {
                int idx = baseIdx + slot + i;
                if (_classCountAt[idx] == 0) _classTagAt[idx] = tag;
                _classCountAt[idx]++;
            }
            _classBusy[cls] |= extent;
        }

        if (roomId >= 0)
        {
            _roomBusy[roomId] |= extent;
            int baseIdx = roomId * _slots;
            for (int i = 0; i < card.Length; i++) _roomLoadAt[baseIdx + slot + i]++;
        }

        _cardSlot[card.Id] = slot;
        _cardRoom[card.Id] = roomId;
        _placedCount++;
    }

    public void Unplace(Card card)
    {
        int slot = _cardSlot[card.Id];
        if (slot < 0) return;
        int roomId = _cardRoom[card.Id];
        var extent = Extent(slot, card.Length);
        var notExtent = ~extent;

        foreach (var t in card.TeacherIds) _teacherBusy[t] &= notExtent;
        foreach (var g in card.GroupIds) _groupBusy[g] &= notExtent;

        for (int ci = 0; ci < card.ClassIds.Length; ci++)
        {
            int cls = card.ClassIds[ci];
            int baseIdx = cls * _slots;
            for (int i = 0; i < card.Length; i++)
            {
                int idx = baseIdx + slot + i;
                _classCountAt[idx]--;
                if (_classCountAt[idx] == 0)
                {
                    _classTagAt[idx] = -1;
                    _classBusy[cls] = _classBusy[cls].Clear(slot + i);
                }
            }
        }

        if (roomId >= 0)
        {
            int baseIdx = roomId * _slots;
            for (int i = 0; i < card.Length; i++)
            {
                _roomLoadAt[baseIdx + slot + i]--;
                if (_roomLoadAt[baseIdx + slot + i] == 0)
                    _roomBusy[roomId] = _roomBusy[roomId].Clear(slot + i);
            }
        }

        _cardSlot[card.Id] = -1;
        _cardRoom[card.Id] = -1;
        _placedCount--;
    }

    // ------------------------------------------------------------------
    // Move apply / undo
    // ------------------------------------------------------------------

    /// <summary>
    /// Harakatni qo'llaydi. Agar biror karta joylashmasa — holat tugilgan holiga qaytariladi va <c>false</c>.
    /// Hard invariant (T-A-04) shu funksiya orqali kafolatlanadi.
    /// </summary>
    public bool TryApply(Move m)
    {
        for (int i = 0; i < m.Count; i++)
            Unplace(_p.Cards[m.CardId(i)]);

        int placed = 0;
        for (; placed < m.Count; placed++)
        {
            var card = _p.Cards[m.CardId(placed)];
            int slot = m.ToSlot(placed);
            if (slot < 0) continue;
            int room = m.ToRoom(placed);
            if (card.NeedsRoom && room < 0)
            {
                room = FindRoom(card, slot);
                if (room < 0) break;
                m.SetToRoom(placed, room);
            }
            if (!CanPlace(card, slot, room)) break;
            Place(card, slot, room);
        }

        if (placed < m.Count)
        {
            // rollback
            for (int i = 0; i < placed; i++)
            {
                int slot = m.ToSlot(i);
                if (slot >= 0) Unplace(_p.Cards[m.CardId(i)]);
            }
            for (int i = 0; i < m.Count; i++)
            {
                int from = m.FromSlot(i);
                if (from >= 0) Place(_p.Cards[m.CardId(i)], from, m.FromRoom(i));
            }
            return false;
        }
        return true;
    }

    /// <summary>Qo'llanilgan harakatni bekor qiladi (T-A-03: bayt-bayt asl holat).</summary>
    public void Undo(Move m)
    {
        for (int i = 0; i < m.Count; i++)
        {
            if (m.ToSlot(i) >= 0) Unplace(_p.Cards[m.CardId(i)]);
        }
        for (int i = 0; i < m.Count; i++)
        {
            int from = m.FromSlot(i);
            if (from >= 0) Place(_p.Cards[m.CardId(i)], from, m.FromRoom(i));
        }
    }

    // ------------------------------------------------------------------
    // Snapshot / restore
    // ------------------------------------------------------------------

    public Solution Snapshot()
    {
        var slots = new int[_cardSlot.Length];
        var rooms = new int[_cardRoom.Length];
        Array.Copy(_cardSlot, slots, slots.Length);
        Array.Copy(_cardRoom, rooms, rooms.Length);
        return new Solution(_p, slots, rooms);
    }

    public void RestoreFrom(Solution s)
    {
        ClearAll();
        for (int i = 0; i < _cardSlot.Length; i++)
        {
            int slot = s.CardSlots[i];
            if (slot >= 0) Place(_p.Cards[i], slot, s.CardRooms[i]);
        }
    }

    public void ClearAll()
    {
        for (int i = 0; i < _cardSlot.Length; i++)
            if (_cardSlot[i] >= 0) Unplace(_p.Cards[i]);
    }

    /// <summary>Qulflangan kartalarni joylashtiradi (C-GBL-06). false = qulf ziddiyatli.</summary>
    public bool PlaceLockedCards()
    {
        foreach (var c in _p.Cards)
        {
            if (!c.IsLocked || _cardSlot[c.Id] >= 0) continue;
            int room = c.LockedRoom >= 0 ? c.LockedRoom : FindRoom(c, c.LockedSlot);
            if (!CanPlace(c, c.LockedSlot, room)) return false;
            Place(c, c.LockedSlot, room);
        }
        return true;
    }
}
