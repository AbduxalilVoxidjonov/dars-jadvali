using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Application.Scheduling;

/// <summary>Qurilgan masala + kalit xaritasi + o'girish paytidagi izohlar.</summary>
/// <param name="Problem">Yadro uchun sof masala.</param>
/// <param name="Map">EF kaliti ↔ yadro indeksi ikki tomonlama xaritasi.</param>
/// <param name="Notes">O'zbekcha ogohlantirishlar (ma'lumot nuqsonlari, soddalashtirishlar).</param>
public sealed record MappedProblem(
    Problem Problem,
    SchedulingIdMap Map,
    IReadOnlyList<string> Notes);

/// <summary>
/// EF ma'lumot modeli ↔ <c>DarsJadvali.Scheduling</c> yadrosi orasidagi mapper (00 §6.3).
/// </summary>
/// <remarks>
/// Bu tur <b>sof</b>: bazaga murojaat qilmaydi, faqat <see cref="SchedulingInput"/> ustida
/// ishlaydi. Shu sababli mapper testlari bazasiz yoziladi.
/// </remarks>
public interface ISchedulingMapper
{
    /// <summary>EF ma'lumotidan yadro masalasini quradi.</summary>
    /// <exception cref="SchedulingMappingException">Ma'lumot generatsiya uchun yaroqsiz bo'lsa.</exception>
    MappedProblem BuildProblem(SchedulingInput input);

    /// <summary>Yadro yechimini bazaga yoziladigan kartochkalarga o'giradi.</summary>
    IReadOnlyList<CardWrite> BuildCards(SchedulingInput input, MappedProblem mapped, Solution solution);

    /// <summary>
    /// Yozilishi kutilayotgan kartochkalarni <c>GROUP_DIVISION_OVERLAP</c> tekshiruvi
    /// uchun ko'rinishga o'giradi (baza yozuvisiz).
    /// </summary>
    IReadOnlyList<PlacedCardView> BuildPlacedViews(
        SchedulingInput input, IReadOnlyList<CardWrite> cards);
}
