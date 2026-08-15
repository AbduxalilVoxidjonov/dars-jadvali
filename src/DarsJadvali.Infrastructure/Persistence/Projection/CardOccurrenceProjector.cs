using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence.Projection;

/// <summary>
/// <c>Card</c> qatorlarini <c>CardOccurrence</c> bandlik qatorlariga yoyadi.
/// </summary>
/// <remarks>
/// Yoyish formulasi:
/// <code>
/// CardOccurrence = Card
///   × { PeriodNo : Period.PeriodNo .. +Card.Length-1 }             // juft dars
///   × { WeekNo   : Card.WeeksMask dagi bitlar }                    // A/B hafta
///   × { resurs   : o'qituvchilar ∪ YOYILGAN guruhlar ∪ xonalar }
/// </code>
///
/// <para><b>Guruhlarni yoyish qoidasi — eng muhim joy.</b>
/// <list type="bullet">
/// <item>Dars <c>IsEntireClass</c> guruhga tegishli bo'lsa — bandlik o'sha guruhga
/// <b>va sinfning barcha boshqa guruhlariga</b> yoziladi. Shu sababli "butun sinf darsi"
/// + "guruh darsi" bir slotda unikal indeksni buzadi va DB tomonidan RAD ETILADI.</item>
/// <item>Oddiy guruh bo'lsa — bandlik faqat o'sha guruhga yoziladi. Shu sababli bir
/// sinfning ikki turli guruhi bir vaqtda dars o'ta oladi (7a va 7b stsenariylari).</item>
/// </list>
/// </para>
///
/// <para><b>DB nimani ushlay olmaydi:</b> turli <c>ClassDivision</c> dagi guruhlar
/// (masalan "1-guruh" + "o'g'illar") bir slotda — ularning Id'lari har xil, indeks
/// buzilmaydi. Bu Application darajasidagi <c>GROUP_DIVISION_OVERLAP</c> qoidasi
/// (00 §2.7) — keyingi bosqichda <c>ScheduleValidator</c> ga qo'shiladi.</para>
/// </remarks>
public sealed class CardOccurrenceProjector : ICardOccurrenceProjector
{
    private readonly AppDbContext _context;

