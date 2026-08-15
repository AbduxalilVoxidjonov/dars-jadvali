using DarsJadvali.Application.Scheduling;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Projection;
using DarsJadvali.Infrastructure.Persistence.Scheduling;
using DarsJadvali.Scheduling.Pipeline;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// Haqiqiy foydalanuvchi bazasining <b>nusxasi</b> ustida uchdan-uchgacha generatsiya.
/// </summary>
/// <remarks>
/// Baza nusxasi ishlab chiqish muhitidagi vaqtinchalik fayl bo'lgani uchun test u
/// mavjud bo'lmasa o'tkazib yuboriladi (CI'da fayl bo'lmaydi). Asl fayl HECH QACHON
/// o'zgartirilmaydi — har doim vaqtinchalik nusxa ochiladi.
/// </remarks>
public class RealDatabaseGenerationTests
{
    private const string SnapshotPath =
        "/private/tmp/claude-501/-Users-me-Projects-TimeTables-TimeTables/" +
        "5bbb8945-b767-4f98-917b-ea9279e39548/scratchpad/snapshot.db";

    private static bool SnapshotAvailable => File.Exists(SnapshotPath);

    private static string CopySnapshot()
    {
        var target = Path.Combine(Path.GetTempPath(), $"dj-gen-{Guid.NewGuid():N}.db");
        File.Copy(SnapshotPath, target, overwrite: true);

        // Nusxa dasturdagidek migratsiya qilinadi — shu bilan yangi migratsiyalar
        // (V2_05: Card.Length, FK Restrict, faol jadval unikal indeksi) HAQIQIY
        // foydalanuvchi ma'lumoti ustida ham sinaladi. Asl fayl tegilmaydi.
        using (var context = OpenCopy(target))
        {
            context.Database.Migrate();
        }

        return target;
    }

    private static AppDbContext OpenCopy(string path)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true };
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(builder.ToString()).Options);
    }

    private static ScheduleGenerationService BuildService(AppDbContext context) =>
        new(new UnitOfWork(context),
            new EfSchedulingStore(context),
            new SchedulingMapper(),
            new CardOccurrenceProjector(context));

    [Fact]
    public async Task Haqiqiy_baza_nusxasida_generatsiya_uchdan_uchgacha_ishlaydi()
    {
        if (!SnapshotAvailable) return;

        // Arrange
        var path = CopySnapshot();
        try
        {
            int scheduleId, cardsBefore;
            await using (var probe = OpenCopy(path))
            {
                scheduleId = await probe.Schedules.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();
                cardsBefore = await probe.Cards.CountAsync(c => c.ScheduleId == scheduleId);
            }

            Assert.True(cardsBefore > 0, "Nusxada kartochkalar yo'q — test ma'nosini yo'qotadi.");

            // Act
            ScheduleGenerationReport report;
            await using (var context = OpenCopy(path))
            {
                report = await BuildService(context).GenerateAsync(new ScheduleGenerationOptions
                {
                    ScheduleId = scheduleId,
                    Seed = 2026,
                    Complexity = Complexity.Small,
                    TimeLimit = TimeSpan.FromSeconds(10)
                });
            }

            // Assert
            await using (var check = OpenCopy(path))
            {
                Assert.True(report.Applied, string.Join(" | ", report.Messages));
                Assert.True(report.PlacedCards > 0);

                var cards = await check.Cards.AsNoTracking()
                    .Where(c => c.ScheduleId == scheduleId)
                    .ToListAsync();

                Assert.Equal(report.PlacedCards, cards.Count);

                // Faol ish kunlari — 0..4 (dushanba–juma), dam olish kunlariga dars qo'yilmaydi.
                var activeDays = await check.WorkDays.AsNoTracking()
                    .Where(w => w.IsActive).Select(w => w.DayNo).ToListAsync();
                Assert.All(cards, c => Assert.Contains(c.DayNo, activeDays));

                // Har kartochkaning WeeksMask > 0 (CHECK cheklovi) va bandlik qayta qurilgan.
                Assert.All(cards, c => Assert.True(c.WeeksMask > 0));
                Assert.Equal(report.OccurrenceRows,
                    await check.CardOccurrences.CountAsync(o => o.ScheduleId == scheduleId));

                // Bo'linish ziddiyati yo'q.
                Assert.Empty(await BuildService(check).ValidateAsync(scheduleId));

                // Eski model (ScheduleEntry) tegilmagan — 1-bosqich additiv.
                Assert.True(await check.ScheduleEntries.CountAsync() > 0);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Haqiqiy_bazada_kalit_xaritasi_ikki_tomonlama_mos()
    {
        if (!SnapshotAvailable) return;

        var path = CopySnapshot();
        try
        {
            await using var context = OpenCopy(path);
            var scheduleId = await context.Schedules.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            var input = await new EfSchedulingStore(context).LoadAsync(scheduleId);
            var mapped = new SchedulingMapper().BuildProblem(input);
            var map = mapped.Map;

            // Har bir EF kaliti indeksga, har bir indeks o'sha kalitga qaytadi.
            foreach (var teacher in input.Teachers)
            {
                Assert.Equal(teacher.Id, map.Teachers.DbIdOf(map.Teachers.IndexOf(teacher.Id)));
            }

            foreach (var lesson in input.Lessons)
            {
                foreach (var index in map.Lessons.IndexesOf(lesson.Id))
                {
                    Assert.Equal(lesson.Id, map.Lessons.DbIdOf(index));
                }
            }

            foreach (var group in input.Groups)
            {
                Assert.Equal(group.Id, map.Groups.DbIdOf(map.Groups.IndexOf(group.Id)));
            }

            // Dars soatlari uzluksiz: PeriodNo qiymatlari bo'shliqsiz o'sadi.
            var periodNumbers = map.PeriodNoOfIndex;
            for (var i = 1; i < periodNumbers.Length; i++)
            {
                Assert.Equal(periodNumbers[i - 1] + 1, periodNumbers[i]);
            }

            Assert.Equal(map.Teachers.Count, mapped.Problem.Teachers.Length);
            Assert.Equal(map.Groups.Count, mapped.Problem.Groups.Length);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Haqiqiy_bazada_haddan_tashqari_yuklama_aniqlanadi()
    {
        if (!SnapshotAvailable) return;

        var path = CopySnapshot();
        try
        {
            await using var context = OpenCopy(path);
            var scheduleId = await context.Schedules.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();

            var report = await BuildService(context).GenerateAsync(new ScheduleGenerationOptions
            {
                ScheduleId = scheduleId,
                Seed = 1,
                Complexity = Complexity.Small,
                TimeLimit = TimeSpan.FromSeconds(10)
            });

            // Bu bazadagi eski biriktirmalar haftasiga 20 soatdan (ko'chirish izi),
            // ya'ni sinf sig'imidan oshadi — generator buni SABABI bilan aytadi.
            Assert.NotEmpty(report.VerificationFaults);
            Assert.True(report.UnplacedCards > 0);
            Assert.NotEmpty(report.RelaxationSuggestions);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
