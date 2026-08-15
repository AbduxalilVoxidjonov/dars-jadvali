using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// 00 §10.8 dagi sxema TODO'lari: faol jadval unikal indeksi, <c>ISoftDeletable</c>
/// global filtri, migratsiya oldidan zaxira va tipli baza istisnolari.
/// </summary>
public class SchemaTodoTests
{
    // ---------------------------------------------------------------------
    // 3-TODO: SetActiveAsync tranzaksiyada + filtrlangan UNIQUE indeks
    // ---------------------------------------------------------------------

    /// <summary>Faol jadvalni almashtirish ishlaydi va faqat bittasi faol qoladi.</summary>
    [Fact]
    public async Task Faol_jadval_almashtirilganda_faqat_bittasi_faol_qoladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var first = db.EnsureActiveSchedule();
        var year = db.Context.AcademicYears.OrderBy(y => y.Id).First();
        var second = db.AddSchedule(year, "2-variant");
        var third = db.AddSchedule(year, "3-variant");

        var sets = db.Get<IScheduleSetService>();

        // Act
        await sets.SetActiveAsync(second.Id);
        await sets.SetActiveAsync(third.Id);
        await sets.SetActiveAsync(first.Id);

        // Assert
        db.Context.ChangeTracker.Clear();
        var active = await db.Context.Schedules.AsNoTracking().Where(s => s.IsActive).ToListAsync();
        Assert.Single(active);
        Assert.Equal(first.Id, active[0].Id);
    }

    /// <summary>Ikkinchi faol jadvalni QO'LDA yozib bo'lmaydi — indeks to'sadi.</summary>
    [Fact]
    public async Task Ikkinchi_faol_jadval_indeks_bilan_toziladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var active = db.EnsureActiveSchedule();
        var year = db.Context.AcademicYears.OrderBy(y => y.Id).First();

        // Act — ikkinchi FAOL jadval.
        db.Context.Schedules.Add(new Schedule
        {
            AcademicYearId = year.Id,
            Name = "Ikkinchi faol",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        // Assert
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => db.Context.SaveChangesAsync());

        db.Context.ChangeTracker.Clear();
        Assert.Equal(active.Id, (await db.Context.Schedules.AsNoTracking().SingleAsync(s => s.IsActive)).Id);
    }

    // ---------------------------------------------------------------------
    // 5-TODO: ISoftDeletable global query filter
    // ---------------------------------------------------------------------

    /// <summary>Yumshoq o'chirilgan yozuv oddiy so'rovlarda ko'rinmaydi.</summary>
    [Fact]
    public async Task Yumshoq_ochirilgan_yozuv_sorovlarda_korinmaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var visible = db.AddTeacher("Ko'rinadigan");
        var hidden = db.AddTeacher("Yashirin");

        hidden.IsDeleted = true;
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        // Act + Assert — global filtr.
        var teachers = await db.Context.Teachers.AsNoTracking().ToListAsync();
        Assert.Single(teachers);
        Assert.Equal(visible.Id, teachers[0].Id);

        // Repozitoriy ham shu filtrga bo'ysunadi.
        Assert.Single(await db.Get<IUnitOfWork>().Teachers.GetAllAsync());
        Assert.Null(await db.Get<IUnitOfWork>().Teachers.GetByIdAsync(hidden.Id));

        // Yozuv BAZADA joyida — faqat ko'rinmaydi.
        Assert.Equal(2, await db.Context.Teachers.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>Filtr faqat <c>ISoftDeletable</c> entity'larga qo'yiladi.</summary>
    [Fact]
    public void Filtr_faqat_soft_delete_entitylarida()
    {
        using var db = new TestDbFactory();
        var model = db.Context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Teacher))!.GetQueryFilter());
        Assert.NotNull(model.FindEntityType(typeof(Subject))!.GetQueryFilter());
        Assert.NotNull(model.FindEntityType(typeof(StudentGroup))!.GetQueryFilter());

        // Kartochka va bandlik yumshoq o'chirilmaydi — ularda filtr yo'q.
        Assert.Null(model.FindEntityType(typeof(Card))!.GetQueryFilter());
        Assert.Null(model.FindEntityType(typeof(CardOccurrence))!.GetQueryFilter());
        Assert.Null(model.FindEntityType(typeof(ScheduleEntry))!.GetQueryFilter());
    }

    // ---------------------------------------------------------------------
    // 8-TODO: DatabaseBackupService
    // ---------------------------------------------------------------------

    /// <summary>Migratsiya kutilayotgan bo'lsa zaxira nusxa yaratiladi va ochiladi.</summary>
    [Fact]
    public async Task Migratsiya_oldidan_zaxira_nusxa_olinadi()
    {
        // Arrange — haqiqiy fayl (VACUUM INTO xotiradagi bazada ma'nosiz).
        var folder = Path.Combine(Path.GetTempPath(), $"dj-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "darsjadvali.db");

        try
        {
            // Eski holat: faqat BIRINCHI migratsiya qo'llangan — keyingilari kutmoqda.
            await using (var context = Open(path))
            {
                await context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                    .MigrateAsync("20260813142230_InitialCreate");
            }

            string? backup;
            await using (var context = Open(path))
            {
                // Act
                backup = await new DatabaseBackupService(context).CreateBackupAsync();
            }

            // Assert
            Assert.NotNull(backup);
            Assert.True(File.Exists(backup));
            Assert.StartsWith(
                Path.Combine(folder, DatabaseBackupService.FolderName),
                backup,
                StringComparison.Ordinal);

            // Nusxa haqiqiy, ochiladigan SQLite bazasi.
            await using (var copy = Open(backup!))
            {
                Assert.True(await copy.Database.CanConnectAsync());
            }

            // Migratsiya qolmagach — yangi zaxira olinmaydi.
            await using (var context = Open(path))
            {
                await context.Database.MigrateAsync();
            }

            await using (var context = Open(path))
            {
                Assert.Null(await new DatabaseBackupService(context).CreateBackupAsync());
            }
        }
        finally
        {
            TryDelete(folder);
        }
    }

    /// <summary>Xotiradagi bazada zaxira olinmaydi (fayl yo'q) va xato ham chiqmaydi.</summary>
    [Fact]
    public async Task Xotiradagi_bazada_zaxira_olinmaydi()
    {
        using var db = new TestDbFactory();
        Assert.Null(await new DatabaseBackupService(db.Context).CreateBackupAsync(onlyIfMigrationsPending: false));
    }

    // ---------------------------------------------------------------------
    // V2_05 migratsiyasi: oldinga/orqaga to'liq aylanish
    // ---------------------------------------------------------------------

    /// <summary>
    /// <c>V2_05</c> oldinga va orqaga ishlaydi: <c>Card.Length</c> ustuni qo'shiladi,
    /// <c>Down()</c> da olib tashlanadi va ma'lumot yo'qolmaydi.
    /// </summary>
    [Fact]
    public async Task V2_05_migratsiyasi_oldinga_va_orqaga_ishlaydi()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dj-mig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "darsjadvali.db");

        try
        {
            // Arrange — V2_04 gacha (Length ustuni hali yo'q).
            await using (var context = Open(path))
            {
                await context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                    .MigrateAsync("20260814142701_V2_04_LessonAndCard");

                Assert.False(HasColumn(context, "Cards", "Length"));
            }

            // Act — oldinga.
            await using (var context = Open(path))
            {
                await context.Database.MigrateAsync();
                Assert.True(HasColumn(context, "Cards", "Length"));

                // Faol jadval unikal indeksi ham paydo bo'ldi.
                Assert.True(HasIndex(context, "UX_Schedules_IsActive"));
            }

            // Act — orqaga (V2_04 ga qaytish).
            await using (var context = Open(path))
            {
                await context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
                    .MigrateAsync("20260814142701_V2_04_LessonAndCard");

                Assert.False(HasColumn(context, "Cards", "Length"));
                Assert.False(HasIndex(context, "UX_Schedules_IsActive"));

                // Baza butun: yetim tashqi kalit yo'q.
                Assert.Empty(ForeignKeyCheck(context));
            }

            // Yana oldinga — takroriy aylanish ham ishlaydi.
            await using (var context = Open(path))
            {
                await context.Database.MigrateAsync();
                Assert.True(HasColumn(context, "Cards", "Length"));
            }
        }
        finally
        {
            TryDelete(folder);
        }
    }

    // ---------------------------------------------------------------------
    // 8-TODO: SqliteExceptionTranslator
    // ---------------------------------------------------------------------

    /// <summary>Unikal indeks buzilishi tipli istisnoga o'giriladi (matn parsing emas).</summary>
    [Fact]
    public async Task Unikal_indeks_buzilishi_tipli_istisno_beradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddEntry(group, subject, teacher, Domain.Enums.WeekDay.Dushanba, 1);

        // Act — aynan shu slotga ikkinchi yozuv.
        db.Context.ScheduleEntries.Add(new ScheduleEntry
        {
            ScheduleId = db.EnsureActiveSchedule().Id,
            ClassGroupId = group.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            DayOfWeek = Domain.Enums.WeekDay.Dushanba,
            LessonNumber = 1,
        });

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => db.Context.SaveChangesAsync());

        // Assert — sabab tur bo'yicha aniqlanadi, xabar o'zbekcha, ichki zanjir saqlanadi.
        Assert.True(SqliteExceptionTranslator.IsUniqueViolation(ex));
        Assert.Contains("allaqachon mavjud", ex.Message, StringComparison.Ordinal);
        Assert.IsType<DbUpdateException>(ex.InnerException);
        Assert.NotNull(ex.ConstraintName);
    }

    /// <summary>O'giruvchi mos kelmaydigan istisnoni o'zgartirmaydi.</summary>
    [Fact]
    public void Begona_istisno_ozgartirilmaydi()
    {
        var original = new InvalidOperationException("boshqa xato");
        Assert.Same(original, SqliteExceptionTranslator.Translate(original));
        Assert.False(SqliteExceptionTranslator.IsUniqueViolation(original));
        Assert.False(SqliteExceptionTranslator.IsReferenceViolation(original));
    }

    // ---------------------------------------------------------------------

    /// <summary>Jadvalda shunday ustun bormi (<c>pragma table_info</c>).</summary>
    private static bool HasColumn(AppDbContext context, string table, string column)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        context.Database.OpenConnection();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>Bazada shunday indeks bormi.</summary>
    private static bool HasIndex(AppDbContext context, string name)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        context.Database.OpenConnection();
        command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{name}';";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>Yetim tashqi kalitlar ro'yxati (bo'sh bo'lishi kutiladi).</summary>
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
