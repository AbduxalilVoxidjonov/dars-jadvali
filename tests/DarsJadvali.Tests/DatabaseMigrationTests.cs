using DarsJadvali.Application.Abstractions;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// HAQIQIY EF Core migratsiyasi ustidan sinov: eski sxemadagi (ScheduleId siz)
/// to'ldirilgan baza yangi sxemaga ko'chganda ma'lumot YO'QOLMASLIGI shart.
/// Bo'sh baza uchun ham tekshiriladi.
/// </summary>
public class DatabaseMigrationTests
{
    private const string InitialCreateMigration = "20260813142230_InitialCreate";

    [Fact]
    public async Task Eski_bazadagi_dars_yozuvlari_Asosiy_jadvalga_kochiriladi()
    {
        // Arrange — eski sxema (InitialCreate) va unda haqiqiy ma'lumot.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var old = CreateContext(connection))
        {
            await old.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
            SeedLegacyData(connection);

            // Eski sxemada ScheduleId ustuni umuman yo'q.
            Assert.False(ColumnExists(connection, "ScheduleEntries", "ScheduleId"));
            Assert.Equal(5, Scalar(connection, "SELECT COUNT(*) FROM ScheduleEntries"));
        }

        // Act — dastur odatdagidek ishga tushadi.
        using (var context = CreateContext(connection))
        {
            IDatabaseInitializer initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
        }

        // Assert
        using (var context = CreateContext(connection))
        {
            var year = await context.AcademicYears.AsNoTracking().SingleAsync();
            Assert.False(string.IsNullOrWhiteSpace(year.Name));

            var schedule = await context.Schedules.AsNoTracking().SingleAsync();
            Assert.Equal("Asosiy jadval", schedule.Name);
            Assert.True(schedule.IsActive);
            Assert.Equal(year.Id, schedule.AcademicYearId);

            // Barcha 5 ta yozuv joyida va "Asosiy jadval" ga biriktirilgan.
            var entries = await context.ScheduleEntries.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
            Assert.Equal(5, entries.Count);
            Assert.All(entries, e => Assert.Equal(schedule.Id, e.ScheduleId));

            // Yozuvlarning mazmuni ham o'zgarmagan.
            Assert.Equal(new[] { 1, 1, 1, 2, 2 }, entries.Select(e => e.ClassGroupId).ToArray());
            Assert.Equal(new[] { 1, 1, 2, 2, 1 }, entries.Select(e => e.LessonNumber).ToArray());
            Assert.Equal("101", entries[0].RoomNumber);

            // Qolgan ma'lumotlar ham yo'qolmagan.
            Assert.Equal(2, await context.Teachers.CountAsync());
            Assert.Equal(2, await context.Subjects.CountAsync());
            Assert.Equal(2, await context.ClassGroups.CountAsync());
            Assert.Equal(2, await context.TeacherAssignments.CountAsync());
        }
    }

    [Fact]
    public async Task Bosh_bazada_oquv_yili_va_Asosiy_jadval_yaratiladi()
    {
        // Arrange — butunlay yangi (bo'sh) baza.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Act
        using (var context = CreateContext(connection))
        {
            IDatabaseInitializer initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
        }

        // Assert — boshlang'ich ma'lumot ham, jadval ham tayyor.
        using (var context = CreateContext(connection))
        {
            Assert.Equal(7, await context.WorkDays.CountAsync());
            Assert.Equal(7, await context.LessonSlots.CountAsync());
            Assert.Equal(1, await context.AcademicYears.CountAsync());

            var schedule = await context.Schedules.AsNoTracking().SingleAsync();
            Assert.Equal("Asosiy jadval", schedule.Name);
            Assert.True(schedule.IsActive);
            Assert.Equal(0, await context.ScheduleEntries.CountAsync());
        }
    }

    [Fact]
    public async Task Initializer_takror_chaqirilsa_dublikat_yaratmaydi()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Act — dastur bir necha marta ishga tushadi.
        for (var i = 0; i < 3; i++)
        {
            using var context = CreateContext(connection);
            IDatabaseInitializer initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
        }

        // Assert — seed idempotent.
        using (var context = CreateContext(connection))
        {
            Assert.Equal(1, await context.AcademicYears.CountAsync());
            Assert.Equal(1, await context.Schedules.CountAsync());
            Assert.Equal(7, await context.WorkDays.CountAsync());
            Assert.Equal(7, await context.LessonSlots.CountAsync());
        }
    }

    [Fact]
    public async Task Initializer_hech_bir_jadval_faol_bolmasa_bittasini_faollashtiradi()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var context = CreateContext(connection))
        {
            IDatabaseInitializer initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
        }

        // Faollik bayrog'i qandaydir sabab bilan o'chib qolgan holat.
        Execute(connection, "UPDATE Schedules SET IsActive = 0;");

        // Act
        using (var context = CreateContext(connection))
        {
            IDatabaseInitializer initializer = new DatabaseInitializer(context);
            await initializer.InitializeAsync();
        }

        // Assert
        using (var context = CreateContext(connection))
        {
            Assert.True((await context.Schedules.AsNoTracking().SingleAsync()).IsActive);
        }
    }

    // -------------------------------------------------------------------------
    // Yordamchilar
    // -------------------------------------------------------------------------

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

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

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }
}
