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
/// Eski (v1) modeldan sxema v2 ga ma'lumot ko'chirish. Eski jadvallar
/// (<c>ScheduleEntries</c>, <c>TeacherAssignments</c>, <c>ClassGroups</c>,
/// <c>LessonSlots</c>) <b>o'chirilmaydi</b> — 1-bosqich additiv.
/// </summary>
public class LegacyBackfillTests
{
    private const int Classes = 4;
    private const int Teachers = 9;
    private const int Subjects = 7;

    [Fact]
    public async Task Barcha_migratsiyalar_bosh_bazada_qollanadi()
    {
        // Arrange
        using var connection = NewIsolatedConnection();

        // Act
        await using var context = CreateContext(connection);
        await context.Database.MigrateAsync();

        // Assert — barcha v2 jadvallari bor va tashqi kalitlar butun.
        foreach (var table in ExpectedV2Tables)
        {
            Assert.True(TableExists(connection, table), $"'{table}' jadvali yaratilmadi.");
        }

        // Eski jadvallar ham JOYIDA (additiv bosqich).
        foreach (var table in new[] { "ScheduleEntries", "TeacherAssignments", "ClassGroups", "LessonSlots" })
        {
            Assert.True(TableExists(connection, table), $"Eski '{table}' jadvali yo'qolgan.");
        }

        Assert.Equal(0, Scalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task Eski_modeldan_kochirish_barcha_yozuvlarni_saqlaydi()
    {
        // Arrange
        using var connection = NewIsolatedConnection();

        int entryCount, assignmentCount;
        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            (entryCount, assignmentCount) = SeedLegacyWorld(seed);
        }

        // Act
        LegacyBackfillResult result;
        await using (var context = CreateContext(connection))
        {
            var backfill = new LegacyToV2Backfill(context, new CardOccurrenceProjector(context));
            result = await backfill.RunAsync();
        }

        // Assert
        await using (var check = CreateContext(connection))
        {
            // 1:1 — har bir eski dars yozuvi uchun bitta kartochka.
            Assert.Equal(entryCount, await check.Cards.CountAsync());
            Assert.Equal(entryCount, await check.ScheduleEntries.CountAsync());

            // Reja >= fakt.
            Assert.True(await check.Lessons.CountAsync() >= assignmentCount);
            Assert.True(await check.Lessons.SumAsync(l => l.PeriodsPerWeek) >= entryCount);

            // Har sinf uchun 3 bo'linish va 5 guruh.
            Assert.Equal(Classes, await check.SchoolClasses.CountAsync());
            Assert.Equal(Classes * 3, await check.ClassDivisions.CountAsync());
            Assert.Equal(Classes * ClassStructureFactory.GroupsPerClass,
                await check.StudentGroups.CountAsync());

            // Har sinfda AYNAN BITTA "Butun sinf" guruhi.
            var entireCounts = await check.StudentGroups
                .Where(g => g.IsEntireClass)
                .GroupBy(g => g.SchoolClassId)
                .Select(g => g.Count())
                .ToListAsync();

            Assert.Equal(Classes, entireCounts.Count);
            Assert.All(entireCounts, c => Assert.Equal(1, c));

            // Har karta: 1 o'qituvchi + 5 guruh (butun sinf darsi barcha guruhlarni band
            // qiladi) + xona (V2_07 dan keyin xonasi bor kartochkada yana bitta qator).
            var withRoom = await check.CardClassrooms.CountAsync();
            Assert.Equal(
                (entryCount * (1 + ClassStructureFactory.GroupsPerClass)) + withRoom,
                await check.CardOccurrences.CountAsync());

            Assert.Equal(entryCount, await check.CardOccurrences
                .CountAsync(o => o.ResourceKind == ResourceKind.Teacher));

            // V2_07: matn xona nomlari haqiqiy xona yozuvlariga aylandi va bandlikka tushdi.
            Assert.Equal(Classes, await check.Classrooms.CountAsync());
            Assert.Equal(withRoom, await check.CardOccurrences
                .CountAsync(o => o.ResourceKind == ResourceKind.Classroom));

            // Choraklar va smenalar.
            Assert.Equal(4, await check.Terms.CountAsync());
            Assert.Equal(2, await check.Shifts.CountAsync());

            // Eski model BUZILMAGAN.
            Assert.Equal(Classes, await check.ClassGroups.CountAsync());
            Assert.Equal(assignmentCount, await check.TeacherAssignments.CountAsync());

            Assert.Equal(0, Scalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
        }

        Assert.Equal(Classes, result.SchoolClasses);
        Assert.Equal(entryCount, result.Cards);
    }

    [Fact]
    public async Task Kochirish_takror_ishga_tushirilsa_dublikat_yaratmaydi()
    {
        // Arrange
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            SeedLegacyWorld(seed);
        }

        async Task RunAsync()
        {
            await using var context = CreateContext(connection);
            var backfill = new LegacyToV2Backfill(context, new CardOccurrenceProjector(context));
            await backfill.RunAsync();
        }

        // Act — ikki marta.
        await RunAsync();

        int cardsAfterFirst, lessonsAfterFirst, groupsAfterFirst, occurrencesAfterFirst;
        await using (var check = CreateContext(connection))
        {
            cardsAfterFirst = await check.Cards.CountAsync();
            lessonsAfterFirst = await check.Lessons.CountAsync();
            groupsAfterFirst = await check.StudentGroups.CountAsync();
            occurrencesAfterFirst = await check.CardOccurrences.CountAsync();
        }

        await RunAsync();

        // Assert
        await using (var check = CreateContext(connection))
        {
            Assert.Equal(cardsAfterFirst, await check.Cards.CountAsync());
            Assert.Equal(lessonsAfterFirst, await check.Lessons.CountAsync());
            Assert.Equal(groupsAfterFirst, await check.StudentGroups.CountAsync());
            Assert.Equal(occurrencesAfterFirst, await check.CardOccurrences.CountAsync());
        }
    }

    [Fact]
    public async Task Biriktirmasiz_yozuv_uchun_dars_avtomatik_yaratiladi()
    {
        // Arrange — yetim ScheduleEntry: unga mos TeacherAssignment yo'q.
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            SeedLegacyWorld(seed, addOrphanEntry: true);
        }

        // Act
        LegacyBackfillResult result;
        await using (var context = CreateContext(connection))
        {
            var backfill = new LegacyToV2Backfill(context, new CardOccurrenceProjector(context));
            result = await backfill.RunAsync();
        }

        // Assert — ma'lumot yo'qotilmagan.
        Assert.True(result.OrphanLessons >= 1);
        Assert.Contains(result.Messages, m => m.Contains("Yetim yozuv", StringComparison.Ordinal));

        await using (var check = CreateContext(connection))
        {
            Assert.Equal(await check.ScheduleEntries.CountAsync(), await check.Cards.CountAsync());
        }
    }

