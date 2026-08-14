using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>
/// Bazani migratsiya qiladi va boshlang'ich ma'lumotlarni to'ldiradi.
/// Seed <b>idempotent</b>: har startda chaqirilsa ham takroriy yozuv qo'shmaydi.
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly AppDbContext _context;

    public DatabaseInitializer(AppDbContext context) => _context = context;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _context.Database.MigrateAsync(ct);
        await SeedWorkDaysAsync(ct);
        await SeedLessonSlotsAsync(ct);
        await SeedScheduleAsync(ct);
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
