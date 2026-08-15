using System.Globalization;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>Jadval qamrovi — sarlavha matni va standart dizaynni tanlashda ishlatiladi.</summary>
public enum PrintScope
{
    /// <summary>Bitta sinf jadvali.</summary>
    Class = 0,

    /// <summary>Bitta o'qituvchi jadvali.</summary>
    Teacher = 1,

    /// <summary>Bitta xona jadvali.</summary>
    Room = 2,

    /// <summary>Butun maktab — barcha sinflar.</summary>
    School = 3,
}

/// <summary>
/// Jadvaldagi bitta ustun — ish kuni.
/// </summary>
/// <param name="Index">0 dan boshlanuvchi tartib raqami (kartadagi <see cref="PrintableCard.DayIndex"/> shunga ishora qiladi).</param>
/// <param name="Name">To'liq nom, masalan "Dushanba".</param>
/// <param name="ShortName">Qisqartma, masalan "Du". Bo'lmasa <c>null</c>.</param>
public sealed record PrintableDay(int Index, string Name, string? ShortName = null)
{
    /// <summary>Tor ustunda ishlatiladigan nom.</summary>
    public string DisplayShort => string.IsNullOrWhiteSpace(ShortName) ? Name : ShortName!;
}

/// <summary>
/// Jadvaldagi bitta qator — dars soati.
/// </summary>
/// <remarks>
/// <b>Ikki smena</b>: soat raqamlari UZLUKSIZ bo'ladi (1-smena 1..6, 2-smena 7..12) va
/// smena nomi <paramref name="ShiftName"/> da beriladi. Renderer bir xil
/// <paramref name="ShiftName"/> ga ega ketma-ket qatorlarni bitta smena polosasi
/// sifatida belgilaydi — shuning uchun 7-soat "2-smenaning 1-darsi" emas, aynan
/// "7" bo'lib qoladi va sinflar orasida taqqoslash buzilmaydi.
/// </remarks>
/// <param name="Number">Uzluksiz soat raqami (1..N).</param>
/// <param name="Label">Ko'rinadigan yozuv, masalan "7".</param>
/// <param name="TimeLabel">Vaqt oralig'i "13:30-14:15". Sozlanmagan bo'lsa <c>null</c>.</param>
/// <param name="ShiftName">Smena nomi, masalan "2-smena". Bitta smena bo'lsa <c>null</c>.</param>
public sealed record PrintablePeriod(
    int Number,
    string Label,
    string? TimeLabel = null,
    string? ShiftName = null);

/// <summary>
/// Chop etiladigan bitta dars kartasi — chop etish dvigatelining YAGONA dars birligi.
/// </summary>
/// <remarks>
/// Bu tur <b>ataylab</b> entity emas: <c>ScheduleEntry</c> dan <c>Card</c> ga o'tish
/// chop etish kodiga umuman tegmasligi kerak. Entity bilan bog'lanish faqat
/// <see cref="ScheduleEntryPrintableAdapter"/> faylida.
/// </remarks>
public sealed record PrintableCard
{
    /// <summary>Faqat A haftada.</summary>
    public const int WeekA = 1;

    /// <summary>Faqat B haftada.</summary>
    public const int WeekB = 2;

    /// <summary>Har hafta (A va B).</summary>
    public const int AllWeeks = WeekA | WeekB;

    /// <summary>Fan nomi — katakning birinchi qatori.</summary>
    public required string SubjectName { get; init; }

    /// <summary>Fan qisqartmasi ("MAT"). Tor katakda shu ishlatiladi.</summary>
    public string? SubjectShortName { get; init; }

    /// <summary>O'qituvchi(lar). Bir nechta bo'lsa — birgalikda o'tiladigan dars.</summary>
    public IReadOnlyList<string> TeacherNames { get; init; } = Array.Empty<string>();

    /// <summary>Sinf nomi ("5-A"). O'qituvchi jadvalida katakda shu ko'rinadi.</summary>
    public string? ClassName { get; init; }

    /// <summary>
    /// Sinf ichidagi bo'linma nomi ("1-guruh", "Qizlar"). Butun sinf darsida <c>null</c>.
    /// Bitta katakda bir nechta guruh bo'lsa — ular YONMA-YON chiziladi.
    /// </summary>
    public string? GroupName { get; init; }

    /// <summary>Xona raqami/nomi.</summary>
    public string? RoomName { get; init; }

    /// <summary>Kun ustuni indeksi (<see cref="PrintableDay.Index"/>).</summary>
    public required int DayIndex { get; init; }

    /// <summary>Boshlanish soati — uzluksiz raqam (<see cref="PrintablePeriod.Number"/>).</summary>
    public required int Period { get; init; }

    /// <summary>Dars uzunligi soatda. 2 — juft dars (yaxlit blok bo'lib chiziladi).</summary>
    public int Length { get; init; } = 1;

    /// <summary>Hafta maskasi: 1 = A, 2 = B, 3 = har hafta.</summary>
    public int WeeksMask { get; init; } = AllWeeks;

    /// <summary>Katak foni uchun rang "#RRGGBB". Bo'lmasa dizayn rangi olinadi.</summary>
    public string? ColorCode { get; init; }

    /// <summary>Har hafta o'tiladimi (A/B belgisi kerak emasmi).</summary>
    public bool IsEveryWeek => WeeksMask == 0 || (WeeksMask & AllWeeks) == AllWeeks;

