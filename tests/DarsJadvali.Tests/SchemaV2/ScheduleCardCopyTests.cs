using DarsJadvali.Application.Services;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Projection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// Jadval variantini nusxalash <b>kartochkalarni ham</b> ko'chiradi.
/// </summary>
/// <remarks>
/// <b>Yopilayotgan kamchilik.</b> <c>ScheduleSetService.DuplicateAsync</c> faqat eski
/// <c>ScheduleEntry</c> qatorlarini nusxalardi. Ko'chirish (backfill) bajarilgan haqiqiy
/// bazada bu jimgina yo'qotish edi: nusxada eski yozuvlar bor, lekin <c>/api/board</c>
/// va Desktop taxtasi o'qiydigan <c>Card</c> qatorlari YO'Q — variant bo'sh ko'rinardi.
/// </remarks>
public class ScheduleCardCopyTests
{
    private const string InitialCreateMigration = "20260813142230_InitialCreate";

    /// <summary>Nusxada eski yozuvlar ham, kartochkalar ham bo'ladi.</summary>
    [Fact]
    public async Task Nusxalashda_kartochkalar_ham_kochadi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        await PrepareBackfilledDatabaseAsync(connection);

        int sourceId;
        int copyId;

        using (var context = CreateContext(connection))
        {
            var uow = new UnitOfWork(context);
            var copier = new ScheduleCardCopier(context, new CardOccurrenceProjector(context));
            var sets = new ScheduleSetService(uow, copier);

            sourceId = (await sets.GetActiveAsync()).Id;
            var copy = await sets.DuplicateAsync(sourceId, "2-variant");
            copyId = copy.Id;
        }

        using (var context = CreateContext(connection))
        {
            // Eski model — avvalgidek.
            Assert.Equal(5, await context.ScheduleEntries.CountAsync(e => e.ScheduleId == sourceId));
            Assert.Equal(5, await context.ScheduleEntries.CountAsync(e => e.ScheduleId == copyId));

            // YANGI model — endi nusxada ham bor.
            Assert.Equal(5, await context.Cards.CountAsync(c => c.ScheduleId == sourceId));
            Assert.Equal(5, await context.Cards.CountAsync(c => c.ScheduleId == copyId));

            // Bandlik proyeksiyasi ham qayta qurilgan.
            Assert.True(await context.CardOccurrences.CountAsync(o => o.ScheduleId == copyId) > 0);

            // Manba jadvalga TEGILMAGAN.
            Assert.Equal(
                await context.CardOccurrences.CountAsync(o => o.ScheduleId == sourceId),
                await context.CardOccurrences.CountAsync(o => o.ScheduleId == copyId));
        }
    }

    /// <summary>
    /// Ko'chirish izi (<c>LegacyScheduleEntryId</c>) nusxaga o'tmaydi — aks holda
    /// <c>UX_Cards_LegacyScheduleEntryId</c> filtrlangan unikal indeksi buzilardi.
    /// </summary>
    [Fact]
    public async Task Nusxadagi_kartochkalarda_kochirish_izi_qolmaydi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        await PrepareBackfilledDatabaseAsync(connection);

        int copyId;
        using (var context = CreateContext(connection))
        {
            var sets = new ScheduleSetService(
                new UnitOfWork(context),
                new ScheduleCardCopier(context, new CardOccurrenceProjector(context)));

            copyId = (await sets.DuplicateAsync((await sets.GetActiveAsync()).Id)).Id;
        }

        using (var context = CreateContext(connection))
        {
            var copied = await context.Cards.AsNoTracking()
                .Where(c => c.ScheduleId == copyId)
                .ToListAsync();

            Assert.Equal(5, copied.Count);
            Assert.All(copied, c => Assert.Null(c.LegacyScheduleEntryId));

            // Xona matni esa saqlanadi.
            Assert.All(copied, c => Assert.False(string.IsNullOrWhiteSpace(c.LegacyRoomNumber)));
        }
    }

    /// <summary>Takror chaqiruv dublikat yaratmaydi.</summary>
    [Fact]
    public async Task Takror_nusxalash_dublikat_yaratmaydi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        await PrepareBackfilledDatabaseAsync(connection);

        using var context = CreateContext(connection);
        var uow = new UnitOfWork(context);
        var copier = new ScheduleCardCopier(context, new CardOccurrenceProjector(context));
        var sets = new ScheduleSetService(uow, copier);

        var sourceId = (await sets.GetActiveAsync()).Id;
        var copyId = (await sets.DuplicateAsync(sourceId, "2-variant")).Id;

        Assert.Equal(0, await copier.CopyCardsAsync(sourceId, copyId));
        Assert.Equal(5, await context.Cards.CountAsync(c => c.ScheduleId == copyId));
    }

    /// <summary>
    /// Nusxalovchi berilmasa (Infrastructure ro'yxatdan o'tmagan holat) eski
    /// xatti-harakat saqlanadi — mavjud chaqiruvlar buzilmaydi.
    /// </summary>
    [Fact]
    public async Task Nusxalovchisiz_eski_xatti_harakat_saqlanadi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        await PrepareBackfilledDatabaseAsync(connection);

        using var context = CreateContext(connection);
        var sets = new ScheduleSetService(new UnitOfWork(context));

        var copy = await sets.DuplicateAsync((await sets.GetActiveAsync()).Id, "2-variant");

        Assert.Equal(5, await context.ScheduleEntries.CountAsync(e => e.ScheduleId == copy.Id));
        Assert.Equal(0, await context.Cards.CountAsync(c => c.ScheduleId == copy.Id));
    }

    // ---------------------------------------------------------------------

    /// <summary>Eski ma'lumot yozilgan va to'liq ko'chirilgan baza tayyorlaydi.</summary>
    private static async Task PrepareBackfilledDatabaseAsync(SqliteConnection connection)
    {
        using (var old = CreateContext(connection))
        {
            await old.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
            SeedLegacyData(connection);
        }

        using var context = CreateContext(connection);
        var initializer = new DatabaseInitializer(
            context, backup: null, new CardOccurrenceProjector(context));

        await initializer.InitializeAsync();
        Assert.Equal(5, initializer.LastBackfill!.Cards);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static void SeedLegacyData(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Teachers (FullName, IsActive, ColorCode, Phone)
                VALUES ('Aliyev Vali', 1, '#1976D2', NULL), ('Karimova Nodira', 1, '#388E3C', NULL);
            INSERT INTO Subjects (Name, Code, ColorCode)
                VALUES ('Matematika', 'MAT', '#455A64'), ('Fizika', 'FIZ', '#8E24AA');
            INSERT INTO ClassGroups (Name, RoomNumber, StudentCount)
                VALUES ('5-A', '101', 25), ('5-B', '102', 27);
            INSERT INTO TeacherAssignments (TeacherId, SubjectId, ClassGroupId, WeeklyHoursCount)
                VALUES (1, 1, 1, 5), (2, 2, 2, 4);
            INSERT INTO WorkDays (DayOfWeek, IsActive, MaxLessonsPerDay)
                VALUES (1,1,7),(2,1,7),(3,1,7),(4,1,7),(5,1,7),(6,1,7),(7,0,7);
            INSERT INTO ScheduleEntries (ClassGroupId, SubjectId, TeacherId, DayOfWeek, LessonNumber, RoomNumber)
                VALUES (1,1,1,1,1,'101'),
                       (1,1,1,2,1,'101'),
                       (1,1,1,3,2,'101'),
                       (2,2,2,1,2,'102'),
                       (2,2,2,4,1,'102');
            """;
        command.ExecuteNonQuery();
    }
}
