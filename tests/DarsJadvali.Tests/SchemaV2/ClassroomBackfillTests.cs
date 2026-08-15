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
/// <c>V2_07</c> — <c>Card.LegacyRoomNumber</c> (erkin matn) dan <c>Classroom</c> +
/// <c>CardClassroom</c> ga ko'chirish.
/// </summary>
/// <remarks>
/// Eng muhim natija: xona endi <c>CardOccurrence</c> ga tushadi, ya'ni "bitta xonada
/// ikki dars" holati BAZA darajasida rad etiladi. Ilgari <c>LegacyRoomNumber</c>
/// proyeksiyaga umuman tushmasdi va bu holat hech qanday joyda ushlanmasdi.
/// </remarks>
public class ClassroomBackfillTests
{
    [Fact]
    public async Task Matn_xona_nomlaridan_takrorlanmaydigan_xona_yozuvlari_yaratiladi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var world = SeedLegacyWorld(seed);

            // Bir xil xona ikki sinfda, turli soatda — bitta Classroom yozuvi bo'lishi kerak.
            AddEntry(seed, world, classIndex: 0, day: WeekDay.Dushanba, lessonNo: 1, room: "101");
            AddEntry(seed, world, classIndex: 1, day: WeekDay.Dushanba, lessonNo: 2, room: "101");
            AddEntry(seed, world, classIndex: 0, day: WeekDay.Seshanba, lessonNo: 1, room: "202-lab");
            seed.SaveChanges();
        }

        var result = await RunAsync(connection);

        await using var check = CreateContext(connection);
        var rooms = await check.Classrooms.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        Assert.Equal(2, rooms.Count);
        Assert.Equal(new[] { "101", "202-lab" }, rooms.Select(r => r.Name).ToArray());
        Assert.Equal(new[] { "101", "202-lab" }, rooms.Select(r => r.LegacySourceName).ToArray());
        Assert.Equal(2, result.Classrooms);
        Assert.Equal(3, result.CardClassrooms);
        Assert.Equal(0, result.RoomConflicts);

        // Bandlik: har kartochka uchun xona qatori ham bor.
        Assert.Equal(3, await check.CardOccurrences.CountAsync(o => o.ResourceKind == ResourceKind.Classroom));
    }

    [Fact]
    public async Task Bosh_va_null_xona_nomlari_otkazib_yuboriladi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var world = SeedLegacyWorld(seed);

            AddEntry(seed, world, 0, WeekDay.Dushanba, 1, room: null);
            AddEntry(seed, world, 0, WeekDay.Dushanba, 2, room: "   ");
            AddEntry(seed, world, 0, WeekDay.Dushanba, 3, room: string.Empty);
            AddEntry(seed, world, 0, WeekDay.Dushanba, 4, room: "  101  ");
            seed.SaveChanges();
        }

        var result = await RunAsync(connection);

        await using var check = CreateContext(connection);

        // Faqat bitta haqiqiy nom — u ham qirqilgan holda.
        var room = Assert.Single(await check.Classrooms.AsNoTracking().ToListAsync());
        Assert.Equal("101", room.Name);
        Assert.Equal("101", room.LegacySourceName);

        Assert.Equal(1, result.Classrooms);
        Assert.Equal(1, result.CardClassrooms);
        Assert.Equal(1, await check.CardClassrooms.CountAsync());
        Assert.Equal(4, await check.Cards.CountAsync());
    }

    [Fact]
    public async Task Kochirish_takror_ishga_tushirilsa_dublikat_xona_yaratmaydi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var world = SeedLegacyWorld(seed);

            AddEntry(seed, world, 0, WeekDay.Dushanba, 1, "101");
            AddEntry(seed, world, 1, WeekDay.Dushanba, 2, "101");
            AddEntry(seed, world, 0, WeekDay.Seshanba, 1, "102");
            seed.SaveChanges();
        }

        await RunAsync(connection);

        int rooms, links, occurrences;
        await using (var check = CreateContext(connection))
        {
            rooms = await check.Classrooms.CountAsync();
            links = await check.CardClassrooms.CountAsync();
            occurrences = await check.CardOccurrences.CountAsync();
        }

        var second = await RunAsync(connection);

        await using (var check = CreateContext(connection))
        {
            Assert.Equal(rooms, await check.Classrooms.CountAsync());
            Assert.Equal(links, await check.CardClassrooms.CountAsync());
            Assert.Equal(occurrences, await check.CardOccurrences.CountAsync());
        }

        Assert.Equal(0, second.Classrooms);
        Assert.Equal(0, second.CardClassrooms);
    }

    /// <summary>
    /// Eski bazada bir xonaga ikki dars yozilgan bo'lishi mumkin (eski model buni
    /// tekshirmagan). Ko'chirish YIQILMAYDI: birinchisi xonani oladi, ikkinchisi
    /// xonasiz qoladi va hisobotda sanaladi.
    /// </summary>
    [Fact]
    public async Task Eski_bazadagi_xona_toqnashuvi_kochirishni_yiqitmaydi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var world = SeedLegacyWorld(seed);

            // AYNAN bir kun, bir soat, bir xona — ikki turli sinf.
            AddEntry(seed, world, 0, WeekDay.Dushanba, 1, "101");
            AddEntry(seed, world, 1, WeekDay.Dushanba, 1, "101");
            seed.SaveChanges();
        }

        var result = await RunAsync(connection);

        await using var check = CreateContext(connection);

        Assert.Equal(1, result.Classrooms);
        Assert.Equal(1, result.CardClassrooms);
        Assert.Equal(1, result.RoomConflicts);
        Assert.Contains(result.Messages, m => m.Contains("allaqachon band edi", StringComparison.Ordinal));

        // Ikkala kartochka ham JOYIDA — ma'lumot yo'qolmadi.
        Assert.Equal(2, await check.Cards.CountAsync());
        Assert.Equal(2, await check.Cards.CountAsync(c => c.LegacyRoomNumber == "101"));
        Assert.Equal(1, await check.CardOccurrences.CountAsync(o => o.ResourceKind == ResourceKind.Classroom));
    }

    /// <summary>
    /// <b>Asosiy natija:</b> ko'chirishdan keyin bitta xonaga ikkinchi darsni qo'yish
    /// baza darajasida RAD ETILADI (ilgari bu holat umuman ushlanmasdi).
    /// </summary>
    [Fact]
    public async Task Bitta_xonada_ikki_dars_endi_rad_etiladi()
    {
        using var connection = NewIsolatedConnection();

        await using (var seed = CreateContext(connection))
        {
            await seed.Database.MigrateAsync();
            var world = SeedLegacyWorld(seed);

            AddEntry(seed, world, 0, WeekDay.Dushanba, 1, "101");
            AddEntry(seed, world, 1, WeekDay.Dushanba, 2, "101");
            seed.SaveChanges();
        }

        await RunAsync(connection);

        await using var context = CreateContext(connection);

        // Ikkinchi kartochkani BIRINCHISINING soatiga ko'chiramiz: sinf va o'qituvchi
        // boshqa, ya'ni to'qnashuvning YAGONA sababi — xona.
        var cards = await context.Cards.OrderBy(c => c.Id).ToListAsync();
        var target = await context.Cards.AsNoTracking().FirstAsync(c => c.Id == cards[0].Id);

        cards[1].PeriodId = target.PeriodId;
        cards[1].DayNo = target.DayNo;
        await context.SaveChangesAsync();

        Application.Abstractions.ICardOccurrenceProjector projector = new CardOccurrenceProjector(context);

        // Assert — bandlik indeksi buni o'tkazmaydi.
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => projector.RebuildForCardAsync(cards[1].Id));
    }

    // =====================================================================

    private static async Task<LegacyBackfillResult> RunAsync(SqliteConnection connection)
    {
        await using var context = CreateContext(connection);
        var backfill = new LegacyToV2Backfill(context, new CardOccurrenceProjector(context));
        return await backfill.RunAsync();
    }

    private sealed record LegacyWorld(
        Schedule Schedule, IReadOnlyList<Teacher> Teachers,
        IReadOnlyList<Subject> Subjects, IReadOnlyList<ClassGroup> Classes);

    /// <summary>2 sinf, 2 o'qituvchi, 2 fan, 7 dars soati — dars yozuvlarisiz.</summary>
    private static LegacyWorld SeedLegacyWorld(AppDbContext context)
    {
        var year = context.AcademicYears.OrderBy(y => y.Id).First();
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
        }

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

        var teachers = new List<Teacher>
        {
            new() { FullName = "Aliyev Vali" },
            new() { FullName = "Karimova Nodira" },
        };

        var subjects = new List<Subject>
        {
            new() { Name = "Matematika", Code = "MAT" },
            new() { Name = "Fizika", Code = "FIZ" },
        };

        var classes = new List<ClassGroup>
        {
            new() { Name = "5-A", StudentCount = 25 },
            new() { Name = "5-B", StudentCount = 26 },
        };

        context.Teachers.AddRange(teachers);
        context.Subjects.AddRange(subjects);
        context.ClassGroups.AddRange(classes);
        context.SaveChanges();

        for (var c = 0; c < classes.Count; c++)
        {
            context.TeacherAssignments.Add(new TeacherAssignment
            {
                TeacherId = teachers[c].Id,
                SubjectId = subjects[c].Id,
                ClassGroupId = classes[c].Id,
                WeeklyHoursCount = 5,
            });
        }

        context.SaveChanges();
        return new LegacyWorld(schedule, teachers, subjects, classes);
    }

    private static void AddEntry(
        AppDbContext context, LegacyWorld world, int classIndex, WeekDay day, int lessonNo, string? room)
    {
        context.ScheduleEntries.Add(new ScheduleEntry
        {
            ScheduleId = world.Schedule.Id,
            ClassGroupId = world.Classes[classIndex].Id,
            SubjectId = world.Subjects[classIndex].Id,
            TeacherId = world.Teachers[classIndex].Id,
            DayOfWeek = day,
            LessonNumber = lessonNo,
            RoomNumber = room,
        });
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static SqliteConnection NewIsolatedConnection()
    {
        var connection = new SqliteConnection(
            $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }
}
