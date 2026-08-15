namespace DarsJadvali.Scheduling.Model;

/// <summary>Neighborhood harakat turi (02-asc-.., 4.7).</summary>
public enum MoveKind
{
    SingleMove = 0,
    Swap = 1,
    RoomChange = 2,
    BlockSwap = 3,
    KempeChain = 4,
    Ejection = 5,
}

/// <summary>
/// Harakat — bir yoki bir necha kartaning ko'chirilishi.
/// Qayta ishlatiladigan obyekt (heap allocation local search ichida nolga tushiriladi).
/// </summary>
public sealed class Move
{
    private int[] _cardIds = new int[8];
    private int[] _fromSlot = new int[8];
    private int[] _fromRoom = new int[8];
    private int[] _toSlot = new int[8];
    private int[] _toRoom = new int[8];

    public MoveKind Kind { get; private set; }
    public int Count { get; private set; }

    public int CardId(int i) => _cardIds[i];
    public int FromSlot(int i) => _fromSlot[i];
    public int FromRoom(int i) => _fromRoom[i];
    public int ToSlot(int i) => _toSlot[i];
    public int ToRoom(int i) => _toRoom[i];

    public void SetToRoom(int i, int room) => _toRoom[i] = room;

    public void Reset(MoveKind kind)
    {
        Kind = kind;
        Count = 0;
    }

    public void Add(int cardId, int fromSlot, int fromRoom, int toSlot, int toRoom)
    {
        if (Count == _cardIds.Length) Grow();
        _cardIds[Count] = cardId;
        _fromSlot[Count] = fromSlot;
        _fromRoom[Count] = fromRoom;
        _toSlot[Count] = toSlot;
        _toRoom[Count] = toRoom;
        Count++;
    }

    private void Grow()
    {
        int n = _cardIds.Length * 2;
        Array.Resize(ref _cardIds, n);
        Array.Resize(ref _fromSlot, n);
        Array.Resize(ref _fromRoom, n);
        Array.Resize(ref _toSlot, n);
        Array.Resize(ref _toRoom, n);
    }

    /// <summary>Tabu ro'yxati uchun imzo.</summary>
    public long Signature()
    {
        long h = (long)Kind;
        for (int i = 0; i < Count; i++)
            h = unchecked(h * 1000003L + _cardIds[i] * 977L + _toSlot[i]);
        return h;
    }
}
