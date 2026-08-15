using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>
/// Bazani migratsiya qiladi, boshlang'ich ma'lumotlarni to'ldiradi va eski (v1)
/// ma'lumotni v2 modeliga ko'chiradi. Hammasi <b>idempotent</b>: har startda
/// chaqirilsa ham takroriy yozuv qo'shmaydi.
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly AppDbContext _context;
    private readonly IDatabaseBackupService? _backup;
    private readonly ICardOccurrenceProjector? _projector;
    private readonly ILogger<DatabaseInitializer>? _logger;

    /// <summary>
    /// Yangi initsializator yaratadi.
    /// </summary>
    /// <param name="context">Baza konteksti.</param>
    /// <param name="backup">
    /// Migratsiya oldidan zaxira oluvchi servis. <c>null</c> bo'lsa zaxira olinmaydi
    /// (xotiradagi bazada ishlaydigan testlar shu yo'ldan foydalanadi).
    /// </param>
    /// <param name="projector">
    /// Bandlik proyektori. <c>null</c> bo'lsa eski ma'lumotni ko'chirish (backfill)
    /// O'TKAZIB YUBORILADI — proyeksiyasiz kartochka yozish taqiqlangan.
    /// </param>
    /// <param name="logger">Diagnostika jurnali (ixtiyoriy).</param>
    public DatabaseInitializer(
        AppDbContext context,
        IDatabaseBackupService? backup = null,
        ICardOccurrenceProjector? projector = null,
        ILogger<DatabaseInitializer>? logger = null)
    {
        _context = context;
        _backup = backup;
        _projector = projector;
        _logger = logger;
    }

    /// <summary>
    /// Oxirgi ishga tushirishdagi ko'chirish natijasi (test va diagnostika uchun).
    /// Ko'chirish bajarilmagan bo'lsa <c>null</c>.
    /// </summary>
    public LegacyBackfillResult? LastBackfill { get; private set; }

    /// <summary>
    /// Bazani ishga tayyorlaydi.
    /// </summary>
    /// <remarks>
    /// <b>Tartib MAJBURIY</b> (00 §4.4 va ko'chirish kafolati):
    /// <c>zaxira → xavfsiz migratsiyalar → boshlang'ich ma'lumot → ko'chirish (backfill)
    /// → buzuvchi migratsiyalar (qo'riqchi bilan)</c>.
    /// <para>
    /// Ilgari BARCHA migratsiyalar ko'chirishdan oldin qo'llanardi. Eski jadvalni
    /// tashlaydigan migratsiya paydo bo'lgach bu tartib <b>jimgina ma'lumot yo'qotish</b>
    /// bo'lardi: <c>V2_04</c> gacha yangilanmagan foydalanuvchida jadval ko'chirilishidan
    /// OLDIN tashlanardi. Endi bunday migratsiya oxirgi bosqichga suriladi va
    /// <see cref="LegacyBackfillGuard"/> uni ko'chirilmagan qator bo'lsa RAD ETADI.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Sxema YOKI ma'lumot o'zgarishidan OLDIN — foydalanuvchi bazasining to'liq
        // nusxasi (00 §4.4). Backfill ham ma'lumotni o'zgartiradi, shuning uchun
        // migratsiya kutilmayotgan bo'lsa ham ko'chirish oldidan zaxira MAJBURIY.
        await TryBackupAsync(ct).ConfigureAwait(false);

        var deferred = await MigrateSafeStageAsync(ct).ConfigureAwait(false);

        await SeedWorkDaysAsync(ct);
        await SeedLessonSlotsAsync(ct);
        await SeedScheduleAsync(ct);

        // Migratsiyalardan KEYIN: eski ScheduleEntry yozuvlari Card/CardOccurrence ga
        // ko'chadi. Busiz haqiqiy foydalanuvchi bazasida sxema yangilanadi, lekin
        // yangi jadval BO'SH ko'rinardi.
        await RunLegacyBackfillAsync(ct).ConfigureAwait(false);

        await MigrateDestructiveStageAsync(deferred, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Birinchi bosqich: eski jadvalni tashlaydigan migratsiyagacha bo'lganlarini
    /// qo'llaydi. Buzuvchisi bo'lmasa — hammasi shu yerda qo'llanadi.
    /// </summary>
    /// <returns>Ikkinchi bosqichga surilgan migratsiyalar (bo'sh bo'lishi mumkin).</returns>
    private async Task<IReadOnlyList<string>> MigrateSafeStageAsync(CancellationToken ct)
    {
        var pending = (await _context.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false))
            .ToList();

        var (safe, destructive) = LegacyBackfillGuard.Split(pending);

        if (destructive.Count == 0)
        {
            // Odatiy yo'l — hech narsa o'zgarmaydi.
            await _context.Database.MigrateAsync(ct).ConfigureAwait(false);
            return Array.Empty<string>();
        }

        _logger?.LogInformation(
            "Eski jadvalni tashlaydigan {Count} ta migratsiya ko'chirishdan KEYINGA surildi: {Names}.",
            destructive.Count, string.Join(", ", destructive));

        if (safe.Count > 0)
        {
            // Aniq nishonga migratsiya: buzuvchisi hali qo'llanmaydi.
            await _context.GetService<IMigrator>()
                .MigrateAsync(safe[^1], ct)
                .ConfigureAwait(false);
        }

        return destructive;
    }

    /// <summary>
    /// Ikkinchi bosqich: ko'chirish tugagach buzuvchi migratsiyalarni qo'llaydi —
    /// lekin FAQAT eski yozuvlarning hammasi kartochkaga o'tgan bo'lsa.
    /// </summary>
    /// <exception cref="LegacyBackfillIncompleteException">
    /// Ko'chirilmagan eski dars yozuvi qolgan. Bu holda migratsiya qo'llanmaydi va
    /// dastur ishga tushmaydi — jimgina ma'lumot yo'qotishdan ko'ra ochiq to'xtash afzal.
    /// </exception>
    private async Task MigrateDestructiveStageAsync(
        IReadOnlyList<string> deferred, CancellationToken ct)
    {
        if (deferred.Count == 0) return;

        await LegacyBackfillGuard
            .EnsureBackfilledAsync(_context, deferred[0], ct)
            .ConfigureAwait(false);

        await _context.Database.MigrateAsync(ct).ConfigureAwait(false);

        _logger?.LogInformation(
            "Eski jadvalni tashlaydigan migratsiyalar qo'llandi: {Names}.",
            string.Join(", ", deferred));
    }

    /// <summary>
    /// Zaxira nusxa oladi. Migratsiya kutilayotgan bo'lsa — har doim; aks holda
    /// faqat eski ma'lumot hali ko'chirilmagan bo'lsa (ya'ni backfill hozir yozadi).
    /// Zaxira olinmasa ham dastur ishga tushishi kerak, shuning uchun xato yutiladi.
    /// </summary>
    private async Task TryBackupAsync(CancellationToken ct)
    {
        if (_backup is null) return;

        try
        {
            var backfillPending = await LegacyDataAwaitsBackfillAsync(ct).ConfigureAwait(false);
            await _backup
                .CreateBackupAsync(onlyIfMigrationsPending: !backfillPending, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger?.LogWarning(ex, "Zaxira nusxa olinmadi — migratsiya davom etadi.");
        }
    }

    /// <summary>
    /// Sxema allaqachon joriy, lekin eski dars yozuvlari hali kartochkaga
    /// ko'chirilmaganmi. Migratsiya kutilayotgan bo'lsa <c>false</c> qaytaradi —
    /// u holda zaxira baribir olinadi va v2 jadvallari hali mavjud bo'lmasligi mumkin.
    /// </summary>
    private async Task<bool> LegacyDataAwaitsBackfillAsync(CancellationToken ct)
    {
        try
        {
            var pending = await _context.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false);
            if (pending.Any()) return false;

            if (!await _context.ScheduleEntries.AnyAsync(ct).ConfigureAwait(false)) return false;

            return !await _context.Cards.AnyAsync(ct).ConfigureAwait(false);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Jadval hali yo'q (juda eski baza) — migratsiya o'zi zaxira sababi bo'ladi.
            return false;
        }
    }

    /// <summary>
    /// Eski (v1) ma'lumotni v2 modeliga ko'chiradi. <b>Idempotent</b> — takror
    /// chaqirilsa 0 ta yangi yozuv qo'shadi.
    /// </summary>
    /// <remarks>
    /// Xato dastur ishga tushishini TO'SMAYDI: ko'chirish additiv, eski jadvallar
    /// joyida qoladi va foydalanuvchi hech narsa yo'qotmaydi. Sabab jurnalga yoziladi.
    /// </remarks>
    private async Task RunLegacyBackfillAsync(CancellationToken ct)
    {
        if (_projector is null)
        {
            _logger?.LogDebug("Bandlik proyektori yo'q — eski ma'lumot ko'chirish o'tkazib yuborildi.");
            return;
        }

        try
        {
            var result = await new LegacyToV2Backfill(_context, _projector)
                .RunAsync(ct).ConfigureAwait(false);

            LastBackfill = result;

            if (result.Cards > 0 || result.Lessons > 0 || result.SchoolClasses > 0)
            {
                _logger?.LogInformation(
                    "Eski ma'lumot ko'chirildi: sinf={Classes}, guruh={Groups}, dars={Lessons}, " +
                    "kartochka={Cards}, bandlik={Occurrences}.",
                    result.SchoolClasses, result.StudentGroups, result.Lessons,
                    result.Cards, result.CardOccurrences);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ataylab keng: ko'chirish nosozligi dasturni ishga tushirmay qo'ymasligi kerak.
            // Bekor qilish (ct) esa chaqiruvchiga qaytadi — u xato emas.
            _logger?.LogError(ex, "Eski ma'lumotni v2 modeliga ko'chirib bo'lmadi — dastur davom etadi.");
        }
    }

    /// <summary>
    /// O'quv yili va faol dars jadvalini kafolatlaydi:
    /// <list type="bullet">
    /// <item>birorta o'quv yili bo'lmasa — joriy sanadan hisoblab yaratiladi (masalan "2025–2026");</item>
    /// <item>birorta jadval bo'lmasa — "Asosiy jadval" yaratiladi va faol qilinadi;</item>
    /// <item>jadvalga biriktirilmagan (eski) dars yozuvlari o'sha jadvalga ko'chiriladi;</item>
    /// <item>hech biri faol bo'lmasa — eng eskisi faol qilinadi, bir nechtasi faol bo'lsa bittasi qoldiriladi.</item>
    /// </list>
    /// Bo'sh baza uchun ham, ma'lumot to'lgan eski baza uchun ham ishlaydi.
    /// </summary>
    private async Task SeedScheduleAsync(CancellationToken ct)
    {
        var year = await _context.AcademicYears
            .OrderByDescending(y => y.StartYear).ThenByDescending(y => y.Id)
            .FirstOrDefaultAsync(ct);

        if (year is null)
        {
            var (name, startYear) = ActiveScheduleResolver.CurrentAcademicYearName(DateTime.Now);
            year = new AcademicYear { Name = name, StartYear = startYear };
            _context.AcademicYears.Add(year);
            await _context.SaveChangesAsync(ct);
        }

        var schedules = await _context.Schedules.OrderBy(s => s.Id).ToListAsync(ct);

        Schedule active;
        if (schedules.Count == 0)
        {
            active = new Schedule
            {
                AcademicYearId = year.Id,
                Name = ActiveScheduleResolver.DefaultScheduleName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Schedules.Add(active);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            active = schedules.FirstOrDefault(s => s.IsActive) ?? schedules[0];
            var changed = false;
            foreach (var schedule in schedules)
            {
                var shouldBeActive = schedule.Id == active.Id;
                if (schedule.IsActive == shouldBeActive) continue;

                schedule.IsActive = shouldBeActive;
                changed = true;
            }

            if (changed) await _context.SaveChangesAsync(ct);
        }

        // Migratsiyadan oldingi (jadvalsiz) yozuvlar yo'qolmasligi uchun faol jadvalga biriktiriladi.
        var validIds = await _context.Schedules.Select(s => s.Id).ToListAsync(ct);
        var orphans = await _context.ScheduleEntries
            .Where(e => !validIds.Contains(e.ScheduleId))
            .ToListAsync(ct);

        if (orphans.Count == 0) return;

        foreach (var entry in orphans)
        {
            entry.ScheduleId = active.Id;
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>7 ta hafta kuni: Dushanba–Shanba faol, Yakshanba nofaol.</summary>
    private async Task SeedWorkDaysAsync(CancellationToken ct)
    {
        var existing = await _context.WorkDays
            .Select(x => x.DayOfWeek)
            .ToListAsync(ct);

        var missing = new List<WorkDay>();
        foreach (var day in AllDays)
        {
            if (existing.Contains(day)) continue;

            missing.Add(new WorkDay
            {
                DayOfWeek = day,
                IsActive = day != WeekDay.Yakshanba,
                MaxLessonsPerDay = 7
            });
        }

        if (missing.Count == 0) return;

        await _context.WorkDays.AddRangeAsync(missing, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>7 ta dars soati: 08:30 dan, 45 daqiqa dars + 10 daqiqa tanaffus.</summary>
    private async Task SeedLessonSlotsAsync(CancellationToken ct)
    {
        var existing = await _context.LessonSlots
            .Select(x => x.LessonNumber)
            .ToListAsync(ct);

        var missing = new List<LessonSlot>();
        foreach (var slot in DefaultLessonSlots())
        {
            if (existing.Contains(slot.LessonNumber)) continue;
            missing.Add(slot);
        }

        if (missing.Count == 0) return;

        await _context.LessonSlots.AddRangeAsync(missing, ct);
        await _context.SaveChangesAsync(ct);
    }

    private static readonly WeekDay[] AllDays =
    {
        WeekDay.Dushanba, WeekDay.Seshanba, WeekDay.Chorshanba, WeekDay.Payshanba,
        WeekDay.Juma, WeekDay.Shanba, WeekDay.Yakshanba
    };

    /// <summary>
    /// 1: 08:30–09:15, 2: 09:25–10:10, 3: 10:20–11:05, 4: 11:15–12:00,
    /// 5: 12:10–12:55, 6: 13:05–13:50, 7: 14:00–14:45
    /// </summary>
    private static IEnumerable<LessonSlot> DefaultLessonSlots()
    {
        const int lessonMinutes = 45;
        const int breakMinutes = 10;
        var start = new TimeSpan(8, 30, 0);

        for (var number = 1; number <= 7; number++)
        {
            var end = start + TimeSpan.FromMinutes(lessonMinutes);
            yield return new LessonSlot
            {
                LessonNumber = number,
                StartTime = start,
                EndTime = end
            };
            start = end + TimeSpan.FromMinutes(breakMinutes);
        }
    }
}
