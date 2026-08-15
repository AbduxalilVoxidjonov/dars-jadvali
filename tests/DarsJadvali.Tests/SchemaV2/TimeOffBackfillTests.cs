using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using DarsJadvali.Infrastructure.Persistence.Projection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// <c>V2_06</c> — eski 2 holatli <c>TeacherAvailability</c> oraliqlaridan yangi
/// 3 holatli <c>TimeOff</c> katakchalariga ko'chirish.
/// </summary>
/// <remarks>
/// Eski model faqat "ishlayman / ishlamayman" ni bilardi, shuning uchun ko'chirishdan
/// faqat <c>Forbidden</c> chiqadi. "?" (jarimali) holat — YANGI imkoniyat va faqat
/// yangi ma'lumotdan paydo bo'ladi; uning yadroga o'girilishi
/// <c>TimeOffOwnerAndPenaltyTests</c> da tekshiriladi.
/// </remarks>
public class TimeOffBackfillTests
{
    /// <summary>Oq ro'yxat: o'qituvchi faqat 08:30–11:05 ishlaydi → 4..7-soatlar taqiqlanadi.</summary>
    [Fact]
    public async Task Oq_royxat_qamramagan_soatlar_taqiqlanadi()
    {
        using var connection = NewIsolatedConnection();
        int teacherId;

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            teacherId = SeedWorld(seed);

            // Dushanba: 08:30–11:05 (1..3-soatlar).
            seed.TeacherAvailabilities.Add(new TeacherAvailability
            {
                TeacherId = teacherId,
                DayOfWeek = WeekDay.Dushanba,
                StartTime = new TimeSpan(8, 30, 0),
                EndTime = new TimeSpan(11, 5, 0),
                IsAvailable = true,
            });
            seed.SaveChanges();
        }

        var result = await RunAsync(connection);

        await using var check = CreateContext(connection);
        var cells = await check.TimeOffs.AsNoTracking().OrderBy(t => t.PeriodNo).ToListAsync();

        Assert.Equal(4, cells.Count);
        Assert.Equal(4, result.TimeOffs);
        Assert.Equal(new[] { 4, 5, 6, 7 }, cells.Select(c => c.PeriodNo).ToArray());

