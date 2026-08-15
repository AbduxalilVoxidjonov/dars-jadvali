namespace DarsJadvali.Application.Scheduling;

/// <summary>
/// EF <c>int</c> kaliti ↔ algoritm yadrosidagi <b>zich indeks</b> (0..N-1) ikki tomonlama xaritasi.
/// </summary>
/// <remarks>
/// Yadro barcha resurslarni massiv indeksi sifatida ko'radi (00 §6.3), shuning uchun
/// har bir resurs turi uchun alohida shunday xarita kerak. Xarita <b>faqat qo'shish</b>
/// bilan to'ldiriladi va tartib barqaror (EF <c>Id</c> o'sish tartibi) —
/// shu tufayli bir xil baza + bir xil urug' har doim bir xil natija beradi.
/// </remarks>
public sealed class DenseIdMap
{
    private readonly List<int> _toDbId = new();
    private readonly Dictionary<int, int> _toIndex = new();

    /// <summary>Xato xabarlarida ishlatiladigan resurs nomi.</summary>
    public DenseIdMap(string resourceName) => ResourceName = resourceName;

    /// <summary>Resurs nomi (o'zbekcha), masalan "O'qituvchi".</summary>
    public string ResourceName { get; }

    /// <summary>Xaritadagi elementlar soni.</summary>
    public int Count => _toDbId.Count;

    /// <summary>Indekslar tartibida EF kalitlari.</summary>
    public IReadOnlyList<int> DbIds => _toDbId;

    /// <summary>EF kalitini qo'shadi (yoki mavjudini qaytaradi) va yadro indeksini beradi.</summary>
    public int Add(int dbId)
    {
        if (_toIndex.TryGetValue(dbId, out var existing)) return existing;

        var index = _toDbId.Count;
        _toDbId.Add(dbId);
        _toIndex[dbId] = index;
        return index;
    }

    /// <summary>EF kaliti xaritada bormi.</summary>
    public bool ContainsDbId(int dbId) => _toIndex.ContainsKey(dbId);

    /// <summary>EF kaliti bo'yicha yadro indeksini topadi.</summary>
    public bool TryIndexOf(int dbId, out int index) => _toIndex.TryGetValue(dbId, out index);

    /// <summary>EF kaliti bo'yicha yadro indeksi. Topilmasa tushunarli xato.</summary>
    public int IndexOf(int dbId) => _toIndex.TryGetValue(dbId, out var index)
        ? index
        : throw new SchedulingMappingException(
            $"{ResourceName} (ID: {dbId}) jadval ma'lumotlari orasida topilmadi.");

    /// <summary>Yadro indeksi bo'yicha EF kaliti. Chegaradan chiqsa tushunarli xato.</summary>
    public int DbIdOf(int index) => index >= 0 && index < _toDbId.Count
        ? _toDbId[index]
        : throw new SchedulingMappingException(
            $"{ResourceName} uchun ichki indeks {index} noto'g'ri (jami {_toDbId.Count} ta).");
}

/// <summary>
/// Dars xaritasi: bitta EF <c>Lesson</c> ga <b>bir nechta</b> yadro darsi to'g'ri kelishi
/// mumkin — A/B haftada har hafta uchun alohida (shunda "haftasiga N soat" HAR haftada
/// bajariladi, hammasi bitta haftaga to'planib qolmaydi).
/// </summary>
public sealed class LessonIndexMap
{
    private readonly List<int> _dbIdOfIndex = new();
    private readonly Dictionary<int, List<int>> _indexesOfDbId = new();

    /// <summary>Yadro darslari soni.</summary>
    public int Count => _dbIdOfIndex.Count;

    /// <summary>Yangi yadro darsini qo'shadi va uning indeksini qaytaradi.</summary>
    public int Add(int dbId)
    {
        var index = _dbIdOfIndex.Count;
        _dbIdOfIndex.Add(dbId);

        if (!_indexesOfDbId.TryGetValue(dbId, out var list))
        {
            list = new List<int>();
            _indexesOfDbId[dbId] = list;
        }

        list.Add(index);
        return index;
    }

    /// <summary>Yadro indeksi → EF <c>Lesson.Id</c>.</summary>
    public int DbIdOf(int index) => index >= 0 && index < _dbIdOfIndex.Count
        ? _dbIdOfIndex[index]
        : throw new SchedulingMappingException(
            $"Dars uchun ichki indeks {index} noto'g'ri (jami {_dbIdOfIndex.Count} ta).");

    /// <summary>EF <c>Lesson.Id</c> → birinchi yadro indeksi.</summary>
    public int IndexOf(int dbId) => _indexesOfDbId.TryGetValue(dbId, out var list) && list.Count > 0
        ? list[0]
        : throw new SchedulingMappingException($"Dars (ID: {dbId}) masalada topilmadi.");

