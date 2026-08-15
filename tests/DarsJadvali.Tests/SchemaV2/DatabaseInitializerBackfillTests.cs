using DarsJadvali.Application.Abstractions;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Projection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// Dastur STARTIDAGI eski ma'lumot ko'chirish (<c>LegacyToV2Backfill</c>) —
/// <see cref="DatabaseInitializer"/> orqali. Uchta kafolat tekshiriladi:
/// <list type="number">
/// <item>zaxira nusxa ma'lumot o'zgarishidan OLDIN olinadi;</item>
/// <item>ko'chirish idempotent — takror startda dublikat yaratmaydi;</item>
/// <item>ko'chirishdagi xato dastur ishga tushishini TO'SMAYDI.</item>
/// </list>
/// </summary>
/// <remarks>
/// <c>SchemaV2/LegacyBackfillTests</c> ko'chirishning O'ZINI sinaydi; bu yerda esa
/// uning startga ULANISHI sinaladi — ilgari ikkalasi ham tekshirilmagan edi va
/// haqiqiy foydalanuvchi bazasida sxema yangilanib, jadval BO'SH ko'rinishi mumkin edi.
/// </remarks>
public class DatabaseInitializerBackfillTests
{
    private const string InitialCreateMigration = "20260813142230_InitialCreate";

    // ---------------------------------------------------------------------
    // Test dublyorlari
    // ---------------------------------------------------------------------

    /// <summary>Chaqiruv PAYTIDAGI baza holatini yozib oladigan zaxira servisi.</summary>
    private sealed class RecordingBackup : IDatabaseBackupService
    {
        private readonly SqliteConnection _connection;

        public RecordingBackup(SqliteConnection connection) => _connection = connection;

        public int Calls { get; private set; }

        /// <summary>Oxirgi chaqiruvdagi <c>onlyIfMigrationsPending</c> argumenti.</summary>
        public bool? OnlyIfMigrationsPending { get; private set; }

        /// <summary>Chaqiruv paytida <c>Cards</c> jadvali bor edimi (ya'ni migratsiya o'tganmi).</summary>
        public bool CardsTableExisted { get; private set; }

        /// <summary>Chaqiruv paytidagi kartochkalar soni (jadval bo'lmasa −1).</summary>
        public long CardCount { get; private set; } = -1;

        public Task<string?> CreateBackupAsync(
            bool onlyIfMigrationsPending = true, CancellationToken ct = default)
        {
            Calls++;
            OnlyIfMigrationsPending = onlyIfMigrationsPending;
            CardsTableExisted = TableExists(_connection, "Cards");
            CardCount = CardsTableExisted ? Scalar(_connection, "SELECT COUNT(*) FROM Cards") : -1;
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>Har doim yiqiladigan proyektor — ko'chirish xatosini modellashtiradi.</summary>
    private sealed class ThrowingProjector : Application.Abstractions.ICardOccurrenceProjector
    {
        public Task<int> RebuildForCardAsync(int cardId, CancellationToken ct = default)
            => throw new InvalidOperationException("Sun'iy xato: bandlik qurilmadi.");

        public Task<int> RebuildForCardsAsync(
            IReadOnlyList<int> cardIds, CancellationToken ct = default)
            => throw new InvalidOperationException("Sun'iy xato: bandlik qurilmadi.");

        public Task<int> RebuildForScheduleAsync(int scheduleId, CancellationToken ct = default)
            => throw new InvalidOperationException("Sun'iy xato: bandlik qurilmadi.");
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Eski (v1) bazada start: sxema yangilanadi VA eski dars yozuvlari kartochkaga
    /// ko'chadi. Zaxira nusxa esa migratsiyadan OLDIN olinadi — chaqiruv paytida
    /// <c>Cards</c> jadvali hali umuman mavjud emas.
    /// </summary>
    [Fact]
    public async Task Startda_eski_malumot_kochadi_va_zaxira_undan_oldin_olinadi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var old = CreateContext(connection))
        {
            await old.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
            SeedLegacyData(connection);
        }

        var backup = new RecordingBackup(connection);

        using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(
                context, backup, new CardOccurrenceProjector(context));

            await initializer.InitializeAsync();

            Assert.NotNull(initializer.LastBackfill);
            Assert.Equal(5, initializer.LastBackfill!.Cards);
            Assert.True(initializer.LastBackfill.CardOccurrences > 0);
        }

        // Zaxira AYNAN BIR MARTA va migratsiyadan OLDIN.
        Assert.Equal(1, backup.Calls);
        Assert.False(backup.CardsTableExisted,
            "Zaxira Cards jadvali yaratilgandan KEYIN olindi — ma'lumot o'zgarishidan oldin emas.");

        using (var context = CreateContext(connection))
        {
            Assert.Equal(5, await context.Cards.CountAsync());

            // Eski jadval JOYIDA qoladi — ko'chirish additiv.
            Assert.Equal(5, await context.ScheduleEntries.CountAsync());
        }
    }

