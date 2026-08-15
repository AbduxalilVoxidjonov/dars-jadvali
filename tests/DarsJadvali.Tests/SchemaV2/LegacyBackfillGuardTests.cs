using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using DarsJadvali.Infrastructure.Persistence.Projection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// Eski <c>ScheduleEntry</c> jadvalini tashlaydigan migratsiya (<c>V2_08_DropLegacyEntry</c>)
/// uchun QO'RIQCHI.
/// </summary>
/// <remarks>
/// <b>Yopilayotgan xavf.</b> <see cref="DatabaseInitializer"/> ilgari barcha
/// migratsiyalarni ko'chirishdan (backfill) OLDIN qo'llardi. Shu tartibda
/// <c>V2_04</c> gacha yangilanmagan foydalanuvchi bazasida eski jadval ko'chirilishidan
/// oldin tashlanardi va butun dars jadvali <b>jimgina</b> yo'qolardi.
/// <para>
/// Bu yerda ikki kafolat sinaladi:
/// <list type="number">
/// <item><b>tartib</b> — buzuvchi migratsiya oxirgi bosqichga suriladi
/// (<see cref="LegacyBackfillGuard.Split"/>);</item>
/// <item><b>rad etish</b> — ko'chirilmagan qator qolgan bo'lsa migratsiya
/// <see cref="LegacyBackfillIncompleteException"/> bilan TO'XTAYDI, jimgina o'tib ketmaydi.</item>
/// </list>
/// </para>
/// </remarks>
public class LegacyBackfillGuardTests
{
    private const string InitialCreateMigration = "20260813142230_InitialCreate";
    private const string DropLegacy = "20260815090000_V2_08_DropLegacyEntry";

