using CommunityToolkit.Mvvm.ComponentModel;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.Models;

/// <summary>Kartaning semantik holati — rang emas (M-06).</summary>
/// <remarks>
/// Ranglarni <c>Converters/CardStateToBrushConverter</c> va <c>Styles/</c> resurslari hal qiladi.
/// </remarks>
public enum TimetableCardState
{
    /// <summary>Odatiy holat.</summary>
    Normal,

    /// <summary>Tanlangan karta.</summary>
    Selected,

    /// <summary>Karta "qo'lda" — kursorga yopishgan (aSc card-in-hand).</summary>
    InHand,

    /// <summary>Kartada ogohlantirish bor.</summary>
    Warning,

    /// <summary>Kartada to'qnashuv bor.</summary>
    Conflict,
}

/// <summary>Jadvaldagi bitta pozitsiya: kun + dars raqami (1 dan boshlanadi).</summary>
/// <param name="Day">Hafta kuni.</param>
/// <param name="Period">Dars raqami (1-based, legacy konvensiya).</param>
public readonly record struct SlotPosition(WeekDay Day, int Period)
{
    /// <summary>Ko'rsatish uchun qisqa matn.</summary>
    public override string ToString() => $"{Day.ToUzbek()} {Period}-soat";
}

/// <summary>
/// <b>UI tomonidagi karta modeli</b> — entity'dan butunlay ajratilgan.
/// </summary>
/// <remarks>
/// <para>
/// To'r, drag-drop va undo/redo <b>faqat shu modelga</b> tayanadi. Entity bilan bog'lanish
/// yagona <c>Services/Timetable/CardViewAdapter.cs</c> faylida jamlangan.
/// </para>
/// <para>
/// <see cref="Length"/> — juft dars (<c>Card.Length</c>), <see cref="WeeksMask"/> —
/// A/B hafta bitmaskasi (<c>Card.WeeksMask</c>), <see cref="GroupName"/> — sinf bo'linmasi
/// (<c>LessonGroup</c>), <see cref="IsLocked"/> — qulf (<c>Card.IsLocked</c>).
/// <b>Endi bularning hammasi haqiqiy maydonlardan keladi</b> — ilgari eski
/// <c>ScheduleEntry</c> da bu ustunlar yo'qligi sababli adapter standart qiymat berardi.
/// </para>
/// </remarks>
public sealed partial class TimetableCard : ObservableObject
{
    /// <summary>Har ikkala hafta (A va B).</summary>
    public const int AllWeeks = 0b11;

    /// <summary>Kun (joylashtirilmagan bo'lsa <c>null</c>).</summary>
    [ObservableProperty]
    private WeekDay? _day;

    /// <summary>Boshlanish dars raqami, 1-based (joylashtirilmagan bo'lsa <c>null</c>).</summary>
    [ObservableProperty]
    private int? _period;

    /// <summary>Qulflangan karta ko'chmaydi va generatsiya uni qimirlatmaydi.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>Kartaning semantik holati.</summary>
    [ObservableProperty]
    private TimetableCardState _state;

    /// <summary>Xona raqami (bo'sh bo'lishi mumkin).</summary>
    [ObservableProperty]
    private string? _roomNumber;

    /// <summary>UI ichidagi barqaror identifikator (entity ID emas).</summary>
    public int Id { get; init; }

    /// <summary>
    /// Bazadagi <c>Card.Id</c> — faqat adapter va saqlovchi ishlatadi.
    /// Hali kartochkasi yo'q (rejada bor, joylashtirilmagan) dars uchun <c>null</c>.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>Dars ta'rifi (<c>Lesson.Id</c>) — reja ↔ fakt bog'lanishi.</summary>
    public int LessonId { get; init; }

    /// <summary>
    /// Asosiy sinf identifikatori (<c>SchoolClass.Id</c>). Birlashtirilgan darsda
    /// <see cref="ClassIds"/> dagi birinchisi.
    /// </summary>
    public int ClassGroupId { get; init; }

    /// <summary>
    /// Kartaga tegishli barcha sinflar (birlashtirilgan darsda bir nechta).
    /// Bo'sh bo'lsa <see cref="ClassGroupId"/> yolg'iz ishlatiladi.
    /// </summary>
    public IReadOnlyList<int> ClassIds { get; init; } = Array.Empty<int>();

    /// <summary>Kartaga tegishli o'quvchi guruhlari (<c>StudentGroup.Id</c>).</summary>
    public IReadOnlyList<int> GroupIds { get; init; } = Array.Empty<int>();

    /// <summary>Sinf smenasi raqami (1 yoki 2; noma'lum bo'lsa 0).</summary>
    public int ShiftNo { get; init; }

    /// <summary>Fan identifikatori.</summary>
    public int SubjectId { get; init; }

    /// <summary>Kartaga tegishli o'qituvchilar (aSc'da bir kartada bir nechta bo'lishi mumkin).</summary>
    public IReadOnlyList<int> TeacherIds { get; init; } = Array.Empty<int>();

    /// <summary>Fan nomi.</summary>
    public string SubjectName { get; init; } = string.Empty;

    /// <summary>O'qituvchi(lar) nomi.</summary>
    public IReadOnlyList<string> TeacherNames { get; init; } = Array.Empty<string>();

