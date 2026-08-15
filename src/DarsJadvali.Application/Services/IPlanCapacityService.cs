using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Pipeline;

namespace DarsJadvali.Application.Services;

/// <summary>Sig'im ogohlantirishi kimga tegishli.</summary>
public enum CapacityScope
{
    /// <summary>Sinf (barcha bo'linishlar hisobga olingan).</summary>
    Class = 0,

    /// <summary>O'quvchilar guruhi (sinf bo'linmasi).</summary>
    Group = 1,

    /// <summary>O'qituvchi.</summary>
    Teacher = 2,
}

/// <summary>
/// Bitta sig'im ogohlantirishi: rejalashtirilgan soat mavjud slotlardan oshib ketgan.
/// </summary>
/// <param name="Scope">Kimga tegishli (sinf / guruh / o'qituvchi).</param>
/// <param name="Name">Nomi ("5-A", "Aliyev Vali").</param>
/// <param name="PlannedPeriods">Rejalashtirilgan soat.</param>
/// <param name="AvailableSlots">Mavjud (taqiqlanmagan) slotlar soni.</param>
public sealed record CapacityWarning(
    CapacityScope Scope,
    string Name,
    int PlannedPeriods,
    int AvailableSlots)
{
    /// <summary>Sig'maydigan soat.</summary>
    public int Overflow => Math.Max(0, PlannedPeriods - AvailableSlots);

    /// <summary>Foydalanuvchi uchun tayyor o'zbekcha xabar.</summary>
    public string Message =>
        $"{Name}: {PlannedPeriods} soat rejalashtirilgan, {AvailableSlots} ta slot bor — " +
        $"{Overflow} soat sig'maydi.";
}

/// <summary>Reja sig'imi tekshiruvining natijasi.</summary>
/// <param name="Warnings">Sig'imdan oshgan sinf / guruh / o'qituvchilar.</param>
/// <param name="VerificationFaults">
/// Yadroning <see cref="Verifier"/> fazasi topgan barcha xatolar ("[KOD] xabar" ko'rinishida).
/// Generatsiya hisobotidagi <c>VerificationFaults</c> bilan AYNI manba.
/// </param>
public sealed record PlanCapacityReport(
    IReadOnlyList<CapacityWarning> Warnings,
    IReadOnlyList<string> VerificationFaults)
{
    /// <summary>Bo'sh (muammosiz) natija.</summary>
    public static PlanCapacityReport Empty { get; } =
        new(Array.Empty<CapacityWarning>(), Array.Empty<string>());

    /// <summary>Sig'im ogohlantirishi bormi.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Jami sig'maydigan soat.</summary>
    public int TotalOverflow => Warnings.Sum(w => w.Overflow);

    /// <summary>Qisqacha xulosa matni.</summary>
    public string Summary => HasWarnings
        ? $"{Warnings.Count} ta sinf/guruh/o'qituvchi sig'imdan oshgan — jami {TotalOverflow} soat sig'maydi."
        : "Reja sig'imga mos: har bir sinf va o'qituvchining soatlari bo'sh slotlarga sig'adi.";
}

/// <summary>
/// Generatsiyadan OLDIN rejani sig'im bo'yicha tekshiradi (aSc "Verify specification").
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> Reja sig'imdan oshsa (masalan 1-A sinfida 47 soat
/// rejalashtirilgan, lekin jami 35 ta slot bor) generator to'g'ri ishlaydi, ammo
/// darslarning bir qismi <b>jimgina</b> joylashmay qoladi va foydalanuvchi buning
/// sababini bilmaydi. Bu servis o'sha farqni generatsiyadan oldin aniq son bilan aytadi.
/// <para>
/// <b>Manba bitta.</b> Xatolar ro'yxati yadroning <see cref="Verifier"/> fazasidan
/// olinadi — ya'ni generatsiya hisobotidagi <c>VerificationFaults</c> bilan bir xil.
/// Ustiga tuzilmali (son bilan) ogohlantirishlar qo'shiladi, chunki yadro xabari
/// "necha soat sig'maydi" degan farqni bermaydi.
/// </para>
/// </remarks>
public interface IPlanCapacityService
{
    /// <summary>Rejani tekshiradi. Bazaga hech narsa yozmaydi.</summary>
    Task<PlanCapacityReport> CheckAsync(int? scheduleId = null, CancellationToken ct = default);
}

/// <summary><see cref="IPlanCapacityService"/> implementatsiyasi.</summary>
public sealed class PlanCapacityService : IPlanCapacityService
{
    private readonly IUnitOfWork _uow;
    private readonly ISchedulingStore _store;
    private readonly ISchedulingMapper _mapper;

