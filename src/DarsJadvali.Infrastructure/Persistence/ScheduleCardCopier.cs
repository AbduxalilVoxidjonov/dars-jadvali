using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary><see cref="IScheduleCardCopier"/> implementatsiyasi.</summary>
/// <remarks>
/// <b>Tranzaksiya qoidasi (00 §6.4):</b> bu servis o'z tranzaksiyasini OCHMAYDI —
/// chaqiruvchi (<c>ScheduleSetService.DuplicateAsync</c>) mas'ul.
/// </remarks>
public sealed class ScheduleCardCopier : IScheduleCardCopier
{
    private readonly AppDbContext _context;
    private readonly ICardOccurrenceProjector _projector;

    /// <summary>Yangi nusxalovchi yaratadi.</summary>
    public ScheduleCardCopier(AppDbContext context, ICardOccurrenceProjector projector)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    /// <inheritdoc />
    public async Task<int> CopyCardsAsync(
        int sourceScheduleId, int targetScheduleId, CancellationToken ct = default)
    {
        if (sourceScheduleId == targetScheduleId) return 0;

        // Idempotentlik: nishonda kartochka bo'lsa aralashmaymiz.
        var occupied = await _context.Cards
            .AsNoTracking()
            .AnyAsync(c => c.ScheduleId == targetScheduleId, ct)
            .ConfigureAwait(false);

        if (occupied) return 0;

        var source = await _context.Cards
            .AsNoTracking()
            .Where(c => c.ScheduleId == sourceScheduleId)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.LessonId,
                c.PeriodId,
                c.DayNo,
                c.WeeksMask,
                c.Length,
                c.IsLocked,
                c.LegacyRoomNumber,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (source.Count == 0) return 0;

        // Xona tayinlashlari — kartochka Id si bo'yicha.
        var roomsByCard = (await _context.CardClassrooms
                .AsNoTracking()
                .Where(cc => cc.Card!.ScheduleId == sourceScheduleId)
                .Select(cc => new { cc.CardId, cc.ClassroomId })
                .ToListAsync(ct)
                .ConfigureAwait(false))
            .GroupBy(x => x.CardId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ClassroomId).ToList());

        var pairs = new List<(int SourceId, Card Copy)>(source.Count);

        foreach (var item in source)
        {
            var copy = new Card
            {
                ScheduleId = targetScheduleId,
                LessonId = item.LessonId,
                PeriodId = item.PeriodId,
                DayNo = item.DayNo,
                WeeksMask = item.WeeksMask,
                Length = item.Length,
                IsLocked = item.IsLocked,
                LegacyRoomNumber = item.LegacyRoomNumber,
                // Ko'chirish izi ATAYLAB nusxalanmaydi — UX_Cards_LegacyScheduleEntryId.
                LegacyScheduleEntryId = null,
            };

            _context.Cards.Add(copy);
            pairs.Add((item.Id, copy));
        }

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        var links = 0;
        foreach (var (sourceId, copy) in pairs)
        {
            if (!roomsByCard.TryGetValue(sourceId, out var classroomIds)) continue;

            foreach (var classroomId in classroomIds)
            {
                _context.CardClassrooms.Add(new CardClassroom
                {
                    CardId = copy.Id,
                    ClassroomId = classroomId,
                });

                links++;
            }
        }

        if (links > 0)
        {
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        // Bandlik qatorlari QO'LDA yozilmaydi — yagona egasi proyektor.
        await _projector.RebuildForScheduleAsync(targetScheduleId, ct).ConfigureAwait(false);

        return pairs.Count;
    }
}
