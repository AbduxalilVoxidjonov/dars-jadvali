using System.Globalization;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// <c>V2_06</c> va <c>V2_07</c> migratsiyalarining to'liq oldinga → orqaga → oldinga
/// aylanishi. Har bosqichda ma'lumot yo'qolmasligi va
/// <c>pragma foreign_key_check</c> bo'sh qolishi shart (00 §4.4).
/// </summary>
public class MigrationV2_06_07_Tests
{
    private const string BeforeV2_06 = "20260814154740_V2_05_CardLengthAndConstraints";
    private const string V2_06 = "20260814161551_V2_06_TimeOffFromAvailability";
    private const string V2_07 = "20260814161600_V2_07_ClassroomsFromLegacyRoom";

    /// <summary>Bosqichma-bosqich oldinga va orqaga: ustunlar va indekslar joyiga tushadi.</summary>
    [Fact]
    public async Task V2_06_va_V2_07_oldinga_va_orqaga_ishlaydi()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dj-mig67-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "darsjadvali.db");

        try
        {
            // Arrange — V2_05 gacha: yangi ustunlar hali yo'q.
            await using (var context = Open(path))
            {
                await Migrator(context).MigrateAsync(BeforeV2_06);

                Assert.False(HasColumn(context, "TimeOffs", "LegacyTeacherAvailabilityId"));
                Assert.False(HasColumn(context, "Classrooms", "LegacySourceName"));
            }

            // Act — V2_06.
            await using (var context = Open(path))
            {
                await Migrator(context).MigrateAsync(V2_06);

                Assert.True(HasColumn(context, "TimeOffs", "LegacyTeacherAvailabilityId"));
                Assert.True(HasIndex(context, "IX_TimeOffs_LegacyTeacherAvailabilityId"));

                // V2_07 hali qo'llanmagan.
                Assert.False(HasColumn(context, "Classrooms", "LegacySourceName"));
            }

            // Act — V2_07 (oxirigacha).
            await using (var context = Open(path))
            {
                await context.Database.MigrateAsync();

                Assert.True(HasColumn(context, "Classrooms", "LegacySourceName"));
                Assert.True(HasIndex(context, "UX_Classrooms_AcademicYearId_LegacySourceName"));
                Assert.Empty(ForeignKeyCheck(context));
            }

            // Act — orqaga V2_06 ga.
            await using (var context = Open(path))
            {
                await Migrator(context).MigrateAsync(V2_06);

                Assert.False(HasColumn(context, "Classrooms", "LegacySourceName"));
                Assert.False(HasIndex(context, "UX_Classrooms_AcademicYearId_LegacySourceName"));
                Assert.True(HasColumn(context, "TimeOffs", "LegacyTeacherAvailabilityId"));
                Assert.Empty(ForeignKeyCheck(context));
            }

            // Act — orqaga V2_05 ga.
            await using (var context = Open(path))
            {
                await Migrator(context).MigrateAsync(BeforeV2_06);

                Assert.False(HasColumn(context, "TimeOffs", "LegacyTeacherAvailabilityId"));
                Assert.False(HasIndex(context, "IX_TimeOffs_LegacyTeacherAvailabilityId"));
                Assert.Empty(ForeignKeyCheck(context));
            }

            // Act — yana oldinga: takroriy aylanish ham ishlaydi.
            await using (var context = Open(path))
            {
                await context.Database.MigrateAsync();

                Assert.True(HasColumn(context, "TimeOffs", "LegacyTeacherAvailabilityId"));
                Assert.True(HasColumn(context, "Classrooms", "LegacySourceName"));
                Assert.Empty(ForeignKeyCheck(context));
            }
        }
        finally
        {
            TryDelete(folder);
        }
    }

    /// <summary>
    /// Ma'lumot bilan to'ldirilgan bazada aylanish: qatorlar YO'QOLMAYDI.
    /// </summary>
    [Fact]
    public async Task Malumotli_bazada_aylanish_yozuvlarni_saqlaydi()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dj-mig67d-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "darsjadvali.db");

        try
        {
            int classrooms;
            int timeOffs;

            await using (var context = Open(path))
            {
                await context.Database.MigrateAsync();

                var yearId = context.AcademicYears.OrderBy(y => y.Id).First().Id;

                context.Classrooms.Add(new Domain.Entities.Classroom
                {
                    AcademicYearId = yearId,
                    Name = "101-xona",
                    ShortName = "101",
                    LegacySourceName = "101",
                });

                context.TimeOffs.Add(new Domain.Entities.TimeOff
                {
                    AcademicYearId = yearId,
                    OwnerKind = Domain.Enums.ResourceOwnerKind.Teacher,
                    OwnerId = 1,
                    DayNo = 0,
                    PeriodNo = 5,
                    WeeksMask = 0,
                    Availability = Domain.Enums.AvailabilityLevel.Forbidden,
                    LegacyTeacherAvailabilityId = 7,
                });

                await context.SaveChangesAsync();
                classrooms = await context.Classrooms.CountAsync();
                timeOffs = await context.TimeOffs.CountAsync();
            }

            // Act — orqaga (yangi ustunlar tushadi) va yana oldinga.
            await using (var context = Open(path))
            {
                await Migrator(context).MigrateAsync(BeforeV2_06);
                Assert.Empty(ForeignKeyCheck(context));
            }

            await using (var context = Open(path))
            {
                await context.Database.MigrateAsync();
                Assert.Empty(ForeignKeyCheck(context));

                // Qatorlarning O'ZI joyida — faqat ko'chirish izi ustunlari tozalangan.
                Assert.Equal(classrooms, await context.Classrooms.CountAsync());
                Assert.Equal(timeOffs, await context.TimeOffs.CountAsync());

                var room = await context.Classrooms.AsNoTracking().SingleAsync();
                Assert.Equal("101-xona", room.Name);
                Assert.Null(room.LegacySourceName);

                var cell = await context.TimeOffs.AsNoTracking().SingleAsync();
                Assert.Equal(5, cell.PeriodNo);
                Assert.Equal(Domain.Enums.AvailabilityLevel.Forbidden, cell.Availability);
                Assert.Null(cell.LegacyTeacherAvailabilityId);
            }
        }
        finally
        {
            TryDelete(folder);
        }
    }

    // =====================================================================

    private static IMigrator Migrator(AppDbContext context) => context.GetService<IMigrator>();

    private static bool HasColumn(AppDbContext context, string table, string column)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        context.Database.OpenConnection();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static bool HasIndex(AppDbContext context, string name)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        context.Database.OpenConnection();
        command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{name}';";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static List<string> ForeignKeyCheck(AppDbContext context)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        context.Database.OpenConnection();
        command.CommandText = "PRAGMA foreign_key_check;";

        var result = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    private static AppDbContext Open(string path)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true };
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(builder.ToString()).Options);
    }

    private static void TryDelete(string folder)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
