using DarsJadvali.Application.Validation;

namespace DarsJadvali.Application.Scheduling;

/// <summary>Kartochka bandligida ishtirok etadigan guruh haqidagi minimal ma'lumot.</summary>
/// <param name="StudentGroupId">Guruh Id.</param>
/// <param name="GroupName">Guruh nomi ("1-guruh", "O'g'illar", "Butun sinf").</param>
/// <param name="SchoolClassId">Sinf Id.</param>
/// <param name="ClassName">Sinf nomi.</param>
/// <param name="DivisionTag">Bo'linish tegi: 0 = butun sinf, 1 = 1/2 guruh, 2 = o'g'il/qiz.</param>
public sealed record PlacedGroupRef(
    int StudentGroupId,
    string GroupName,
    int SchoolClassId,
    string ClassName,
    int DivisionTag);

/// <summary>
/// Joylashtirilgan kartochkaning tekshiruvga kerakli ko'rinishi
/// (bazadan ham, hali yozilmagan generatsiya natijasidan ham hosil qilinadi).
/// </summary>
/// <param name="CardId">Kartochka Id (yangi natijada — yadro indeksi).</param>
/// <param name="SubjectName">Fan nomi (xabar uchun).</param>
/// <param name="DayNo">Kun raqami (0-based).</param>
/// <param name="StartPeriodNo">Boshlanish dars soati raqami.</param>
/// <param name="Length">Necha soat egallaydi (juft dars).</param>
/// <param name="WeeksMask">Qaysi haftalarda turadi.</param>
/// <param name="Groups">Dars o'tiladigan guruhlar.</param>
public sealed record PlacedCardView(
    int CardId,
    string SubjectName,
    int DayNo,
    int StartPeriodNo,
    int Length,
    int WeeksMask,
    IReadOnlyList<PlacedGroupRef> Groups);

/// <summary>
/// <b>GROUP_DIVISION_OVERLAP</b> — DB ushlay olmaydigan yagona holat (00 §10.3).
/// </summary>
/// <remarks>
/// Bandlik qatorlari guruh aniqligida yozilgani uchun unikal indeks "1-guruh + 2-guruh"
/// (bir bo'linish ichida) ni to'g'ri ravishda RUXSAT etadi, "Butun sinf + 1-guruh" ni esa
/// RAD etadi. Lekin <b>turli bo'linishlardagi</b> guruhlar ("1-guruh" + "O'g'illar")
/// har xil <c>StudentGroupId</c> ga ega, ya'ni indeks buzilmaydi — holbuki bitta
/// o'quvchi ikkala guruhga ham kirishi mumkin. Shu sababli qoida Application
/// darajasida tekshiriladi (00 §2.7, aSc #1895).
/// <para>
/// Yadro bu qoidani generatsiya paytida <c>C-GBL-08</c> sifatida ushlaydi; bu tekshiruv
/// esa <b>qo'lda tahrirlangan</b> yoki ko'chirib keltirilgan ma'lumot uchun himoya.
/// </para>
/// </remarks>
public static class GroupDivisionOverlapValidator
{
    /// <summary>Konflikt kodi.</summary>
    public const string Code = ConflictCodes.GroupDivisionOverlap;

    /// <summary>
    /// Joylashtirilgan kartochkalar orasida turli bo'linish guruhlarining bir slotda
    /// uchrashishini topadi.
    /// </summary>
    public static IReadOnlyList<Conflict> Check(IReadOnlyList<PlacedCardView> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        // (sinf, kun, soat, hafta) -> shu slotda uchragan birinchi (tag, karta) juftligi.
        var seen = new Dictionary<(int ClassId, int DayNo, int PeriodNo, int WeekNo),
                                  (int Tag, PlacedCardView Card, PlacedGroupRef Group)>();
        var conflicts = new List<Conflict>();
        var reported = new HashSet<(int, int, int, int)>();

        foreach (var card in cards)
        {
            var weeks = ExpandWeeks(card.WeeksMask);
            var length = Math.Max(1, card.Length);

            // Bitta kartaning o'zi bir nechta bo'linishni birlashtirsa — bu ham xato
            // (yadro buni ProblemBuilder darajasida ushlaydi, lekin bazadagi ma'lumot
            // boshqa yo'l bilan kelgan bo'lishi mumkin).
            foreach (var group in card.Groups)
            {
                foreach (var week in weeks)
                {
                    for (var offset = 0; offset < length; offset++)
                    {
                        var key = (group.SchoolClassId, card.DayNo, card.StartPeriodNo + offset, week);

                        if (!seen.TryGetValue(key, out var prev))
                        {
                            seen[key] = (group.DivisionTag, card, group);
                            continue;
                        }

                        if (prev.Tag == group.DivisionTag) continue;
                        if (!reported.Add(key)) continue;

                        conflicts.Add(new Conflict(
                            ConflictSeverity.Error,
                            Code,
                            $"{group.ClassName} sinfida {DayName(card.DayNo)} kuni " +
                            $"{key.Item3}-soatda turli bo'linish guruhlari bir vaqtda dars o'tmoqda: " +
                            $"«{prev.Group.GroupName}» ({prev.Card.SubjectName}) va " +
                            $"«{group.GroupName}» ({card.SubjectName}). " +
                            $"Bir vaqtda faqat BITTA bo'linish ichidagi guruhlar dars o'ta oladi."));
                    }
                }
            }
        }

        return conflicts;
    }

    /// <summary>Hafta maskasidagi yoqilgan bitlar (0 = faqat birinchi hafta).</summary>
    private static IReadOnlyList<int> ExpandWeeks(int weeksMask)
    {
        if (weeksMask <= 0) return new[] { 0 };

        var weeks = new List<int>(2);
        for (var i = 0; i < 31; i++)
        {
            if ((weeksMask & (1 << i)) != 0) weeks.Add(i);
        }

        return weeks.Count == 0 ? new[] { 0 } : weeks;
    }

    private static readonly string[] DayNames =
    {
        "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma", "Shanba", "Yakshanba"
    };

    private static string DayName(int dayNo) =>
        dayNo >= 0 && dayNo < DayNames.Length ? DayNames[dayNo] : $"{dayNo + 1}-kun";
}