    /// <summary>Yangi servis yaratadi.</summary>
    public PlanCapacityService(IUnitOfWork uow, ISchedulingStore store, ISchedulingMapper mapper)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public async Task<PlanCapacityReport> CheckAsync(
        int? scheduleId = null, CancellationToken ct = default)
    {
        var id = await ActiveScheduleResolver.ResolveIdAsync(_uow, scheduleId, ct).ConfigureAwait(false);
        var input = await _store.LoadAsync(id, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        // Yadro masalasi generatsiyadagi bilan AYNAN bir xil yo'ldan quriladi.
        var mapped = _mapper.BuildProblem(input);
        var problem = mapped.Problem;

        if (problem.Cards.Length == 0)
        {
            return PlanCapacityReport.Empty;
        }

        var faults = Verifier.Verify(problem).Faults
            .Select(f => $"[{f.Code}] {f.Message}")
            .ToList();

        // Haqiqiy o'qish to'ri: ish kunlari bo'lmagan kunlar hisobga OLINMAYDI.
        var grid = ActiveMask(problem);

        var warnings = new List<CapacityWarning>();
        warnings.AddRange(ClassWarnings(problem, grid));
        warnings.AddRange(GroupWarnings(problem, grid));
        warnings.AddRange(TeacherWarnings(problem, grid));

        return new PlanCapacityReport(
            warnings.OrderByDescending(w => w.Overflow)
                .ThenBy(w => w.Name, StringComparer.CurrentCulture)
                .ToList(),
            faults);
    }

    /// <summary>
    /// Maktabning HAQIQIY o'qish to'ri: kamida bitta sinf uchun ochiq bo'lgan slotlar.
    /// </summary>
    /// <remarks>
    /// Yadroning vaqt to'ri (<c>TimeGrid</c>) haftaning barcha kunlarini o'z ichiga oladi,
    /// dam olish kunlari esa sinf darajasida taqiqlanadi. Shu sababli o'qituvchi uchun
    /// "bo'sh slot" ni to'g'ridan-to'g'ri <c>FullMask</c> dan hisoblash yakshanbani ham
    /// ish kuni deb ko'rsatib yuborardi.
    /// </remarks>
    private static SlotMask ActiveMask(Problem p)
    {
        if (p.Classes.Length == 0) return p.Grid.FullMask;

        var mask = SlotMask.Empty;
        foreach (var cls in p.Classes)
        {
            mask |= p.Grid.FullMask.AndNot(cls.Availability.Forbidden);
        }

        return mask.IsEmpty ? p.Grid.FullMask : mask;
    }

    /// <summary>Sinflar: talab (bo'linishlar hisobga olingan) va bo'sh slotlar.</summary>
    private static IEnumerable<CapacityWarning> ClassWarnings(Problem p, SlotMask grid)
    {
        for (var c = 0; c < p.Classes.Length; c++)
        {
            var demand = ClassPeriodDemand(p, c);
            var free = grid.AndNot(p.Classes[c].Availability.Forbidden).PopCount();

            if (demand > free)
            {
                yield return new CapacityWarning(CapacityScope.Class, p.Classes[c].Name, demand, free);
            }
        }
    }

    /// <summary>Guruhlar: bo'linma darajasidagi sig'im (sinf darajasi yashirib yuboradigan holat).</summary>
    private static IEnumerable<CapacityWarning> GroupWarnings(Problem p, SlotMask grid)
    {
        for (var g = 0; g < p.Groups.Length; g++)
        {
            var demand = 0;
            foreach (var cid in p.CardsOfGroup[g])
            {
                demand += p.Cards[cid].Length;
            }

            if (demand == 0) continue;

            var free = grid.AndNot(p.Groups[g].Availability.Forbidden).PopCount();

            // Butun sinf guruhi sinf qatorida allaqachon aytilgan — takrorlamaymiz.
            if (demand > free && !p.Groups[g].IsEntireClass)
            {
                yield return new CapacityWarning(CapacityScope.Group, GroupName(p, g), demand, free);
            }
        }
    }

    /// <summary>O'qituvchilar: haftalik yuk va ish vaqtidagi bo'sh slotlar.</summary>
    private static IEnumerable<CapacityWarning> TeacherWarnings(Problem p, SlotMask grid)
    {
        for (var t = 0; t < p.Teachers.Length; t++)
        {
            var demand = 0;
            foreach (var cid in p.CardsOfTeacher[t])
            {
                demand += p.Cards[cid].Length;
            }

            var free = grid.AndNot(p.Teachers[t].Availability.Forbidden).PopCount();

            if (demand > free)
            {
                yield return new CapacityWarning(CapacityScope.Teacher, p.Teachers[t].Name, demand, free);
            }
        }
    }

    private static string GroupName(Problem p, int groupId)
    {
        var group = p.Groups[groupId];
        var className = group.ClassId >= 0 && group.ClassId < p.Classes.Length
            ? p.Classes[group.ClassId].Name
            : string.Empty;

        return string.IsNullOrWhiteSpace(className)
            ? group.Name
            : $"{className} / {group.Name}";
    }

    /// <summary>
    /// Sinfning haqiqiy soat talabi: bir xil <c>divisiontag</c> li guruhlar PARALLEL
    /// o'tadi, shuning uchun bo'linish ichida eng yuklangan guruh olinadi, bo'linishlar
    /// esa qo'shiladi. Yadroning <see cref="Verifier"/> dagi hisobi bilan bir xil qoida.
    /// </summary>
    private static int ClassPeriodDemand(Problem p, int classId)
    {
        var perTag = new Dictionary<int, Dictionary<int, int>>();

        foreach (var cid in p.CardsOfClass[classId])
        {
            var card = p.Cards[cid];
            var idx = Array.IndexOf(card.ClassIds, classId);
            if (idx < 0 || idx >= card.ClassDivisionTags.Length) continue;

            var tag = card.ClassDivisionTags[idx];
            if (!perTag.TryGetValue(tag, out var byGroup))
            {
                byGroup = new Dictionary<int, int>();
                perTag[tag] = byGroup;
            }

            foreach (var g in card.GroupIds)
            {
                if (p.Groups[g].ClassId != classId) continue;
                byGroup.TryGetValue(g, out var v);
                byGroup[g] = v + card.Length;
            }
        }

        var total = 0;
        foreach (var byGroup in perTag.Values)
        {
            var max = 0;
            foreach (var v in byGroup.Values)
            {
                if (v > max) max = v;
            }

            total += max;
        }

        return total;
    }
}