    /// <summary>
    /// Sxema allaqachon joriy, lekin eski yozuvlar hali ko'chirilmagan holat:
    /// migratsiya kutilmasa ham zaxira MAJBURIY olinadi (chunki backfill hozir yozadi)
    /// va u kartochkalar paydo bo'lishidan OLDIN olinadi.
    /// </summary>
    [Fact]
    public async Task Kochirishdan_oldin_migratsiya_kutilmasa_ham_zaxira_olinadi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var old = CreateContext(connection))
        {
            await old.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
            SeedLegacyData(connection);
        }

        // 1-start: proyektorsiz — sxema yangilanadi, lekin ko'chirish O'TKAZIB YUBORILADI.
        using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
            Assert.Null(initializer.LastBackfill);
            Assert.Equal(0, await context.Cards.CountAsync());
        }

        // 2-start: endi migratsiya kutilmaydi, lekin ko'chirish kerak.
        var backup = new RecordingBackup(connection);

        using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(
                context, backup, new CardOccurrenceProjector(context));

            await initializer.InitializeAsync();
            Assert.Equal(5, initializer.LastBackfill!.Cards);
        }

        Assert.Equal(1, backup.Calls);

        // Migratsiya kutilmaydi — demak zaxira AYNAN backfill uchun so'ralgan.
        Assert.False(backup.OnlyIfMigrationsPending);

        // Va u kartochkalar yozilishidan OLDIN olingan.
        Assert.True(backup.CardsTableExisted);
        Assert.Equal(0, backup.CardCount);
    }

    /// <summary>
    /// Ikkinchi start dublikat yaratmaydi: ko'chirish idempotent, kartochkalar soni
    /// va Id'lari o'zgarmaydi.
    /// </summary>
    [Fact]
    public async Task Takror_startda_dublikat_kartochka_paydo_bolmaydi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var old = CreateContext(connection))
        {
            await old.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
            SeedLegacyData(connection);
        }

        List<int> firstIds;

        using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(
                context, backup: null, new CardOccurrenceProjector(context));

            await initializer.InitializeAsync();
            Assert.Equal(5, initializer.LastBackfill!.Cards);

            firstIds = await context.Cards.AsNoTracking()
                .Select(c => c.Id).OrderBy(id => id).ToListAsync();
        }

        long occurrencesAfterFirst;
        using (var context = CreateContext(connection))
        {
            occurrencesAfterFirst = await context.CardOccurrences.CountAsync();
        }

        using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(
                context, backup: null, new CardOccurrenceProjector(context));

            await initializer.InitializeAsync();

            // Ikkinchi startda BIRORTA yangi kartochka qo'shilmaydi.
            Assert.Equal(0, initializer.LastBackfill!.Cards);
        }

        using (var context = CreateContext(connection))
        {
            Assert.Equal(firstIds, await context.Cards.AsNoTracking()
                .Select(c => c.Id).OrderBy(id => id).ToListAsync());
            Assert.Equal(occurrencesAfterFirst, await context.CardOccurrences.CountAsync());
            Assert.Equal(1, await context.Schedules.CountAsync());
            Assert.Equal(1, await context.AcademicYears.CountAsync());
        }
    }

    /// <summary>
    /// Ko'chirishdagi xato dasturni ishga tushirmay qo'ymaydi: migratsiya qo'llangan,
    /// eski ma'lumot joyida, faqat ko'chirish natijasi yo'q.
    /// </summary>
    [Fact]
    public async Task Kochirishdagi_xato_dastur_startini_tosmaydi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var old = CreateContext(connection))
        {
            await old.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
            SeedLegacyData(connection);
        }

        using (var context = CreateContext(connection))
        {
            var initializer = new DatabaseInitializer(context, backup: null, new ThrowingProjector());

            // Istisno CHIQMAYDI.
            await initializer.InitializeAsync();
            Assert.Null(initializer.LastBackfill);
        }

        using (var context = CreateContext(connection))
        {
            // Sxema baribir yangilangan va foydalanuvchi ma'lumoti joyida.
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.Equal(5, await context.ScheduleEntries.CountAsync());
            Assert.Equal(1, await context.Schedules.CountAsync());
        }
    }

    /// <summary>
    /// Bekor qilish (<c>CancellationToken</c>) xato emas — u chaqiruvchiga qaytadi
    /// va yutilmaydi.
    /// </summary>
    [Fact]
    public async Task Bekor_qilish_yutilmaydi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = CreateContext(connection);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var initializer = new DatabaseInitializer(context);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => initializer.InitializeAsync(cts.Token));
    }

    // -------------------------------------------------------------------------
    // Yordamchilar
    // -------------------------------------------------------------------------

    private static AppDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    /// <summary>Eski sxemaga (ScheduleId siz) to'g'ridan-to'g'ri SQL bilan ma'lumot yozadi.</summary>
    private static void SeedLegacyData(SqliteConnection connection)
    {
        Execute(connection, """
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
            """);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static bool TableExists(SqliteConnection connection, string table)
        => Scalar(connection,
            $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}';") > 0;
}
