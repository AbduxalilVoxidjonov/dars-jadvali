using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence.Scheduling;

/// <summary>
/// Generatsiya uchun EF o'qish/yozish. Barcha so'rovlar <b>bitta jadval varianti</b> va
/// uning o'quv yili bilan cheklangan hamda <c>AsNoTracking</c>.
/// </summary>
/// <remarks>
/// Eski yo'l (<c>ScheduleSnapshot</c>) har chaqiruvda 8 ta to'liq <c>SELECT *</c> qilib,
/// <b>barcha o'quv yillari</b> yozuvlarini o'qir va keyin xotirada filtrlar edi
/// (05-audit K-07/K-19). Bu yerda har bir so'rov <c>WHERE AcademicYearId = @year</c>
/// yoki <c>WHERE ScheduleId = @schedule</c> bilan cheklanadi.
/// </remarks>
public sealed class EfSchedulingStore : ISchedulingStore
{
    private readonly AppDbContext _context;

    public EfSchedulingStore(AppDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public async Task<SchedulingInput> LoadAsync(int scheduleId, CancellationToken ct = default)
    {
        var schedule = await _context.Schedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == scheduleId, ct)
            .ConfigureAwait(false);

        if (schedule is null)
        {
            throw new SchedulingMappingException($"Dars jadvali topilmadi (ID: {scheduleId}).");
        }

        var year = await _context.AcademicYears
            .AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == schedule.AcademicYearId, ct)
            .ConfigureAwait(false);

        if (year is null)
        {
            throw new SchedulingMappingException(
                $"«{schedule.Name}» jadvalining o'quv yili topilmadi (ID: {schedule.AcademicYearId}).");
        }

        var yearId = year.Id;

