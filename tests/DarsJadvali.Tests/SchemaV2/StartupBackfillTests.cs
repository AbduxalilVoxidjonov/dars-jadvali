using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Projection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// <b>Dastur STARTIDA</b> eski (v1) ma'lumot v2 modeliga ko'chishini tekshiradi.
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> Ilgari <c>LegacyToV2Backfill</c> faqat testlarda
/// chaqirilardi. Natijada haqiqiy foydalanuvchi bazasida migratsiyalar qo'llanar,
/// lekin eski darslar <c>Card</c> ga KO'CHMASDI va yangi jadval BO'SH ko'rinardi.
/// Shu yerdagi testlar ko'chirish <see cref="DatabaseInitializer.InitializeAsync"/>
/// ga ulanganini qat'iy qayd etadi.
/// </remarks>
public class StartupBackfillTests
{
    private const int Classes = 3;
    private const int LessonsPerClass = 4;

    /// <summary>
    /// Dastur startida eski dars yozuvlari kartochkaga ko'chadi — backfill'ni
    /// QO'LDA chaqirmasdan, faqat initsializator orqali.
    /// </summary>
    [Fact]
    public async Task Dastur_startida_eski_darslar_kartochkaga_kochadi()
    {
        // Arrange — v1 darajasidagi baza (v2 jadvallari hali YO'Q).
        using var connection = NewIsolatedConnection();
        var entries = SeedLegacyWorld(connection);

        await using (var check = CreateContext(connection))
        {
            Assert.Equal(entries, await check.ScheduleEntries.CountAsync());

            // Boshlanishda jadval BO'SH — aynan foydalanuvchi ko'rgan holat.
            Assert.Equal(0, await check.Cards.CountAsync());
        }

        // Act — dastur starti.
        LegacyBackfillNumbers first;
        await using (var context = CreateContext(connection))
        {
            var initializer = NewInitializer(context);
            await initializer.InitializeAsync();

            Assert.NotNull(initializer.LastBackfill);
            Assert.Equal(entries, initializer.LastBackfill!.Cards);
        }

        // Assert
        await using (var check = CreateContext(connection))
        {
            first = await ReadAsync(check);

            // Har bir eski dars yozuvi uchun AYNAN bitta kartochka.
            Assert.Equal(entries, first.Cards);
            Assert.True(first.Occurrences > 0, "Bandlik qatorlari qurilmadi.");
            Assert.Equal(Classes, first.SchoolClasses);

            // Eski model BUZILMAGAN — 1-bosqich additiv.
            Assert.Equal(entries, first.Entries);
            Assert.Equal(Classes, await check.ClassGroups.CountAsync());

            Assert.Equal(0, Scalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
        }

        // Act — IKKINCHI start (dastur qayta ochildi).
        await using (var context = CreateContext(connection))
        {
            await NewInitializer(context).InitializeAsync();
        }

        // Assert — +0: idempotent.
        await using (var check = CreateContext(connection))
        {
            var second = await ReadAsync(check);
            Assert.Equal(first.Cards, second.Cards);
            Assert.Equal(first.Occurrences, second.Occurrences);
            Assert.Equal(first.Lessons, second.Lessons);
            Assert.Equal(first.SchoolClasses, second.SchoolClasses);
            Assert.Equal(first.StudentGroups, second.StudentGroups);
        }
    }

    /// <summary>
    /// Bandlik proyektori berilmasa ko'chirish o'tkazib yuboriladi, lekin migratsiya
    /// va seed baribir bajariladi (dastur ishga tushadi).
    /// </summary>
    [Fact]
    public async Task Proyektorsiz_migratsiya_bajariladi_kochirish_otkazib_yuboriladi()
    {
        using var connection = NewIsolatedConnection();
        SeedLegacyWorld(connection);

        await using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
            Assert.Null(initializer.LastBackfill);
        }

        await using (var check = CreateContext(connection))
        {
            Assert.True(TableExists(connection, "Cards"), "Migratsiya qo'llanmadi.");

            // Ko'chirish bajarilmadi — jadval hamon bo'sh.
            Assert.Equal(0, await check.Cards.CountAsync());
        }
    }

    /// <summary>
    /// Ko'chirishdagi xato dastur startini TO'SMAYDI: migratsiya va seed joyida
    /// qoladi, eski ma'lumot ham yo'qolmaydi.
    /// </summary>
    [Fact]
    public async Task Kochirish_xatosi_dastur_startini_tosmaydi()
    {
        // Arrange
        using var connection = NewIsolatedConnection();
        var entries = SeedLegacyWorld(connection);

        // Act — proyektor har doim xato tashlaydi.
        await using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(context, backup: null, new ThrowingProjector());

            // Assert — istisno CHIQMAYDI.
            await initializer.InitializeAsync();
        }