    public CardOccurrenceProjector(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<int> RebuildForCardAsync(int cardId, CancellationToken ct = default)
    {
        var card = await _context.Cards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId, ct)
            .ConfigureAwait(false);

        if (card is null) return 0;

        await _context.CardOccurrences
            .Where(o => o.CardId == cardId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        var rows = await BuildRowsAsync(new[] { card }, ct).ConfigureAwait(false);
        if (rows.Count == 0) return 0;

        await _context.CardOccurrences.AddRangeAsync(rows, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<int> RebuildForCardsAsync(
        IReadOnlyList<int> cardIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cardIds);
        if (cardIds.Count == 0) return 0;

        var ids = cardIds.Distinct().ToList();

        // 1-faza: BARCHASINING eski qatorlari o'chiriladi (o'rin almashtirish uchun shart).
        await _context.CardOccurrences
            .Where(o => ids.Contains(o.CardId))
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        var cards = await _context.Cards
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // 2-faza: yangilari yoziladi.
        var rows = await BuildRowsAsync(cards, ct).ConfigureAwait(false);
        if (rows.Count == 0) return 0;

        await _context.CardOccurrences.AddRangeAsync(rows, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<int> RebuildForScheduleAsync(int scheduleId, CancellationToken ct = default)
    {
        await _context.CardOccurrences
            .Where(o => o.ScheduleId == scheduleId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        var cards = await _context.Cards
            .AsNoTracking()
            .Where(c => c.ScheduleId == scheduleId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var rows = await BuildRowsAsync(cards, ct).ConfigureAwait(false);
        if (rows.Count == 0) return 0;

        await _context.CardOccurrences.AddRangeAsync(rows, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return rows.Count;
    }

    // -------------------------------------------------------------------------

    private async Task<List<CardOccurrence>> BuildRowsAsync(
        IReadOnlyCollection<Card> cards, CancellationToken ct)
    {
        var result = new List<CardOccurrence>();
        if (cards.Count == 0) return result;

        var cardIds = cards.Select(c => c.Id).ToHashSet();
        var lessonIds = cards.Select(c => c.LessonId).ToHashSet();
        var periodIds = cards.Select(c => c.PeriodId).ToHashSet();

        // Boshlanish soati raqami. Juft dars uzunligi endi KARTOCHKANING o'zidan olinadi
        // (Card.Length): "2 + 2 + 1" holatida bir darsning kartochkalari turli uzunlikda
        // bo'ladi va Lesson.PeriodsPerCard bunga yaramaydi.
        var periodNo = await _context.Periods
            .AsNoTracking()
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PeriodNo, ct)
            .ConfigureAwait(false);

        var teachersByLesson = (await _context.LessonTeachers
                .AsNoTracking()
                .Where(lt => lessonIds.Contains(lt.LessonId))
                .ToListAsync(ct).ConfigureAwait(false))
            .GroupBy(lt => lt.LessonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TeacherId).ToArray());

        var groupsByLesson = (await _context.LessonGroups
                .AsNoTracking()
                .Where(lg => lessonIds.Contains(lg.LessonId))
                .ToListAsync(ct).ConfigureAwait(false))
            .GroupBy(lg => lg.LessonId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentGroupId).ToArray());

        var classroomsByCard = (await _context.CardClassrooms
                .AsNoTracking()
                .Where(cc => cardIds.Contains(cc.CardId))
                .ToListAsync(ct).ConfigureAwait(false))
            .GroupBy(cc => cc.CardId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ClassroomId).ToArray());

        var groupExpansion = await BuildGroupExpansionAsync(
            groupsByLesson.Values.SelectMany(x => x).ToHashSet(), ct).ConfigureAwait(false);

        foreach (var card in cards)
        {
            var length = Math.Max(1, card.Length);
            var startPeriod = periodNo.TryGetValue(card.PeriodId, out var pn) ? pn : 0;

            // Resurslar to'plami — takrorlanmaydigan (tur, id) juftliklari.
            var resources = new HashSet<(ResourceKind Kind, int Id)>();

            if (teachersByLesson.TryGetValue(card.LessonId, out var teacherIds))
            {
                foreach (var id in teacherIds) resources.Add((ResourceKind.Teacher, id));
            }

            if (groupsByLesson.TryGetValue(card.LessonId, out var groupIds))
            {
                foreach (var id in groupIds)
                {
                    foreach (var expanded in groupExpansion[id])
                    {
                        resources.Add((ResourceKind.StudentGroup, expanded));
                    }
                }
            }

            if (classroomsByCard.TryGetValue(card.Id, out var roomIds))
            {
                foreach (var id in roomIds) resources.Add((ResourceKind.Classroom, id));
            }

            if (resources.Count == 0) continue;

            // WeeksMask = 0 bo'lishi CHECK bilan taqiqlangan, lekin himoya sifatida
            // 0 ni "faqat 0-hafta" deb talqin qilamiz.
            var weeks = card.WeeksMask == 0
                ? new[] { 0 }
                : BitMask.Bits(card.WeeksMask).ToArray();

            foreach (var week in weeks)
            {
                for (var offset = 0; offset < length; offset++)
                {
                    foreach (var (kind, id) in resources)
                    {
                        result.Add(new CardOccurrence
                        {
                            ScheduleId = card.ScheduleId,
                            CardId = card.Id,
                            DayNo = card.DayNo,
                            PeriodNo = startPeriod + offset,
                            WeekNo = week,
                            ResourceKind = kind,
                            ResourceId = id
                        });
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Har bir guruh Id uchun u BAND QILADIGAN guruhlar to'plamini qaytaradi.
    /// "Butun sinf" guruhi — sinfning barcha guruhlarini; oddiy guruh — faqat o'zini.
    /// </summary>
    private async Task<Dictionary<int, int[]>> BuildGroupExpansionAsync(
        HashSet<int> groupIds, CancellationToken ct)
    {
        var map = new Dictionary<int, int[]>();
        if (groupIds.Count == 0) return map;

        var groups = await _context.StudentGroups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.SchoolClassId, g.IsEntireClass })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var entireClassIds = groups
            .Where(g => g.IsEntireClass)
            .Select(g => g.SchoolClassId)
            .ToHashSet();

        // Faqat "butun sinf" darsi bor sinflar uchun barcha guruhlar o'qiladi.
        var siblings = entireClassIds.Count == 0
            ? new List<(int SchoolClassId, int Id)>()
            : (await _context.StudentGroups
                    .AsNoTracking()
                    .Where(g => entireClassIds.Contains(g.SchoolClassId) && !g.IsDeleted)
                    .Select(g => new { g.SchoolClassId, g.Id })
                    .ToListAsync(ct).ConfigureAwait(false))
                .Select(x => (x.SchoolClassId, x.Id))
                .ToList();

        var byClass = siblings
            .GroupBy(x => x.SchoolClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToArray());

        foreach (var group in groups)
        {
            map[group.Id] = group.IsEntireClass && byClass.TryGetValue(group.SchoolClassId, out var all)
                ? all
                : new[] { group.Id };
        }

        // Topilmagan (o'chirilgan) guruhlar — o'zini band qiladi.
        foreach (var id in groupIds)
        {
            if (!map.ContainsKey(id)) map[id] = new[] { id };
        }

        return map;
    }
}
