namespace DarsJadvali.Scheduling.Model;

/// <summary>
/// aSc'ning 3 holatli time-off matritsasi (C-AVL, tip #18):
/// ko'k ✓ = ruxsat, `?` = ruxsat lekin yomon (jarima, C-AVL-06), qizil ✗ = taqiqlangan (hard).
/// </summary>
public enum AvailabilityState
{
    /// <summary>Ruxsat etilgan.</summary>
    Allowed = 0,

    /// <summary>Ruxsat, lekin yomon — C-AVL-06 jarimasi qo'llanadi.</summary>
    Questioned = 1,

    /// <summary>Taqiqlangan — C-AVL-01..05, domain'dan chiqariladi.</summary>
    Forbidden = 2,
}

/// <summary>3 holatli mavjudlik matritsasi (ikkita bitmask sifatida).</summary>
public sealed class Availability
{
    /// <summary>Qizil ✗ — hard taqiq.</summary>
    public SlotMask Forbidden { get; internal set; }

    /// <summary>`?` — soft jarima.</summary>
    public SlotMask Questioned { get; internal set; }

    public void Set(TimeGrid grid, int dayIndex, int period, AvailabilityState state)
        => Set(grid.SlotOf(dayIndex, period), state);

    public void Set(int slot, AvailabilityState state)
    {
        Forbidden = Forbidden.Clear(slot);
        Questioned = Questioned.Clear(slot);
        switch (state)
        {
            case AvailabilityState.Forbidden: Forbidden = Forbidden.Set(slot); break;
            case AvailabilityState.Questioned: Questioned = Questioned.Set(slot); break;
        }
    }

    /// <summary>Butun kunni belgilash.</summary>
    public void SetDay(TimeGrid grid, int dayIndex, AvailabilityState state)
    {
        for (int p = 0; p < grid.Periods; p++) Set(grid.SlotOf(dayIndex, p), state);
    }

    public AvailabilityState Get(int slot)
        => Forbidden.Test(slot) ? AvailabilityState.Forbidden
         : Questioned.Test(slot) ? AvailabilityState.Questioned
         : AvailabilityState.Allowed;
}

/// <summary>Resurs turi.</summary>
public enum ResourceKind
{
    Teacher = 0,
    Class = 1,
    Group = 2,
    Room = 3,
    Subject = 4,
}

/// <summary>Barcha resurslar uchun umumiy asos.</summary>
public abstract class ResourceDef
{
    public int Id { get; internal set; } = -1;
    public string Name { get; set; } = string.Empty;
    public abstract ResourceKind Kind { get; }

    /// <summary>3 holatli time-off (C-AVL).</summary>
    public Availability Availability { get; } = new();

    public override string ToString() => $"{Kind}#{Id} {Name}";
}

/// <summary>O'qituvchi (C-TCH oilasi).</summary>
public sealed class TeacherDef : ResourceDef
{
    public override ResourceKind Kind => ResourceKind.Teacher;

    /// <summary>C-TCH-02 — kuniga maksimal oynalar. -1 = cheklanmagan.</summary>
    public int MaxGapsPerDay { get; set; } = -1;

    /// <summary>C-TCH-01 — haftasiga maksimal oynalar. -1 = cheklanmagan.</summary>
    public int MaxGapsPerWeek { get; set; } = -1;

    /// <summary>C-TCH-10 — maksimal ketma-ket darslar (kvadratik jarima). -1 = cheklanmagan.</summary>
    public int MaxConsecutivePeriods { get; set; } = -1;

    /// <summary>C-TCH-14 — kuniga maksimal darslar. -1 = cheklanmagan.</summary>
    public int MaxPeriodsPerDay { get; set; } = -1;

    /// <summary>C-TCH-15 — kuniga minimal darslar (bo'sh kun bundan mustasno). -1 = cheklanmagan.</summary>
    public int MinPeriodsPerDay { get; set; } = -1;

    /// <summary>C-TCH-07 — haftasiga maksimal dars kunlari (bo'sh kun talabi shu orqali). -1 = cheklanmagan.</summary>
    public int MaxDaysPerWeek { get; set; } = -1;

    /// <summary>C-TCH-08 — haftasiga minimal dars kunlari. -1 = cheklanmagan.</summary>
    public int MinDaysPerWeek { get; set; } = -1;
}

/// <summary>Sinf (C-CLS oilasi).</summary>
public sealed class ClassDef : ResourceDef
{
    public override ResourceKind Kind => ResourceKind.Class;

    /// <summary>C-CLS-01 — sinfda oyna bo'lmasligi kerak (w=800). Ruxsat etilgan oynalar soni.</summary>
    public int MaxGapsPerDay { get; set; } = 0;

    /// <summary>C-CLS-03 — kuniga maksimal darslar. -1 = cheklanmagan.</summary>
    public int MaxLessonsPerDay { get; set; } = -1;

    /// <summary>C-CLS-03 — kuniga minimal darslar (bo'sh kun bundan mustasno). -1 = cheklanmagan.</summary>
    public int MinLessonsPerDay { get; set; } = -1;