        // Assert — dastur ishlashi uchun kerak bo'lgani bajarildi.
        await using (var check = CreateContext(connection))
        {
            Assert.True(TableExists(connection, "Cards"));

            // Eski ma'lumot butun: ko'chirish yiqilsa ham hech narsa yo'qolmaydi.
            Assert.Equal(entries, await check.ScheduleEntries.CountAsync());
            Assert.NotEmpty(await check.WorkDays.ToListAsync());
        }
    }

    /// <summary>
    /// Ko'chirish oldidan zaxira nusxa MAJBURIY olinadi — sxema allaqachon joriy
    /// bo'lsa ham (migratsiya kutilmayotgan bo'lsa ham).
    /// </summary>
    /// <remarks>
    /// Eski shart faqat "migratsiya kutilyaptimi" edi. Ko'chirish ham ma'lumotni
    /// o'zgartiradi, shuning uchun u ham zaxira sababi bo'lishi kerak.
    /// </remarks>
    [Fact]
    public async Task Kochirish_oldidan_zaxira_nusxa_olinadi()
    {
        // Arrange — FAYL asosidagi baza: VACUUM INTO ga haqiqiy fayl kerak.
        var folder = Path.Combine(Path.GetTempPath(), $"dj-backfill-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            var dbPath = Path.Combine(folder, "darsjadvali.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                ForeignKeys = true,
            }.ToString();

            // 1) Sxemani TO'LIQ joriy holatga keltiramiz, lekin ko'chirishni bajarmaymiz.
            await using (var context = CreateContext(connectionString))
            {
                await context.Database.MigrateAsync();
                SeedLegacyRows(context);
            }

            var backupFolder = Path.Combine(folder, DatabaseBackupService.FolderName);
            Assert.False(Directory.Exists(backupFolder), "Zaxira erta olingan.");

            // 2) Start: migratsiya KUTILMAYAPTI, lekin ko'chirilmagan eski ma'lumot bor.
            await using (var context = CreateContext(connectionString))
            {
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());

                var initializer = new DatabaseInitializer(
                    context, new DatabaseBackupService(context), new CardOccurrenceProjector(context));

                await initializer.InitializeAsync();
                Assert.Equal(Classes * LessonsPerClass, initializer.LastBackfill!.Cards);
            }

            // Assert — zaxira aynan ko'chirishdan OLDIN olingan.
            Assert.True(Directory.Exists(backupFolder), "Ko'chirish oldidan zaxira olinmadi.");
            var backups = Directory.GetFiles(backupFolder, "*.db");
            Assert.Single(backups);

            // Zaxirada kartochkalar HALI yo'q — demak u ko'chirishdan oldin olingan.
            await using (var backupContext = CreateContext(
                new SqliteConnectionStringBuilder { DataSource = backups[0] }.ToString()))
            {
                Assert.Equal(0, await backupContext.Cards.CountAsync());
                Assert.Equal(Classes * LessonsPerClass, await backupContext.ScheduleEntries.CountAsync());
            }

            // 3) Ikkinchi start: ko'chiradigan narsa yo'q — YANGI zaxira ham olinmaydi.
            await using (var context = CreateContext(connectionString))
            {
                await new DatabaseInitializer(
                    context, new DatabaseBackupService(context), new CardOccurrenceProjector(context))
                    .InitializeAsync();
            }

            Assert.Single(Directory.GetFiles(backupFolder, "*.db"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(folder);
        }
    }

    // =====================================================================
    // Yordamchilar
    // =====================================================================

    private sealed record LegacyBackfillNumbers(
        int Entries, int Cards, int Occurrences, int Lessons, int SchoolClasses, int StudentGroups);

    private static async Task<LegacyBackfillNumbers> ReadAsync(AppDbContext context) => new(
        await context.ScheduleEntries.CountAsync(),
        await context.Cards.CountAsync(),
        await context.CardOccurrences.CountAsync(),
        await context.Lessons.CountAsync(),
        await context.SchoolClasses.CountAsync(),
        await context.StudentGroups.CountAsync());

    private static DatabaseInitializer NewInitializer(AppDbContext context)
        => new(context, backup: null, new CardOccurrenceProjector(context));

    private static AppDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static AppDbContext CreateContext(string connectionString)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);

    /// <summary>Har test uchun to'liq alohida xotiradagi baza (hovuzlanmaydi).</summary>
    private static SqliteConnection NewIsolatedConnection()
    {
        var connection = new SqliteConnection(
            $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Foydalanuvchining HAQIQIY holatini modellaydi: sxema yangilangan, lekin eski
    /// dars yozuvlari hali kartochkaga ko'chirilmagan (v2 jadvallari BO'SH).
    /// </summary>
    /// <remarks>
    /// Aynan shu holat xatoning o'zi edi — migratsiyalar qo'llanardi, backfill esa
    /// hech qachon chaqirilmasdi va yangi jadval bo'sh ko'rinardi.
    /// </remarks>
    private static int SeedLegacyWorld(SqliteConnection connection)
    {
        using var context = CreateContext(connection);
        context.Database.Migrate();
        return SeedLegacyRows(context);
    }

    /// <summary>
    /// Eski (v1) ma'lumot: <see cref="Classes"/> sinf, har birida
    /// <see cref="LessonsPerClass"/> ta dars yozuvi.
    /// </summary>
    private static int SeedLegacyRows(AppDbContext context)
    {
        var year = context.AcademicYears.OrderBy(y => y.Id).FirstOrDefault();
        if (year is null)
        {
            year = new AcademicYear { Name = "2025–2026", StartYear = 2025 };
            context.AcademicYears.Add(year);
            context.SaveChanges();
        }

        var schedule = context.Schedules.OrderBy(s => s.Id).FirstOrDefault();
        if (schedule is null)
        {
            schedule = new Schedule
            {
                AcademicYearId = year.Id,
                Name = "Asosiy jadval",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            context.Schedules.Add(schedule);
            context.SaveChanges();
        }

        if (context.ScheduleEntries.Any()) return context.ScheduleEntries.Count();

        for (var i = 1; i <= 7; i++)
        {
            if (context.LessonSlots.Any(s => s.LessonNumber == i)) continue;

            var start = new TimeSpan(8, 30, 0) + TimeSpan.FromMinutes((i - 1) * 55);
            context.LessonSlots.Add(new LessonSlot
            {
                LessonNumber = i,
                StartTime = start,
                EndTime = start + TimeSpan.FromMinutes(45),
            });
        }

        var subject = new Subject { Name = "Matematika", Code = "MAT" };
        context.Subjects.Add(subject);
        context.SaveChanges();

        var days = new[] { WeekDay.Dushanba, WeekDay.Seshanba, WeekDay.Chorshanba, WeekDay.Payshanba };
        var entries = 0;

        for (var c = 0; c < Classes; c++)
        {
            // Har sinfga O'Z o'qituvchisi — eski unikal indekslar buzilmaydi.
            var teacher = new Teacher { FullName = $"O'qituvchi {c + 1}" };
            context.Teachers.Add(teacher);

            var classGroup = new ClassGroup
            {
                Name = $"{c + 5}-A",
                RoomNumber = $"{c + 1}01",
                StudentCount = 28,
            };
            context.ClassGroups.Add(classGroup);
            context.SaveChanges();

            context.TeacherAssignments.Add(new TeacherAssignment
            {
                TeacherId = teacher.Id,
                SubjectId = subject.Id,
                ClassGroupId = classGroup.Id,
                WeeklyHoursCount = LessonsPerClass,
            });

            for (var l = 0; l < LessonsPerClass; l++)
            {
                context.ScheduleEntries.Add(new ScheduleEntry
                {
                    ScheduleId = schedule.Id,
                    ClassGroupId = classGroup.Id,
                    SubjectId = subject.Id,
                    TeacherId = teacher.Id,
                    DayOfWeek = days[l % days.Length],
                    LessonNumber = (l / days.Length) + 1,
                    RoomNumber = classGroup.RoomNumber,
                });

                entries++;
            }

            context.SaveChanges();
        }

        return entries;
    }

    private static bool TableExists(SqliteConnection connection, string table)
        => Scalar(connection, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'") > 0;

    private static int Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // Vaqtinchalik papkani tozalay olmaslik testni yiqitmaydi.
        }
    }

    /// <summary>Har doim xato tashlaydigan proyektor — ko'chirish nosozligini modellaydi.</summary>
    private sealed class ThrowingProjector : Application.Abstractions.ICardOccurrenceProjector
    {
        public Task<int> RebuildForCardAsync(int cardId, CancellationToken ct = default)
            => throw new InvalidOperationException("Sinov uchun ataylab yiqitilgan.");

        public Task<int> RebuildForCardsAsync(IReadOnlyList<int> cardIds, CancellationToken ct = default)
            => throw new InvalidOperationException("Sinov uchun ataylab yiqitilgan.");

        public Task<int> RebuildForScheduleAsync(int scheduleId, CancellationToken ct = default)
            => throw new InvalidOperationException("Sinov uchun ataylab yiqitilgan.");
    }
}