        Assert.All(cells, c =>
        {
            Assert.Equal(ResourceOwnerKind.Teacher, c.OwnerKind);
            Assert.Equal(teacherId, c.OwnerId);
            // Dushanba → 0-kun.
            Assert.Equal(0, c.DayNo);
            // Eski modelda hafta o'lchovi yo'q edi: 0 = barcha haftalar.
            Assert.Equal(0, c.WeeksMask);
            Assert.Equal(AvailabilityLevel.Forbidden, c.Availability);
            Assert.Equal(0, c.Penalty);
            Assert.NotNull(c.LegacyTeacherAvailabilityId);
        });
    }

    /// <summary>Qora ro'yxat ("band" oralig'i) kesishgan soatlarni taqiqlaydi.</summary>
    [Fact]
    public async Task Qora_royxat_kesishgan_soatlarni_taqiqlaydi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var teacherId = SeedWorld(seed);

            // Seshanba 09:00–10:30 band → 1, 2, 3-soatlar bilan kesishadi.
            seed.TeacherAvailabilities.Add(new TeacherAvailability
            {
                TeacherId = teacherId,
                DayOfWeek = WeekDay.Seshanba,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 30, 0),
                IsAvailable = false,
            });
            seed.SaveChanges();
        }

        await RunAsync(connection);

        await using var check = CreateContext(connection);
        var cells = await check.TimeOffs.AsNoTracking().OrderBy(t => t.PeriodNo).ToListAsync();

        // 1-dars 08:30–09:15, 2-dars 09:25–10:10, 3-dars 10:20–11:05.
        Assert.Equal(new[] { 1, 2, 3 }, cells.Select(c => c.PeriodNo).ToArray());
        Assert.All(cells, c => Assert.Equal(1, c.DayNo));
    }

    /// <summary>Cheklovi yo'q kun butunlay ochiq qoladi — ortiqcha qator yozilmaydi.</summary>
    [Fact]
    public async Task Cheklovsiz_kun_uchun_qator_yozilmaydi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var teacherId = SeedWorld(seed);

            seed.TeacherAvailabilities.Add(new TeacherAvailability
            {
                TeacherId = teacherId,
                DayOfWeek = WeekDay.Dushanba,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(15, 0, 0),
                IsAvailable = true,
            });
            seed.SaveChanges();
        }

        await RunAsync(connection);

        await using var check = CreateContext(connection);
        Assert.Equal(0, await check.TimeOffs.CountAsync());
    }

    /// <summary>Takror ishga tushirish dublikat katakcha yaratmaydi (idempotentlik).</summary>
    [Fact]
    public async Task Kochirish_takror_ishga_tushirilsa_dublikat_yaratmaydi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var teacherId = SeedWorld(seed);

            foreach (var day in new[] { WeekDay.Dushanba, WeekDay.Seshanba, WeekDay.Chorshanba })
            {
                seed.TeacherAvailabilities.Add(new TeacherAvailability
                {
                    TeacherId = teacherId,
                    DayOfWeek = day,
                    StartTime = new TimeSpan(8, 30, 0),
                    EndTime = new TimeSpan(11, 5, 0),
                    IsAvailable = true,
                });
            }

            seed.SaveChanges();
        }

        var first = await RunAsync(connection);

        int afterFirst;
        await using (var check = CreateContext(connection))
        {
            afterFirst = await check.TimeOffs.CountAsync();
        }

        var second = await RunAsync(connection);

        await using (var check = CreateContext(connection))
        {
            Assert.Equal(afterFirst, await check.TimeOffs.CountAsync());
        }

        // 3 kun × 4 taqiqlangan soat.
        Assert.Equal(12, first.TimeOffs);
        Assert.Equal(12, afterFirst);
        Assert.Equal(0, second.TimeOffs);
    }

    /// <summary>Foydalanuvchi qo'lda tahrirlagan katakcha ko'chirish tomonidan buzilmaydi.</summary>
    [Fact]
    public async Task Qolda_tahrirlangan_katakcha_ozgartirilmaydi()
    {
        using var connection = NewIsolatedConnection();
        int teacherId;
        int yearId;

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            teacherId = SeedWorld(seed);
            yearId = seed.AcademicYears.OrderBy(y => y.Id).First().Id;

            seed.TeacherAvailabilities.Add(new TeacherAvailability
            {
                TeacherId = teacherId,
                DayOfWeek = WeekDay.Dushanba,
                StartTime = new TimeSpan(8, 30, 0),
                EndTime = new TimeSpan(11, 5, 0),
                IsAvailable = true,
            });

            // Aynan o'sha katakcha, lekin "tavsiya etilmaydi" (yumshoq) sifatida.
            seed.TimeOffs.Add(new TimeOff
            {
                AcademicYearId = yearId,
                OwnerKind = ResourceOwnerKind.Teacher,
                OwnerId = teacherId,
                DayNo = 0,
                PeriodNo = 4,
                WeeksMask = 0,
                Availability = AvailabilityLevel.NotRecommended,
                Penalty = 300,
            });

            seed.SaveChanges();
        }

        await RunAsync(connection);

        await using var check = CreateContext(connection);
        var manual = await check.TimeOffs.AsNoTracking()
            .SingleAsync(t => t.DayNo == 0 && t.PeriodNo == 4);

        Assert.Equal(AvailabilityLevel.NotRecommended, manual.Availability);
        Assert.Equal(300, manual.Penalty);
        Assert.Null(manual.LegacyTeacherAvailabilityId);

        // Qolgan uchtasi baribir yaratildi.
        Assert.Equal(4, await check.TimeOffs.CountAsync());
    }

    // =====================================================================

    private static async Task<LegacyBackfillResult> RunAsync(SqliteConnection connection)
    {
        await using var context = CreateContext(connection);
        var backfill = new LegacyToV2Backfill(context, new CardOccurrenceProjector(context));
        return await backfill.RunAsync();
    }

    /// <summary>O'quv yili, jadval, 7 ta dars soati va bitta o'qituvchi.</summary>
    private static int SeedWorld(AppDbContext context)
    {
        var year = context.AcademicYears.OrderBy(y => y.Id).First();

        if (!context.Schedules.Any())
        {
            context.Schedules.Add(new Schedule
            {
                AcademicYearId = year.Id,
                Name = "Asosiy jadval",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        // 1: 08:30–09:15, 2: 09:25–10:10, 3: 10:20–11:05, 4: 11:15–12:00, ...
        for (var i = 1; i <= 7; i++)
        {
            var start = new TimeSpan(8, 30, 0) + TimeSpan.FromMinutes((i - 1) * 55);
            context.LessonSlots.Add(new LessonSlot
            {
                LessonNumber = i,
                StartTime = start,
                EndTime = start + TimeSpan.FromMinutes(45),
            });
        }

        var teacher = new Teacher { FullName = "Aliyev Vali Anvarovich" };
        context.Teachers.Add(teacher);
        context.SaveChanges();
        return teacher.Id;
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    /// <summary>Har test uchun to'liq alohida xotiradagi baza (pooling tuzog'isiz).</summary>
    private static SqliteConnection NewIsolatedConnection()
    {
        var connection = new SqliteConnection(
            $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }
}