    // ---------------------------------------------------------------------
    // 1. Tartib: buzuvchi migratsiya ikkinchi bosqichga suriladi
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("20260815090000_V2_08_DropLegacyEntry", true)]
    [InlineData("V2_08_DropLegacyEntry", true)]
    [InlineData("20260814161600_V2_07_ClassroomsFromLegacyRoom", false)]
    [InlineData("20260814142701_V2_04_LessonAndCard", false)]
    [InlineData("20260813142230_InitialCreate", false)]
    public void Buzuvchi_migratsiya_tanib_olinadi(string migrationId, bool expected)
        => Assert.Equal(expected, LegacyBackfillGuard.IsDestructive(migrationId));

    /// <summary>Buzuvchisi yo'q bo'lsa hammasi birinchi bosqichda qo'llanadi.</summary>
    [Fact]
    public void Buzuvchi_bolmasa_hamma_migratsiya_birinchi_bosqichda()
    {
        var pending = new[]
        {
            "20260814161551_V2_06_TimeOffFromAvailability",
            "20260814161600_V2_07_ClassroomsFromLegacyRoom",
        };

        var (safe, destructive) = LegacyBackfillGuard.Split(pending);

        Assert.Equal(pending, safe);
        Assert.Empty(destructive);
    }

    /// <summary>
    /// Buzuvchi migratsiya VA undan keyingilarning hammasi ikkinchi bosqichga tushadi —
    /// keyingilari uning natijasi ustiga quriladi, undan oldin qo'llab bo'lmaydi.
    /// </summary>
    [Fact]
    public void Buzuvchi_va_undan_keyingilar_ikkinchi_bosqichga_suriladi()
    {
        var pending = new[]
        {
            "20260814161600_V2_07_ClassroomsFromLegacyRoom",
            DropLegacy,
            "20260816090000_V2_09_Keyingi",
        };

        var (safe, destructive) = LegacyBackfillGuard.Split(pending);

        Assert.Equal(new[] { "20260814161600_V2_07_ClassroomsFromLegacyRoom" }, safe);
        Assert.Equal(new[] { DropLegacy, "20260816090000_V2_09_Keyingi" }, destructive);
    }

    // ---------------------------------------------------------------------
    // 2. Rad etish: ko'chirilmagan qator bor bazada V2_08 to'xtaydi
    // ---------------------------------------------------------------------

    /// <summary>
    /// <b>Asosiy stsenariy.</b> Eski (v1) baza, ko'chirish BAJARILMAGAN (proyektor yo'q —
    /// aynan shu holat haqiqiy foydalanuvchida ham bo'lishi mumkin). Bunday bazada
    /// eski jadvalni tashlash 5 ta darsni yo'qotgan bo'lardi — qo'riqchi RAD ETADI.
    /// </summary>
    [Fact]
    public async Task Kochirilmagan_qator_bor_bazada_V2_08_rad_etiladi()
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
            // Proyektorsiz start: sxema yangilanadi, ko'chirish O'TKAZIB YUBORILADI.
            var initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
            Assert.Null(initializer.LastBackfill);
        }

        using (var context = CreateContext(connection))
        {
            Assert.Equal(5, await context.ScheduleEntries.CountAsync());
            Assert.Equal(0, await context.Cards.CountAsync());

            Assert.Equal(5, await LegacyBackfillGuard.CountUnmigratedAsync(context));

            var ex = await Assert.ThrowsAsync<LegacyBackfillIncompleteException>(
                () => LegacyBackfillGuard.EnsureBackfilledAsync(context, DropLegacy));

            Assert.Equal(5, ex.Unmigrated);
            Assert.Equal(DropLegacy, ex.Migration);
            Assert.Contains("ko'chirilmagan", ex.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Ko'chirish muvaffaqiyatli tugagan bazada qo'riqchi YO'L BERADI — aks holda
    /// u foydali migratsiyani ham to'sib qo'yardi.
    /// </summary>
    [Fact]
    public async Task Kochirish_tugagan_bazada_qoriqchi_yol_beradi()
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
            var initializer = new DatabaseInitializer(
                context, backup: null, new CardOccurrenceProjector(context));

            await initializer.InitializeAsync();
            Assert.Equal(5, initializer.LastBackfill!.Cards);
        }

        using (var context = CreateContext(connection))
        {
            Assert.Equal(0, await LegacyBackfillGuard.CountUnmigratedAsync(context));

            // Istisno CHIQMAYDI.
            await LegacyBackfillGuard.EnsureBackfilledAsync(context, DropLegacy);
        }
    }

    /// <summary>
    /// <b>Qisman ko'chirilgan</b> baza eng xavfli holat: jadvalda kartochkalar bor,
    /// ya'ni yuzaki qaraganda "ko'chirilgan" ko'rinadi. Qo'riqchi har bir qatorni
    /// alohida tekshiradi va bitta yetim qator ham migratsiyani to'xtatadi.
    /// </summary>
    [Fact]
    public async Task Qisman_kochirilgan_bazada_ham_rad_etiladi()
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
            var initializer = new DatabaseInitializer(
                context, backup: null, new CardOccurrenceProjector(context));

            await initializer.InitializeAsync();
        }

        // Ko'chirishdan KEYIN paydo bo'lgan yozuv (masalan eski Desktop hali yozgan).
        using (var context = CreateContext(connection))
        {
            context.ScheduleEntries.Add(new Domain.Entities.ScheduleEntry
            {
                ScheduleId = 1,
                ClassGroupId = 1,
                SubjectId = 1,
                TeacherId = 1,
                DayOfWeek = Domain.Enums.WeekDay.Juma,
                LessonNumber = 3,
                RoomNumber = "101",
            });

            await context.SaveChangesAsync();
        }

        using (var context = CreateContext(connection))
        {
            Assert.Equal(6, await context.ScheduleEntries.CountAsync());
            Assert.Equal(5, await context.Cards.CountAsync());

            Assert.Equal(1, await LegacyBackfillGuard.CountUnmigratedAsync(context));

            var ex = await Assert.ThrowsAsync<LegacyBackfillIncompleteException>(
                () => LegacyBackfillGuard.EnsureBackfilledAsync(context, DropLegacy));

            Assert.Equal(1, ex.Unmigrated);
        }
    }

    /// <summary>
    /// Eski jadval umuman bo'lmagan bazada (migratsiya allaqachon o'tgan) qo'riqchi
    /// tinch o'tadi — takroriy startda dastur ishga tushmay qolmaydi.
    /// </summary>
    [Fact]
    public async Task Eski_jadval_yoq_bolsa_qoriqchi_tinch_otadi()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var context = CreateContext(connection))
        {
            await new DatabaseInitializer(context).InitializeAsync();
        }

        // V2_08 dan keyingi holatni modellashtiramiz: jadval yo'q.
        Execute(connection, "DROP TABLE ScheduleEntries;");

        using (var context = CreateContext(connection))
        {
            Assert.Equal(0, await LegacyBackfillGuard.CountUnmigratedAsync(context));
            await LegacyBackfillGuard.EnsureBackfilledAsync(context, DropLegacy);
        }
    }

    // ---------------------------------------------------------------------
    // Yordamchilar
    // ---------------------------------------------------------------------

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
}