    /// <summary>Katakda ko'rinadigan hafta belgisi: "A", "B" yoki <c>null</c>.</summary>
    public string? WeekLabel => IsEveryWeek
        ? null
        : (WeeksMask & WeekA) != 0 ? "A" : (WeeksMask & WeekB) != 0 ? "B" : null;

    /// <summary>Karta egallagan oxirgi soat raqami.</summary>
    public int EndPeriod => Period + Math.Max(1, Length) - 1;

    /// <summary>Tor katakda ishlatiladigan fan nomi.</summary>
    public string DisplaySubject =>
        string.IsNullOrWhiteSpace(SubjectShortName) ? SubjectName : SubjectShortName!;

    /// <summary>O'qituvchilar bitta qatorda.</summary>
    public string TeacherLine => string.Join(", ", TeacherNames.Where(t => !string.IsNullOrWhiteSpace(t)));
}

/// <summary>
/// Bitta to'r (grid) — sinf jadvalida bitta, maktab jadvalida har sinf uchun bittadan.
/// </summary>
/// <param name="Caption">To'r sarlavhasi: sinf nomi yoki o'qituvchi ismi.</param>
/// <param name="SubCaption">Qo'shimcha qator: sinf rahbari, xona, soat soni.</param>
/// <param name="Cards">Shu to'rdagi kartalar.</param>
public sealed record PrintableSection(
    string Caption,
    string? SubCaption,
    IReadOnlyList<PrintableCard> Cards);

/// <summary>
/// Chop etish dvigatelining KIRISH modeli. Renderer boshqa hech narsani bilmaydi.
/// </summary>
public sealed record PrintableTimetable
{
    /// <summary>Maktab nomi — <c>{School.Name}</c> tokeni.</summary>
    public string? SchoolName { get; init; }

    /// <summary>O'quv yili "2025/2026" — <c>{AcademicYear}</c>.</summary>
    public string? AcademicYear { get; init; }

    /// <summary>Chorak/semestr — <c>{Term}</c>.</summary>
    public string? Term { get; init; }

    /// <summary>Qamrov turi.</summary>
    public PrintScope Scope { get; init; } = PrintScope.Class;

    /// <summary>Qamrov nomi: sinf nomi, o'qituvchi ismi yoki maktab nomi — <c>{Scope.Name}</c>.</summary>
    public string? ScopeName { get; init; }

    /// <summary>Ustunlar — faol ish kunlari.</summary>
    public IReadOnlyList<PrintableDay> Days { get; init; } = Array.Empty<PrintableDay>();

    /// <summary>Qatorlar — dars soatlari (uzluksiz raqamlangan).</summary>
    public IReadOnlyList<PrintablePeriod> Periods { get; init; } = Array.Empty<PrintablePeriod>();

    /// <summary>To'rlar.</summary>
    public IReadOnlyList<PrintableSection> Sections { get; init; } = Array.Empty<PrintableSection>();

    /// <summary>Hujjat sanasi — <c>{Date}</c>.</summary>
    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    /// <summary>Ko'rsatadigan narsa bormi.</summary>
    public bool IsEmpty =>
        Days.Count == 0 || Periods.Count == 0 || Sections.Count == 0 ||
        Sections.All(s => s.Cards.Count == 0);

    /// <summary>Bo'sh jadval uchun matn.</summary>
    public const string EmptyMessage = "Hali dars qo'yilmagan";

    /// <summary>Barcha to'rlardagi kartalar.</summary>
    public IEnumerable<PrintableCard> AllCards => Sections.SelectMany(s => s.Cards);

    /// <summary>Soat raqami bo'yicha qator indeksini beradi (topilmasa -1).</summary>
    public int PeriodIndexOf(int periodNumber)
    {
        for (var i = 0; i < Periods.Count; i++)
        {
            if (Periods[i].Number == periodNumber)
                return i;
        }

        return -1;
    }

    /// <summary>Smenalar ro'yxati (tartib buzilmagan holda, takrorsiz).</summary>
    public IReadOnlyList<string> ShiftNames
    {
        get
        {
            var result = new List<string>();
            foreach (var period in Periods)
            {
                if (!string.IsNullOrWhiteSpace(period.ShiftName) &&
                    (result.Count == 0 || !string.Equals(result[^1], period.ShiftName, StringComparison.Ordinal)))
                {
                    result.Add(period.ShiftName!);
                }
            }

            return result;
        }
    }

    /// <summary>Sarlavhada ishlatiladigan qamrov tavsifi.</summary>
    public string ScopeTitle => Scope switch
    {
        PrintScope.Class => string.IsNullOrWhiteSpace(ScopeName) ? "Sinf jadvali" : $"{ScopeName} sinf dars jadvali",
        PrintScope.Teacher => string.IsNullOrWhiteSpace(ScopeName) ? "O'qituvchi jadvali" : $"{ScopeName} — dars jadvali",
        PrintScope.Room => string.IsNullOrWhiteSpace(ScopeName) ? "Xona jadvali" : $"{ScopeName} xona jadvali",
        _ => "Maktab dars jadvali",
    };

    /// <summary>Sanani <c>{Date}</c> tokeni uchun formatlaydi.</summary>
    public string FormattedDate => GeneratedAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}