    /// <summary>EF <c>Lesson.Id</c> → barcha yadro indekslari (haftalar bo'yicha).</summary>
    public IReadOnlyList<int> IndexesOf(int dbId) =>
        _indexesOfDbId.TryGetValue(dbId, out var list) ? list : Array.Empty<int>();

    /// <summary>EF kaliti xaritada bormi.</summary>
    public bool ContainsDbId(int dbId) => _indexesOfDbId.ContainsKey(dbId);

    /// <summary>EF kaliti bo'yicha birinchi indeksni topadi.</summary>
    public bool TryIndexOf(int dbId, out int index)
    {
        if (_indexesOfDbId.TryGetValue(dbId, out var list) && list.Count > 0)
        {
            index = list[0];
            return true;
        }

        index = -1;
        return false;
    }
}

/// <summary>
/// Bitta generatsiya seansi uchun barcha kalit xaritalari va vaqt o'lchamlari.
/// </summary>
public sealed class SchedulingIdMap
{
    /// <summary>O'qituvchi: <c>Teacher.Id</c> ↔ indeks.</summary>
    public DenseIdMap Teachers { get; } = new("O'qituvchi");

    /// <summary>Sinf: <c>SchoolClass.Id</c> ↔ indeks.</summary>
    public DenseIdMap Classes { get; } = new("Sinf");

    /// <summary>Guruh: <c>StudentGroup.Id</c> ↔ indeks.</summary>
    public DenseIdMap Groups { get; } = new("Guruh");

    /// <summary>Xona: <c>Classroom.Id</c> ↔ indeks.</summary>
    public DenseIdMap Rooms { get; } = new("Xona");

    /// <summary>Fan: <c>Subject.Id</c> ↔ indeks.</summary>
    public DenseIdMap Subjects { get; } = new("Fan");

    /// <summary>Dars ta'rifi: <c>Lesson.Id</c> ↔ indeks(lar) — A/B haftada 1:N.</summary>
    public LessonIndexMap Lessons { get; } = new();

    /// <summary>
    /// Dars soati: <c>Period.Id</c> ↔ panjaradagi soat indeksi (0-based).
    /// <c>PeriodNo</c> smenalar bo'ylab uzluksiz bo'lgani uchun indeks ham uzluksiz.
    /// </summary>
    public DenseIdMap Periods { get; } = new("Dars soati");

    /// <summary>Soat indeksi → <c>Period.PeriodNo</c>.</summary>
    public int[] PeriodNoOfIndex { get; internal set; } = Array.Empty<int>();

    /// <summary><c>Period.PeriodNo</c> → soat indeksi.</summary>
    public Dictionary<int, int> IndexOfPeriodNo { get; } = new();

    /// <summary>Haftadagi kun o'rinlari soni (panjara kengligi).</summary>
    public int DaysPerWeek { get; internal set; } = 1;

    /// <summary>Sikldagi haftalar soni (A/B hafta uchun 2).</summary>
    public int Weeks { get; internal set; } = 1;

    /// <summary>Bir kundagi dars soatlari soni.</summary>
    public int PeriodCount { get; internal set; }

    /// <summary>Faol kun raqamlari (<c>WorkDay.DayNo</c>).</summary>
    public IReadOnlyList<int> ActiveDayNumbers { get; internal set; } = Array.Empty<int>();

    /// <summary>(hafta, kun raqami) → panjaradagi kun indeksi.</summary>
    public int DayIndexOf(int weekNo, int dayNo) => weekNo * DaysPerWeek + dayNo;

    /// <summary>Panjaradagi kun indeksi → <c>Card.DayNo</c>.</summary>
    public int DayNoOf(int dayIndex) => dayIndex % DaysPerWeek;

    /// <summary>Panjaradagi kun indeksi → hafta raqami (0-based).</summary>
    public int WeekNoOf(int dayIndex) => dayIndex / DaysPerWeek;

    /// <summary>
    /// Yadro kartasi ↔ bazadagi <c>Card.Id</c>. Natija yozilgandan keyin to'ldiriladi
    /// (yadro kartasi joylashmagan bo'lishi mumkin — u holda bazada qatori yo'q, 00 §6.3).
    /// </summary>
    public Dictionary<int, int> CardDbIds { get; } = new();
}

/// <summary>EF ma'lumotini yadro modeliga o'girishda chiqadigan xato (foydalanuvchiga ko'rsatiladi).</summary>
public sealed class SchedulingMappingException : Exception
{
    public SchedulingMappingException(string message) : base(message) { }

    public SchedulingMappingException(string message, Exception inner) : base(message, inner) { }
}