    // =====================================================================
    // Yordamchilar
    // =====================================================================

    private static AppDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    /// <summary>
    /// Har test uchun to'liq alohida xotiradagi baza.
    /// </summary>
    /// <remarks>
    /// Oddiy <c>DataSource=:memory:</c> YARAMAYDI: Microsoft.Data.Sqlite ulanishlarni
    /// hovuzlaydi (pooling) va qayta ishlatilgan ulanish <b>o'sha</b> xotira bazasini
    /// qaytaradi — parallel ishlaydigan testlar bir-birining ma'lumotini ko'radi.
    /// Nomlangan <c>mode=memory&amp;cache=shared</c> bazasi bu muammoni yechadi.
    /// </remarks>
    private static SqliteConnection NewIsolatedConnection()
    {
        var connection = new SqliteConnection(
            $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Foydalanuvchining haqiqiy bazasiga o'xshash eski (v1) dunyo:
    /// 9 o'qituvchi, 7 fan, 4 sinf, 7 dars soati, 1 o'quv yili, 1 jadval.
    /// Har sinfda o'z o'qituvchisi bor — eski unikal indekslar buzilmaydi.
    /// </summary>
    private static (int Entries, int Assignments) SeedLegacyWorld(
        AppDbContext context, bool addOrphanEntry = false)
    {
        // AddAcademicYearAndSchedule migratsiyasi o'quv yili va "Asosiy jadval" ni
        // ALLAQACHON yaratgan — ularni qayta yaratmaymiz, mavjudini ishlatamiz.
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
                CreatedAt = DateTime.UtcNow
            };
            context.Schedules.Add(schedule);
        }

        for (var i = 1; i <= 7; i++)
        {
            var start = new TimeSpan(8, 30, 0) + TimeSpan.FromMinutes((i - 1) * 55);
            context.LessonSlots.Add(new LessonSlot
            {
                LessonNumber = i,
                StartTime = start,
                EndTime = start + TimeSpan.FromMinutes(45)
            });
        }

        var teachers = new List<Teacher>();
        for (var i = 0; i < Teachers; i++)
        {
            var teacher = new Teacher { FullName = $"O'qituvchi {i + 1} Familiya" };
            teachers.Add(teacher);
            context.Teachers.Add(teacher);
        }

        var subjects = new List<Subject>();
        for (var i = 0; i < Subjects; i++)
        {
            var subject = new Subject { Name = $"Fan {i + 1}", Code = $"F{i + 1:00}" };
            subjects.Add(subject);
            context.Subjects.Add(subject);
        }

        var classes = new List<ClassGroup>();
        for (var i = 0; i < Classes; i++)
        {
            var group = new ClassGroup { Name = $"{5 + i}-A", RoomNumber = $"10{i + 1}", StudentCount = 25 + i };
            classes.Add(group);
            context.ClassGroups.Add(group);
        }

        context.SaveChanges();

        // Biriktirmalar: har sinf uchun 4 ta fan, o'qituvchi sinfga biriktirilgan
        // (shu tufayli bitta o'qituvchi bir vaqtda ikki joyda bo'lmaydi).
        var assignments = 0;
        for (var c = 0; c < Classes; c++)
        {
            for (var s = 0; s < 4; s++)
            {
                context.TeacherAssignments.Add(new TeacherAssignment
                {
                    TeacherId = teachers[c].Id,
                    SubjectId = subjects[s].Id,
                    ClassGroupId = classes[c].Id,
                    WeeklyHoursCount = 5
                });
                assignments++;
            }
        }

        context.SaveChanges();

        // Dars yozuvlari: har sinf 5 kun × 4 soat = 20 ta.
        var entries = 0;
        for (var c = 0; c < Classes; c++)
        {
            for (var day = 1; day <= 5; day++)
            {
                for (var lessonNo = 1; lessonNo <= 4; lessonNo++)
                {
                    context.ScheduleEntries.Add(new ScheduleEntry
                    {
                        ScheduleId = schedule.Id,
                        ClassGroupId = classes[c].Id,
                        SubjectId = subjects[lessonNo - 1].Id,
                        TeacherId = teachers[c].Id,
                        DayOfWeek = (WeekDay)day,
                        LessonNumber = lessonNo,
                        RoomNumber = $"10{c + 1}"
                    });
                    entries++;
                }
            }
        }

        if (addOrphanEntry)
        {
            // Biriktirmasi yo'q o'qituvchi + fan uchligi.
            context.ScheduleEntries.Add(new ScheduleEntry
            {
                ScheduleId = schedule.Id,
                ClassGroupId = classes[0].Id,
                SubjectId = subjects[6].Id,
                TeacherId = teachers[8].Id,
                DayOfWeek = WeekDay.Juma,
                LessonNumber = 6
            });
            entries++;
        }

        context.SaveChanges();
        return (entries, assignments);
    }

    private static readonly string[] ExpectedV2Tables =
    {
        "Terms", "Shifts", "Periods", "Grades", "SchoolClasses", "ClassDivisions",
        "StudentGroups", "Classrooms", "Lessons", "LessonTeachers", "LessonClasses",
        "LessonGroups", "LessonClassrooms", "Cards", "CardClassrooms",
        "CardOccurrences", "TimeOffs"
    };

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
