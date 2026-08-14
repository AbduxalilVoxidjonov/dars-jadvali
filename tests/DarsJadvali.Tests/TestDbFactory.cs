using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.DependencyInjection;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarsJadvali.Tests;

/// <summary>
/// Har bir test uchun to'liq izolyatsiyalangan SQLite (in-memory) baza va DI konteyner.
/// Ulanish (<see cref="SqliteConnection"/>) ochiq turadi — yopilsa baza yo'qoladi,
/// shuning uchun <see cref="Dispose"/> gacha saqlanadi.
/// </summary>
public sealed class TestDbFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _rootScope;
    private readonly List<IServiceScope> _extraScopes = new();

    public TestDbFactory()
    {
        // Arrange: xotiradagi SQLite bazasi — fayl yaratilmaydi, testlar bir-biriga xalaqit bermaydi.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        // Infrastructure qatlami qo'lda ro'yxatdan o'tkaziladi: bizga fayl emas,
        // ochiq turgan ulanish ustidagi DbContext kerak.
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        // Application qatlami — kontrakt 2.5 bo'yicha barcha servis, validator va generator.
        services.AddApplication();

        _provider = services.BuildServiceProvider();
        _rootScope = _provider.CreateScope();

        Context = _rootScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Context.Database.EnsureCreated();
    }

    /// <summary>Testlar to'g'ridan-to'g'ri ishlatadigan DbContext (servislar bilan bir xil nusxa).</summary>
    public AppDbContext Context { get; }

    /// <summary>Asosiy skop — servislar shundan olinadi.</summary>
    public IServiceProvider Services => _rootScope.ServiceProvider;

    /// <summary>Servisni asosiy skopdan oladi.</summary>
    public T Get<T>() where T : notnull => _rootScope.ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// Yangi skop (yangi DbContext) yaratadi — tracking keshini chetlab o'tish uchun.
    /// AutoInclude testlarida shu kerak bo'ladi.
    /// </summary>
    public T GetFromNewScope<T>() where T : notnull
    {
        var scope = _provider.CreateScope();
        _extraScopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    // ---------------------------------------------------------------------
    // Seed yordamchilari
    // ---------------------------------------------------------------------

    /// <summary>Dushanba–Shanba faol, Yakshanba nofaol. Har kuni <paramref name="maxLessons"/> ta dars.</summary>
    public void SeedWorkDays(int maxLessons = 7)
    {
        foreach (var day in WeekDayExtensions.All)
        {
            Context.WorkDays.Add(new WorkDay
            {
                DayOfWeek = day,
                IsActive = day != WeekDay.Yakshanba,
                MaxLessonsPerDay = maxLessons
            });
        }

        Context.SaveChanges();
    }

    /// <summary>08:30 dan boshlab 45 daqiqa dars + 10 daqiqa tanaffus.</summary>
    public void SeedLessonSlots(int count = 7)
    {
        var start = new TimeSpan(8, 30, 0);
        for (var number = 1; number <= count; number++)
        {
            var end = start + TimeSpan.FromMinutes(45);
            Context.LessonSlots.Add(new LessonSlot
            {
                LessonNumber = number,
                StartTime = start,
                EndTime = end
            });
            start = end + TimeSpan.FromMinutes(10);
        }

        Context.SaveChanges();
    }

    /// <summary>Ish kunlari + dars soatlari.</summary>
    public void SeedDefaults(int maxLessons = 7)
    {
        SeedWorkDays(maxLessons);
        SeedLessonSlots(maxLessons);
    }

    public Teacher AddTeacher(string fullName = "Aliyev Vali", bool isActive = true)
    {
        var teacher = new Teacher { FullName = fullName, IsActive = isActive };
        Context.Teachers.Add(teacher);
        Context.SaveChanges();
        return teacher;
    }

    public Subject AddSubject(string name = "Matematika", string? code = null)
    {
        var subject = new Subject { Name = name, Code = code ?? name[..Math.Min(3, name.Length)].ToUpperInvariant() };
        Context.Subjects.Add(subject);
        Context.SaveChanges();
        return subject;
    }

    public ClassGroup AddClassGroup(string name = "5-A", string? room = null, int studentCount = 25)
    {
        var group = new ClassGroup { Name = name, RoomNumber = room, StudentCount = studentCount };
        Context.ClassGroups.Add(group);
        Context.SaveChanges();
        return group;
    }

    public TeacherAssignment AddAssignment(Teacher teacher, Subject subject, ClassGroup classGroup, int weeklyHours = 5)
    {
        var assignment = new TeacherAssignment
        {
            TeacherId = teacher.Id,
            SubjectId = subject.Id,
            ClassGroupId = classGroup.Id,
            WeeklyHoursCount = weeklyHours
        };
        Context.TeacherAssignments.Add(assignment);
        Context.SaveChanges();
        return assignment;
    }

    /// <summary>
    /// O'quv yili + "Asosiy jadval" (faol) yaratadi yoki mavjudini qaytaradi.
    /// Dars yozuvlari majburiy ravishda biror jadvalga tegishli bo'lishi kerak.
    /// </summary>
    public Schedule EnsureActiveSchedule()
    {
        var active = Context.Schedules.OrderBy(s => s.Id).FirstOrDefault(s => s.IsActive);
        if (active is not null) return active;

        var year = Context.AcademicYears.OrderBy(y => y.Id).FirstOrDefault();
        if (year is null)
        {
            year = new AcademicYear { Name = "2025–2026", StartYear = 2025 };
            Context.AcademicYears.Add(year);
            Context.SaveChanges();
        }

        active = new Schedule
        {
            AcademicYearId = year.Id,
            Name = "Asosiy jadval",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        Context.Schedules.Add(active);
        Context.SaveChanges();
        return active;
    }

    /// <summary>Yangi o'quv yili qo'shadi.</summary>
    public AcademicYear AddAcademicYear(string name = "2026–2027", int startYear = 2026)
    {
        var year = new AcademicYear { Name = name, StartYear = startYear };
        Context.AcademicYears.Add(year);
        Context.SaveChanges();
        return year;
    }

    /// <summary>O'quv yili ichida yangi jadval (variant) qo'shadi.</summary>
    public Schedule AddSchedule(AcademicYear year, string name = "2-variant", bool isActive = false)
    {
        var schedule = new Schedule
        {
            AcademicYearId = year.Id,
            Name = name,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        Context.Schedules.Add(schedule);
        Context.SaveChanges();
        return schedule;
    }

    public ScheduleEntry AddEntry(
        ClassGroup classGroup,
        Subject subject,
        Teacher teacher,
        WeekDay day,
        int lessonNumber,
        string? room = null,
        Schedule? schedule = null)
    {
        schedule ??= EnsureActiveSchedule();

        var entry = new ScheduleEntry
        {
            ScheduleId = schedule.Id,
            ClassGroupId = classGroup.Id,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            DayOfWeek = day,
            LessonNumber = lessonNumber,
            RoomNumber = room
        };
        Context.ScheduleEntries.Add(entry);
        Context.SaveChanges();
        return entry;
    }

    public TeacherAvailability AddAvailability(
        Teacher teacher,
        WeekDay day,
        TimeSpan start,
        TimeSpan end,
        bool isAvailable = true)
    {
        var availability = new TeacherAvailability
        {
            TeacherId = teacher.Id,
            DayOfWeek = day,
            StartTime = start,
            EndTime = end,
            IsAvailable = isAvailable
        };
        Context.TeacherAvailabilities.Add(availability);
        Context.SaveChanges();
        return availability;
    }

    public void Dispose()
    {
        foreach (var scope in _extraScopes)
        {
            scope.Dispose();
        }

        _rootScope.Dispose();
        _provider.Dispose();
        _connection.Dispose();
    }
}