    /// <summary>Sinfdagi o'quvchilar soni (C-ROM-02 sig'imi uchun standart qiymat).</summary>
    public int StudentCount { get; set; }
}

/// <summary>
/// Guruh (bo'linish elementi). <b>divisiontag semantikasi</b> (01-asc-data-model.md, 3.2):
/// bir xil <see cref="DivisionTag"/> ga ega guruhlar bitta bo'linishga tegishli va
/// <b>bir vaqtda</b> dars o'tishi MUMKIN; turli tag'lilar YO'Q (bir o'quvchi ikki joyda bo'la olmaydi, C-GBL-08).
/// <c>entireclass=1</c> guruh <see cref="DivisionTag"/> = 0 va hech kim bilan parallel bo'la olmaydi.
/// </summary>
public sealed class GroupDef : ResourceDef
{
    public override ResourceKind Kind => ResourceKind.Group;

    public int ClassId { get; internal set; } = -1;

    /// <summary>Bo'linish raqami. 0 = butun sinf.</summary>
    public int DivisionTag { get; internal set; }

    /// <summary>Butun sinfni ifodalovchi guruhmi.</summary>
    public bool IsEntireClass => DivisionTag == 0;

    public int StudentCount { get; set; }
}

/// <summary>Xona (C-ROM oilasi).</summary>
public sealed class RoomDef : ResourceDef
{
    public override ResourceKind Kind => ResourceKind.Room;

    /// <summary>C-ROM-02 — sig'im (o'quvchilar soni). 0 = cheklanmagan.</summary>
    public int Capacity { get; set; }

    /// <summary>C-ROM-05 — bir vaqtda shu xonada bo'la oladigan darslar soni.</summary>
    public int ParallelLessons { get; set; } = 1;
}

/// <summary>Taqsimot darajasi — aSc `#3727..#3730`.</summary>
public enum DistributionLevel
{
    /// <summary>#3727 — tekshirilmaydi.</summary>
    None = 0,

    /// <summary>#3728 — faqat bir kunda takror.</summary>
    Low = 1,

    /// <summary>#3729 — takror + kunlararo masofa.</summary>
    Medium = 2,

    /// <summary>#3730 — qattiq tekis taqsimot.</summary>
    Ideal = 3,
}

/// <summary>Fan (C-DST oilasi).</summary>
public sealed class SubjectDef : ResourceDef
{
    public override ResourceKind Kind => ResourceKind.Subject;

    /// <summary>C-DST-05 — bir kunda faqat bir marta.</summary>
    public bool OncePerDay { get; set; } = true;

    /// <summary>C-DST-01/02 — taqsimot darajasi.</summary>
    public DistributionLevel Distribution { get; set; } = DistributionLevel.Medium;
}

/// <summary>Kartani ma'lum pozitsiyaga qulflash (C-GBL-06).</summary>
public readonly record struct FixedPlacement(int DayIndex, int Period, int RoomId = -1);

/// <summary>
/// Dars TALABI (aSc `lessons`): "shu fan, shu guruh(lar)ga, shu o'qituvchi(lar) bilan haftada N soat".
/// Generatsiya vaqtida <see cref="Card"/> larga bo'linadi (lesson vs card, 01-asc-data-model.md).
/// </summary>
public sealed class LessonDef
{
    public int Id { get; internal set; } = -1;
    public string Name { get; set; } = string.Empty;
    public int SubjectId { get; internal set; } = -1;

    /// <summary>Bir darsda bir necha o'qituvchi bo'lishi mumkin.</summary>
    public int[] TeacherIds { get; internal set; } = Array.Empty<int>();

    /// <summary>Dars o'tiladigan guruhlar (bo'linish guruhlari yoki butun sinf guruhi).</summary>
    public int[] GroupIds { get; internal set; } = Array.Empty<int>();

    /// <summary>C-ROM-01 — ruxsat etilgan xonalar. Bo'sh = xona talab qilinmaydi.</summary>
    public int[] AllowedRoomIds { get; set; } = Array.Empty<int>();

    /// <summary>Haftadagi jami dars soati.</summary>
    public int PeriodsPerWeek { get; internal set; } = 1;

    /// <summary>Bitta kartadagi soatlar (1 = single, 2 = double, 3 = triple; C-DBL-01).</summary>
    public int PeriodsPerCard { get; internal set; } = 1;

    /// <summary>C-CYC-03 — dars o'tilishi mumkin bo'lgan kunlar (dayIndex) bitmask'i. null = barcha kunlar.</summary>
    public int[]? AllowedDays { get; set; }

    /// <summary>C-ROM-02 — o'quvchilar soni (xona sig'imi uchun). 0 bo'lsa guruhlardan hisoblanadi.</summary>
    public int StudentCount { get; set; }

    /// <summary>C-GBL-06 — qulflangan kartalar (tartib bo'yicha kartalarga biriktiriladi).</summary>
    public List<FixedPlacement> Locked { get; } = new();

    /// <summary>C-DST-04 — bu dars tekis taqsimlanishi shart emas.</summary>
    public bool SkipDistribution { get; set; }

    public override string ToString() => $"Lesson#{Id} {Name} ({PeriodsPerWeek}x{PeriodsPerCard})";
}