        // Ish kunlari eski modelda global (AcademicYearId = null) bo'lishi mumkin.
        var workDays = await _context.WorkDays
            .AsNoTracking()
            .Where(w => w.AcademicYearId == null || w.AcademicYearId == yearId)
            .OrderBy(w => w.DayNo).ThenBy(w => w.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var periods = await _context.Periods
            .AsNoTracking()
            .Where(p => p.AcademicYearId == yearId)
            .OrderBy(p => p.PeriodNo)
            .ToListAsync(ct).ConfigureAwait(false);

        var shifts = await _context.Shifts
            .AsNoTracking()
            .Where(s => s.AcademicYearId == yearId)
            .OrderBy(s => s.ShiftNo)
            .ToListAsync(ct).ConfigureAwait(false);

        var classes = await _context.SchoolClasses
            .AsNoTracking()
            .Where(c => c.AcademicYearId == yearId && !c.IsDeleted)
            .OrderBy(c => c.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var classIds = classes.Select(c => c.Id).ToList();

        var divisions = await _context.ClassDivisions
            .AsNoTracking()
            .Where(d => classIds.Contains(d.SchoolClassId))
            .OrderBy(d => d.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var groups = await _context.StudentGroups
            .AsNoTracking()
            .Where(g => classIds.Contains(g.SchoolClassId) && !g.IsDeleted)
            .OrderBy(g => g.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        // O'qituvchi/fan eski yozuvlarda yilga bog'lanmagan bo'lishi mumkin.
        var teachers = await _context.Teachers
            .AsNoTracking()
            .Where(t => !t.IsDeleted && (t.AcademicYearId == null || t.AcademicYearId == yearId))
            .OrderBy(t => t.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var subjects = await _context.Subjects
            .AsNoTracking()
            .Where(s => !s.IsDeleted && (s.AcademicYearId == null || s.AcademicYearId == yearId))
            .OrderBy(s => s.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var classrooms = await _context.Classrooms
            .AsNoTracking()
            .Where(c => c.AcademicYearId == yearId && !c.IsDeleted)
            .OrderBy(c => c.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var lessons = await _context.Lessons
            .AsNoTracking()
            .Where(l => l.AcademicYearId == yearId)
            .OrderBy(l => l.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var lessonIds = lessons.Select(l => l.Id).ToList();

        var lessonTeachers = await _context.LessonTeachers
            .AsNoTracking()
            .Where(x => lessonIds.Contains(x.LessonId))
            .ToListAsync(ct).ConfigureAwait(false);

        var lessonClasses = await _context.LessonClasses
            .AsNoTracking()
            .Where(x => lessonIds.Contains(x.LessonId))
            .ToListAsync(ct).ConfigureAwait(false);

        var lessonGroups = await _context.LessonGroups
            .AsNoTracking()
            .Where(x => lessonIds.Contains(x.LessonId))
            .ToListAsync(ct).ConfigureAwait(false);

        var lessonClassrooms = await _context.LessonClassrooms
            .AsNoTracking()
            .Where(x => lessonIds.Contains(x.LessonId))
            .ToListAsync(ct).ConfigureAwait(false);

        var timeOffs = await _context.TimeOffs
            .AsNoTracking()
            .Where(t => t.AcademicYearId == yearId)
            .OrderBy(t => t.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var lockedCards = await _context.Cards
            .AsNoTracking()
            .Where(c => c.ScheduleId == scheduleId && c.IsLocked)
            .OrderBy(c => c.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        return new SchedulingInput
        {
            Schedule = schedule,
            Year = year,
            WorkDays = workDays,
            Periods = periods,
            Shifts = shifts,
            Classes = classes,
            Divisions = divisions,
            Groups = groups,
            Teachers = teachers,
            Subjects = subjects,
            Classrooms = classrooms,
            Lessons = lessons,
            LessonTeachers = lessonTeachers,
            LessonClasses = lessonClasses,
            LessonGroups = lessonGroups,
            LessonClassrooms = lessonClassrooms,
            TimeOffs = timeOffs,
            LockedCards = lockedCards,
        };
    }

    /// <inheritdoc />
    public async Task<int> DeleteCardsAsync(
        int scheduleId, bool keepLocked, CancellationToken ct = default)
    {
        // Bandlik qatorlari kartochkaga kaskad bilan bog'langan, lekin ular oldin
        // o'chiriladi: shunda unikal indeks oraliq holatda ham buzilmaydi.
        var occurrences = _context.CardOccurrences.Where(o => o.ScheduleId == scheduleId);
        var cards = _context.Cards.Where(c => c.ScheduleId == scheduleId);

        if (keepLocked)
        {
            occurrences = _context.CardOccurrences
                .Where(o => o.ScheduleId == scheduleId &&
                            _context.Cards.Any(c => c.Id == o.CardId && !c.IsLocked));
            cards = cards.Where(c => !c.IsLocked);
        }

        await occurrences.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        var removed = await cards.ExecuteDeleteAsync(ct).ConfigureAwait(false);

        // ExecuteDelete change tracker'ni chetlab o'tadi — bazadan yo'q bo'lgan
        // kartochkalar keshda "mavjud" bo'lib qolmasligi uchun ularni ajratamiz.
        Detach<Card>(e => e.ScheduleId == scheduleId && (!keepLocked || !e.IsLocked));
        Detach<CardOccurrence>(e => e.ScheduleId == scheduleId);
        return removed;
    }

    /// <summary>Shartga mos kuzatilayotgan yozuvlarni change tracker'dan ajratadi.</summary>
    private void Detach<TEntity>(Func<TEntity, bool> predicate) where TEntity : class
    {
        foreach (var entry in _context.ChangeTracker.Entries<TEntity>().ToList())
        {
            if (predicate(entry.Entity)) entry.State = EntityState.Detached;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> InsertCardsAsync(
        IReadOnlyList<CardWrite> cards, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (cards.Count == 0) return Array.Empty<int>();

        var entities = new List<Card>(cards.Count);
        foreach (var card in cards)
        {
            entities.Add(new Card
            {
                ScheduleId = card.ScheduleId,
                LessonId = card.LessonId,
                PeriodId = card.PeriodId,
                DayNo = card.DayNo,
                WeeksMask = card.WeeksMask <= 0 ? 1 : card.WeeksMask,
                // Uzunlik har kartochkada alohida ("2 + 2 + 1" qoldig'i shu yerda saqlanadi).
                Length = card.Length <= 0 ? 1 : card.Length,
                IsLocked = card.IsLocked,
                // Xonalar ma'lumotnomasi to'ldirilmagan maktabda xona faqat matn
                // bo'lib qoladi — foydalanuvchi kiritgan qiymat yo'qolmasin.
                LegacyRoomNumber = string.IsNullOrWhiteSpace(card.RoomNumber)
                    ? null
                    : card.RoomNumber.Trim(),
            });
        }

        await _context.Cards.AddRangeAsync(entities, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        var rooms = new List<CardClassroom>();
        for (var i = 0; i < cards.Count; i++)
        {
            foreach (var roomId in cards[i].ClassroomIds)
            {
                rooms.Add(new CardClassroom { CardId = entities[i].Id, ClassroomId = roomId });
            }
        }

        if (rooms.Count > 0)
        {
            await _context.CardClassrooms.AddRangeAsync(rooms, ct).ConfigureAwait(false);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return entities.Select(e => e.Id).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlacedCardView>> LoadPlacedCardsAsync(
        int scheduleId, CancellationToken ct = default)
    {
        var rows = await (
            from card in _context.Cards.AsNoTracking()
            where card.ScheduleId == scheduleId
            join lesson in _context.Lessons.AsNoTracking() on card.LessonId equals lesson.Id
            join subject in _context.Subjects.AsNoTracking() on lesson.SubjectId equals subject.Id
            join period in _context.Periods.AsNoTracking() on card.PeriodId equals period.Id
            select new
            {
                card.Id,
                card.LessonId,
                card.DayNo,
                card.WeeksMask,
                card.Length,
                period.PeriodNo,
                SubjectName = subject.Name,
            }).ToListAsync(ct).ConfigureAwait(false);

        if (rows.Count == 0) return Array.Empty<PlacedCardView>();

        var lessonIds = rows.Select(r => r.LessonId).Distinct().ToList();

        var groupRows = await (
            from link in _context.LessonGroups.AsNoTracking()
            where lessonIds.Contains(link.LessonId)
            join grp in _context.StudentGroups.AsNoTracking() on link.StudentGroupId equals grp.Id
            join division in _context.ClassDivisions.AsNoTracking() on grp.ClassDivisionId equals division.Id
            join cls in _context.SchoolClasses.AsNoTracking() on grp.SchoolClassId equals cls.Id
            select new
            {
                link.LessonId,
                GroupId = grp.Id,
                GroupName = grp.Name,
                grp.IsEntireClass,
                grp.SchoolClassId,
                ClassName = cls.Name,
                division.DivisionTag,
            }).ToListAsync(ct).ConfigureAwait(false);

        var groupsByLesson = groupRows
            .GroupBy(g => g.LessonId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PlacedGroupRef>)g
                    .Select(x => new PlacedGroupRef(
                        x.GroupId,
                        x.GroupName,
                        x.SchoolClassId,
                        x.ClassName,
                        // "Butun sinf" har doim 0-teg; qolganlari kamida 1.
                        x.IsEntireClass ? 0 : Math.Max(1, x.DivisionTag)))
                    .ToList());

        return rows
            .Select(r => new PlacedCardView(
                r.Id,
                r.SubjectName,
                r.DayNo,
                r.PeriodNo,
                Math.Max(1, r.Length),
                r.WeeksMask,
                groupsByLesson.TryGetValue(r.LessonId, out var groups)
                    ? groups
                    : Array.Empty<PlacedGroupRef>()))
            .ToList();
    }

    // =====================================================================
    // Jadval to'ri (UI) uchun o'qish/yozish
    // =====================================================================

    /// <inheritdoc />
    public async Task<IReadOnlyList<CardView>> LoadCardViewsAsync(
        int scheduleId, CancellationToken ct = default)
    {
        var rows = await (
            from card in _context.Cards.AsNoTracking()
            where card.ScheduleId == scheduleId
            join lesson in _context.Lessons.AsNoTracking() on card.LessonId equals lesson.Id
            join subject in _context.Subjects.AsNoTracking() on lesson.SubjectId equals subject.Id
            join period in _context.Periods.AsNoTracking() on card.PeriodId equals period.Id
            orderby card.DayNo, period.PeriodNo, card.Id
            select new
            {
                card.Id,
                card.LessonId,
                card.ScheduleId,
                card.DayNo,
                card.PeriodId,
                card.Length,
                card.WeeksMask,
                card.IsLocked,
                card.LegacyRoomNumber,
                period.PeriodNo,
                lesson.SubjectId,
                SubjectName = subject.Name,
            }).ToListAsync(ct).ConfigureAwait(false);

        if (rows.Count == 0) return Array.Empty<CardView>();

        var lessonIds = rows.Select(r => r.LessonId).Distinct().ToList();
        var links = await LoadLessonLinksAsync(lessonIds, ct).ConfigureAwait(false);

        // V2_07: xona endi TAYINLANGAN bog'lanishdan olinadi. Eski matn ustuni
        // (LegacyRoomNumber) faqat bog'lanish yo'q kartochkalar uchun zaxira sifatida
        // qoladi — Desktop/Web hali unga tayanadi.
        var rooms = await LoadCardRoomsAsync(rows.Select(r => r.Id).ToList(), ct).ConfigureAwait(false);

        return rows
            .Select(r =>
            {
                var link = links.For(r.LessonId);
                rooms.TryGetValue(r.Id, out var room);
                return new CardView(
                    CardId: r.Id,
                    ScheduleId: r.ScheduleId,
                    LessonId: r.LessonId,
                    SubjectId: r.SubjectId,
                    SubjectName: r.SubjectName,
                    TeacherIds: link.TeacherIds,
                    TeacherNames: link.TeacherNames,
                    SchoolClassIds: link.ClassIds,
                    ClassName: link.ClassName,
                    StudentGroupIds: link.GroupIds,
                    GroupName: link.GroupName,
                    DayNo: r.DayNo,
                    PeriodId: r.PeriodId,
                    PeriodNo: r.PeriodNo,
                    Length: Math.Max(1, r.Length),
                    WeeksMask: r.WeeksMask <= 0 ? 1 : r.WeeksMask,
                    IsLocked: r.IsLocked,
                    RoomNumber: room.Name ?? r.LegacyRoomNumber)
                {
                    ClassroomIds = room.Ids ?? Array.Empty<int>(),
                };
            })
            .ToList();
    }

    /// <summary>Kartochka → tayinlangan xonalar (Id'lar va ko'rsatiladigan nom).</summary>
    private async Task<Dictionary<int, (int[] Ids, string? Name)>> LoadCardRoomsAsync(
        IReadOnlyList<int> cardIds, CancellationToken ct)
    {
        var result = new Dictionary<int, (int[], string?)>();
        if (cardIds.Count == 0) return result;

        var rows = await (
            from link in _context.CardClassrooms.AsNoTracking()
            where cardIds.Contains(link.CardId)
            join room in _context.Classrooms.AsNoTracking() on link.ClassroomId equals room.Id
            orderby link.CardId, room.Id
            select new { link.CardId, room.Id, room.Name, room.ShortName })
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var group in rows.GroupBy(x => x.CardId))
        {
            var name = string.Join(", ", group.Select(x =>
                string.IsNullOrWhiteSpace(x.ShortName) ? x.Name : x.ShortName));

            result[group.Key] = (group.Select(x => x.Id).ToArray(), name);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnplacedLessonView>> LoadUnplacedLessonsAsync(
        int scheduleId, CancellationToken ct = default)
    {
        var schedule = await _context.Schedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == scheduleId, ct)
            .ConfigureAwait(false);

        if (schedule is null) return Array.Empty<UnplacedLessonView>();

        var yearId = schedule.AcademicYearId;

        var lessons = await (
            from lesson in _context.Lessons.AsNoTracking()
            where lesson.AcademicYearId == yearId
            join subject in _context.Subjects.AsNoTracking() on lesson.SubjectId equals subject.Id
            orderby lesson.Id
            select new
            {
                lesson.Id,
                lesson.SubjectId,
                lesson.PeriodsPerWeek,
                lesson.PeriodsPerCard,
                SubjectName = subject.Name,
            }).ToListAsync(ct).ConfigureAwait(false);

        if (lessons.Count == 0) return Array.Empty<UnplacedLessonView>();

        // Qo'yilgan soat = kartochkalar UZUNLIKLARI yig'indisi (soni emas — juft dars ikki soat).
        var placed = await _context.Cards
            .AsNoTracking()
            .Where(c => c.ScheduleId == scheduleId)
            .GroupBy(c => c.LessonId)
            .Select(g => new { LessonId = g.Key, Periods = g.Sum(c => c.Length) })
            .ToDictionaryAsync(x => x.LessonId, x => x.Periods, ct)
            .ConfigureAwait(false);

        var links = await LoadLessonLinksAsync(lessons.Select(l => l.Id).ToList(), ct).ConfigureAwait(false);

        var result = new List<UnplacedLessonView>();
        foreach (var lesson in lessons)
        {
            placed.TryGetValue(lesson.Id, out var placedPeriods);
            if (placedPeriods >= lesson.PeriodsPerWeek) continue;

            var link = links.For(lesson.Id);
            result.Add(new UnplacedLessonView(
                LessonId: lesson.Id,
                SubjectId: lesson.SubjectId,
                SubjectName: lesson.SubjectName,
                ClassName: link.ClassName,
                GroupName: link.GroupName,
                TeacherIds: link.TeacherIds,
                TeacherNames: link.TeacherNames,
                PeriodsPerWeek: lesson.PeriodsPerWeek,
                PlacedPeriods: placedPeriods,
                PeriodsPerCard: Math.Max(1, lesson.PeriodsPerCard))
            {
                // Prezentatsiya qatlami sinfni NOM bo'yicha tiklamasligi uchun.
                SchoolClassIds = link.ClassIds,
                StudentGroupIds = link.GroupIds,
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CardOccupancy>> LoadOccupancyAsync(
        int scheduleId, CancellationToken ct = default)
    {
        // Struct konstruktoriga proyeksiya SQL'ga tushmasligi mumkin — avval anonim tur.
        var rows = await _context.CardOccurrences
            .AsNoTracking()
            .Where(o => o.ScheduleId == scheduleId)
            .Select(o => new { o.CardId, o.DayNo, o.PeriodNo, o.WeekNo, o.ResourceKind, o.ResourceId })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .Select(o => new CardOccupancy(
                o.CardId, o.DayNo, o.PeriodNo, o.WeekNo, o.ResourceKind, o.ResourceId))
            .ToList();
    }

    /// <inheritdoc />
    public Task<bool> MoveCardAsync(
        int cardId, int dayNo, int periodId, int? weeksMask, CancellationToken ct = default)
        => MoveOneAsync(cardId, dayNo, periodId, weeksMask, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <b>Tartib muhim.</b> <c>UX_Cards_Schedule_Lesson_Day_Period_Weeks</c> unikal indeksi
    /// har bir <c>UPDATE</c> dan keyin tekshiriladi, shuning uchun ikki kartochkani
    /// "o'rin almashtirish" to'g'ridan-to'g'ri bajarilsa ORALIQ holatda indeks buzilardi.
    /// Bu yerda avval kalitlari bo'sh bo'lgan ko'chirishlar bajariladi; sikl qolsa
    /// undagi bitta kartochka vaqtincha bo'sh o'ringa "qo'yib turiladi".
    /// </remarks>
    public async Task<int> MoveCardsAsync(
        IReadOnlyList<CardPlacement> placements, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placements);
        if (placements.Count == 0) return 0;

        var ids = placements.Select(p => p.CardId).Distinct().ToList();
        var cards = await _context.Cards
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct).ConfigureAwait(false);

        if (cards.Count == 0) return 0;

        var scheduleIds = cards.Select(c => c.ScheduleId).Distinct().ToList();

        // Jadvaldagi BARCHA kartochkalarning joriy kalitlari (indeks holatining nusxasi).
        var occupied = (await _context.Cards
                .AsNoTracking()
                .Where(c => scheduleIds.Contains(c.ScheduleId))
                .Select(c => new { c.Id, c.ScheduleId, c.LessonId, c.DayNo, c.PeriodId, c.WeeksMask })
                .ToListAsync(ct).ConfigureAwait(false))
            .ToDictionary(
                c => (c.ScheduleId, c.LessonId, c.DayNo, c.PeriodId, c.WeeksMask),
                c => c.Id);

        var periodIds = await _context.Periods
            .AsNoTracking()
            .Where(p => !p.IsBreak)
            .OrderBy(p => p.PeriodNo)
            .Select(p => p.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var byId = cards.ToDictionary(c => c.Id);
        var pending = placements.Where(p => byId.ContainsKey(p.CardId)).ToList();
        var moved = 0;

        while (pending.Count > 0)
        {
            var progressed = false;

            foreach (var placement in pending.ToList())
            {
                var card = byId[placement.CardId];
                var mask = placement.WeeksMask is int m && m > 0 ? m : card.WeeksMask;
                var key = (card.ScheduleId, card.LessonId, placement.DayNo, placement.PeriodId, mask);

                if (occupied.TryGetValue(key, out var owner) && owner != card.Id) continue;

                await ApplyAsync(card, placement.DayNo, placement.PeriodId, mask, occupied, ct)
                    .ConfigureAwait(false);

                pending.Remove(placement);
                progressed = true;
                moved++;
            }

            if (progressed || pending.Count == 0) continue;

            // Sikl: bitta kartochkani vaqtincha bo'sh o'ringa qo'yib turamiz.
            var stuck = byId[pending[0].CardId];
            var free = FindFreeKey(stuck, periodIds, occupied);
            if (free is null)
            {
                throw new InvalidOperationException(
                    "Kartochkalarni ko'chirib bo'lmadi: vaqtincha bo'sh o'rin topilmadi.");
            }

            await ApplyAsync(stuck, free.Value.DayNo, free.Value.PeriodId, free.Value.Mask, occupied, ct)
                .ConfigureAwait(false);
        }

        return moved;
    }

    /// <summary>Kartochkani yangi o'ringa yozadi va kalit xaritasini yangilaydi.</summary>
    private async Task ApplyAsync(
        Card card, int dayNo, int periodId, int weeksMask,
        Dictionary<(int, int, int, int, int), int> occupied, CancellationToken ct)
    {
        occupied.Remove((card.ScheduleId, card.LessonId, card.DayNo, card.PeriodId, card.WeeksMask));

        card.DayNo = dayNo;
        card.PeriodId = periodId;
        card.WeeksMask = weeksMask;

        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        occupied[(card.ScheduleId, card.LessonId, dayNo, periodId, weeksMask)] = card.Id;
    }

    /// <summary>Shu dars uchun band bo'lmagan birinchi (kun, soat) juftligi.</summary>
    private static (int DayNo, int PeriodId, int Mask)? FindFreeKey(
        Card card, IReadOnlyList<int> periodIds, Dictionary<(int, int, int, int, int), int> occupied)
    {
        // CK_Cards_DayNo: 0..13.
        for (var dayNo = 0; dayNo <= 13; dayNo++)
        {
            foreach (var periodId in periodIds)
            {
                var key = (card.ScheduleId, card.LessonId, dayNo, periodId, card.WeeksMask);
                if (!occupied.ContainsKey(key)) return (dayNo, periodId, card.WeeksMask);
            }
        }

        return null;
    }

    private async Task<bool> MoveOneAsync(
        int cardId, int dayNo, int periodId, int? weeksMask, CancellationToken ct)
    {
        var moved = await MoveCardsAsync(
            new[] { new CardPlacement(cardId, dayNo, periodId, weeksMask) }, ct).ConfigureAwait(false);

        return moved > 0;
    }

    /// <inheritdoc />
    public async Task<bool> SetCardLockAsync(int cardId, bool isLocked, CancellationToken ct = default)
    {
        var card = await _context.Cards.FirstOrDefaultAsync(c => c.Id == cardId, ct).ConfigureAwait(false);
        if (card is null) return false;

        if (card.IsLocked == isLocked) return true;

        card.IsLocked = isLocked;
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteCardAsync(int cardId, CancellationToken ct = default)
    {
        var exists = await _context.Cards
            .AsNoTracking()
            .AnyAsync(c => c.Id == cardId, ct)
            .ConfigureAwait(false);

        if (!exists) return false;

        // Bandlik qatorlari AVVAL o'chadi — DeleteCardsAsync dagi bilan bir xil qoida:
        // unikal indeks oraliq holatda ham buzilmasin.
        await _context.CardOccurrences
            .Where(o => o.CardId == cardId)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        await _context.CardClassrooms
            .Where(r => r.CardId == cardId)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        await _context.Cards
            .Where(c => c.Id == cardId)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);

        // ExecuteDelete change tracker'ni chetlab o'tadi.
        Detach<Card>(e => e.Id == cardId);
        Detach<CardOccurrence>(e => e.CardId == cardId);
        Detach<CardClassroom>(e => e.CardId == cardId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetClassShiftAsync(
        int schoolClassId, int? shiftId, CancellationToken ct = default)
    {
        var schoolClass = await _context.SchoolClasses
            .FirstOrDefaultAsync(c => c.Id == schoolClassId, ct)
            .ConfigureAwait(false);

        if (schoolClass is null) return false;

        if (shiftId is int id)
        {
            // Smena BOSHQA o'quv yiliga tegishli bo'lsa rad etiladi: aks holda sinf
            // hech qachon ochilmaydigan dars soatlariga bog'lanib qolardi.
            var valid = await _context.Shifts
                .AsNoTracking()
                .AnyAsync(s => s.Id == id && s.AcademicYearId == schoolClass.AcademicYearId, ct)
                .ConfigureAwait(false);

            if (!valid) return false;
        }

        if (schoolClass.ShiftId == shiftId) return true;

        schoolClass.ShiftId = shiftId;
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    // ---------------------------------------------------------------------

    /// <summary>Dars ↔ o'qituvchi / sinf / guruh bog'lanishlarining o'qish keshi.</summary>
    private async Task<LessonLinkCache> LoadLessonLinksAsync(
        IReadOnlyList<int> lessonIds, CancellationToken ct)
    {
        if (lessonIds.Count == 0) return LessonLinkCache.Empty;

        var teacherRows = await (
            from link in _context.LessonTeachers.AsNoTracking()
            where lessonIds.Contains(link.LessonId)
            join teacher in _context.Teachers.AsNoTracking() on link.TeacherId equals teacher.Id
            orderby link.LessonId, teacher.Id
            select new { link.LessonId, teacher.Id, teacher.FullName })
            .ToListAsync(ct).ConfigureAwait(false);

        var classRows = await (
            from link in _context.LessonClasses.AsNoTracking()
            where lessonIds.Contains(link.LessonId)
            join cls in _context.SchoolClasses.AsNoTracking() on link.SchoolClassId equals cls.Id
            orderby link.LessonId, cls.Id
            select new { link.LessonId, cls.Id, cls.Name })
            .ToListAsync(ct).ConfigureAwait(false);

        var groupRows = await (
            from link in _context.LessonGroups.AsNoTracking()
            where lessonIds.Contains(link.LessonId)
            join grp in _context.StudentGroups.AsNoTracking() on link.StudentGroupId equals grp.Id
            join cls in _context.SchoolClasses.AsNoTracking() on grp.SchoolClassId equals cls.Id
            orderby link.LessonId, grp.Id
            select new
            {
                link.LessonId,
                grp.Id,
                grp.Name,
                grp.IsEntireClass,
                grp.SchoolClassId,
                ClassName = cls.Name,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var cache = new LessonLinkCache();

        foreach (var group in teacherRows.GroupBy(x => x.LessonId))
        {
            cache.Teachers[group.Key] = (
                group.Select(x => x.Id).ToList(),
                group.Select(x => x.FullName).ToList());
        }

        foreach (var group in classRows.GroupBy(x => x.LessonId))
        {
            cache.Classes[group.Key] = (
                group.Select(x => x.Id).ToList(),
                string.Join(", ", group.Select(x => x.Name)));
        }

        foreach (var group in groupRows.GroupBy(x => x.LessonId))
        {
            // Butun sinf darsi bo'lsa guruh nomi KO'RSATILMAYDI — kartada ortiqcha yozuv bo'lmaydi.
            var names = group.Where(x => !x.IsEntireClass).Select(x => x.Name).ToList();

            cache.Groups[group.Key] = (
                group.Select(x => x.Id).ToList(),
                string.Join(", ", names),
                // Dars faqat guruhga bog'langan bo'lsa sinf nomi guruhdan olinadi.
                string.Join(", ", group.Select(x => x.ClassName).Distinct()),
                group.Select(x => x.SchoolClassId).Distinct().ToList());
        }

        return cache;
    }

    /// <summary>Bir dars uchun yig'ilgan nomlar.</summary>
    private sealed record LessonLink(
        IReadOnlyList<int> TeacherIds,
        IReadOnlyList<string> TeacherNames,
        IReadOnlyList<int> ClassIds,
        string ClassName,
        IReadOnlyList<int> GroupIds,
        string GroupName);

    private sealed class LessonLinkCache
    {
        internal static LessonLinkCache Empty { get; } = new();

        internal Dictionary<int, (List<int> Ids, List<string> Names)> Teachers { get; } = new();

        internal Dictionary<int, (List<int> Ids, string Name)> Classes { get; } = new();

        internal Dictionary<int, (List<int> Ids, string GroupName, string ClassName, List<int> ClassIds)> Groups { get; } = new();

        internal LessonLink For(int lessonId)
        {
            Teachers.TryGetValue(lessonId, out var teachers);
            Classes.TryGetValue(lessonId, out var classes);
            Groups.TryGetValue(lessonId, out var groups);

            var hasClassLinks = classes.Ids is { Count: > 0 };
            var className = hasClassLinks ? classes.Name : groups.ClassName ?? string.Empty;

            // Dars faqat guruhga bog'langan bo'lsa (LessonClass qatorlari yo'q) sinf
            // Id'lari GURUHDAN tiklanadi — shunda prezentatsiya qatlami sinfni
            // hech qachon NOM bo'yicha izlashga majbur bo'lmaydi.
            var classIds = hasClassLinks
                ? classes.Ids
                : groups.ClassIds ?? new List<int>();

            return new LessonLink(
                teachers.Ids ?? new List<int>(),
                teachers.Names ?? new List<string>(),
                classIds,
                className,
                groups.Ids ?? new List<int>(),
                groups.GroupName ?? string.Empty);
        }
    }
}