    /// <summary>Sinf nomi ("5-A").</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Sinf bo'linmasi nomi ("1-guruh"); bo'sh bo'lsa — butun sinf.</summary>
    public string GroupName { get; init; } = string.Empty;

    /// <summary>
    /// Bir darsga tegishli kartalar to'plami — <c>CTRL</c> bilan birga ko'chirish shu bo'yicha aniqlanadi.
    /// </summary>
    public string LessonKey { get; init; } = string.Empty;

    /// <summary>Karta uzunligi darslarda: 1 — oddiy, 2 — juft dars.</summary>
    public int Length { get; init; } = 1;

    /// <summary>Hafta bitmaskasi: <c>0b01</c> toq, <c>0b10</c> juft, <c>0b11</c> har hafta.</summary>
    public int WeeksMask { get; init; } = AllWeeks;

    /// <summary>Rang kodi ("#RRGGBB") — o'qituvchi yoki fan rangi.</summary>
    public string ColorCode { get; init; } = "#90A4AE";

    /// <summary>Karta jadvalga qo'yilganmi.</summary>
    public bool IsPlaced => Day.HasValue && Period.HasValue;

    /// <summary>Joriy pozitsiya (qo'yilmagan bo'lsa <c>null</c>).</summary>
    public SlotPosition? Position => IsPlaced ? new SlotPosition(Day!.Value, Period!.Value) : null;

    /// <summary>Juft (yoki undan uzun) darsmi.</summary>
    public bool IsDouble => Length > 1;

    /// <summary>O'qituvchilar matni ("Aliyev A., Valiyev V.").</summary>
    public string TeacherText => TeacherNames.Count == 0 ? string.Empty : string.Join(", ", TeacherNames);

    /// <summary>Sinf + bo'linma matni ("5-A / 1-guruh").</summary>
    public string ScopeText => string.IsNullOrWhiteSpace(GroupName) ? ClassName : ClassName + " / " + GroupName;

    /// <summary>
    /// Hafta maskasi matni: har hafta bo'lsa bo'sh, aks holda "A hafta" / "B hafta".
    /// </summary>
    public string WeeksText => WeeksMask switch
    {
        AllWeeks or <= 0 => string.Empty,
        0b01 => "A hafta",
        0b10 => "B hafta",
        _ => "hafta: " + Convert.ToString(WeeksMask, 2),
    };

    /// <summary>Kartada qo'shimcha belgi ko'rsatiladimi (juft dars yoki hafta maskasi).</summary>
    public bool HasBadge => IsDouble || WeeksText.Length > 0;

    /// <summary>Belgi matni ("2 soat", "A hafta", "2 soat • A hafta").</summary>
    public string BadgeText
    {
        get
        {
            var weeks = WeeksText;

            if (IsDouble && weeks.Length > 0)
            {
                return $"{Length} soat • {weeks}";
            }

            return IsDouble ? $"{Length} soat" : weeks;
        }
    }

    /// <summary>
    /// Karta <paramref name="start"/> pozitsiyasidan boshlansa, qaysi dars raqamlarini egallaydi.
    /// Juft dars <see cref="Length"/> ta ketma-ket soatni yaxlit egallaydi.
    /// </summary>
    public IEnumerable<int> PeriodsFrom(int start)
    {
        for (var i = 0; i < Length; i++)
        {
            yield return start + i;
        }
    }

    /// <summary>Karta hozir egallab turgan dars raqamlari (qo'yilmagan bo'lsa — bo'sh).</summary>
    public IEnumerable<int> OccupiedPeriods => IsPlaced ? PeriodsFrom(Period!.Value) : Enumerable.Empty<int>();

    /// <summary>Ikki karta hafta bo'yicha kesishadimi (mask kesishmasa — to'qnashuv yo'q).</summary>
    public bool OverlapsWeeks(TimetableCard other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return (WeeksMask & other.WeeksMask) != 0;
    }

    /// <summary>
    /// Ikki karta bitta sinf resursini talashadimi.
    /// Turli bo'linmalar (guruhlar) bir vaqtda dars o'tishi mumkin — bu to'qnashuv emas.
    /// </summary>
    public bool SharesClassResource(TimetableCard other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!SharesClass(other))
        {
            return false;
        }

        // Bo'linma ko'rsatilmagan karta butun sinfni band qiladi.
        if (string.IsNullOrWhiteSpace(GroupName) || string.IsNullOrWhiteSpace(other.GroupName))
        {
            return true;
        }

        return string.Equals(GroupName, other.GroupName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ikki karta kamida bitta umumiy sinfga tegishlimi (birlashtirilgan dars hisobga olinadi).
    /// </summary>
    private bool SharesClass(TimetableCard other)
    {
        if (ClassIds.Count == 0 || other.ClassIds.Count == 0)
        {
            return ClassGroupId == other.ClassGroupId;
        }

        foreach (var id in ClassIds)
        {
            if (other.ClassIds.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Kartani boshqa pozitsiyaga qo'yadi (<c>null</c> — joylashtirilmaganlar paneliga).</summary>
    public void MoveTo(SlotPosition? position)
    {
        Day = position?.Day;
        Period = position?.Period;
    }
}
