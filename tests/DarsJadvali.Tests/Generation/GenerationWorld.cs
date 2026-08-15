using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using DarsJadvali.Infrastructure.Persistence.Projection;
using DarsJadvali.Infrastructure.Persistence.Scheduling;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// Kartochka (v2) generatsiyasi testlari uchun tayyor "dunyo": o'quv yili, ish kunlari,
/// smenalar, uzluksiz raqamlangan dars soatlari va faol jadval varianti.
/// </summary>
/// <remarks>
/// <c>SchemaV2/V2World</c> dan ataylab mustaqil: bu yerda ish kunlari, smenalar soni
/// va sikldagi haftalar soni testdan boshqariladi.
/// </remarks>
internal sealed class GenerationWorld : IDisposable
{
    private readonly TestDbFactory _db;

    public GenerationWorld(
        int weeksInCycle = 1,
        int shiftCount = 1,
        int periodsPerShift = 6,
        int activeDays = 5,
        int daysPerWeek = 6,
        int maxLessonsPerDay = 0)
    {
        _db = new TestDbFactory();
        Context = _db.Context;

        Year = new AcademicYear
        {
            Name = "2025–2026",
            StartYear = 2025,
            DaysPerWeek = daysPerWeek,
            WeeksInCycle = weeksInCycle,
            TermsCount = 4
        };
        Context.AcademicYears.Add(Year);
        Context.SaveChanges();

        for (var no = 1; no <= shiftCount; no++)
        {
            Context.Shifts.Add(new Shift
            {
                AcademicYearId = Year.Id,
                ShiftNo = no,
                Name = $"{no}-smena",
                ShortName = no.ToString()
            });
        }

        Context.SaveChanges();
        Shifts = Context.Shifts.OrderBy(s => s.ShiftNo).ToList();

        Schedule = new Schedule
        {
            AcademicYearId = Year.Id,
            Name = "Asosiy jadval",
            IsActive = true,
            WeeksInCycle = weeksInCycle,
            CreatedAt = DateTime.UtcNow
        };
        Context.Schedules.Add(Schedule);

        // Dars soatlari smenalar bo'ylab UZLUKSIZ: 1-smena 1..N, 2-smena N+1..2N.
        var total = Math.Max(1, shiftCount) * periodsPerShift;
        for (var no = 1; no <= total; no++)
        {
            var shiftIndex = (no - 1) / periodsPerShift;
            var start = 8 * 60 + 30 + (no - 1) * 55;
            Context.Periods.Add(new Period
            {
                AcademicYearId = Year.Id,
                ShiftId = Shifts.Count > shiftIndex ? Shifts[shiftIndex].Id : null,
                PeriodNo = no,
                StartTime = new TimeOnly(start / 60 % 24, start % 60),
                EndTime = new TimeOnly((start + 45) / 60 % 24, (start + 45) % 60),
                Name = $"{no}-dars",
                ShortName = no.ToString()
            });
        }

        // Ish kunlari: birinchi `activeDays` kun faol.
        for (var dayNo = 0; dayNo < 7; dayNo++)
        {
            Context.WorkDays.Add(new WorkDay
            {
                AcademicYearId = Year.Id,
                DayOfWeek = DayNumbering.ToWeekDay(dayNo),
                DayNo = dayNo,
                IsActive = dayNo < activeDays,
                MaxLessonsPerDay = maxLessonsPerDay > 0 ? maxLessonsPerDay : total,
                Name = DayNumbering.ToWeekDay(dayNo).ToString()
            });
        }

        Context.SaveChanges();

        PeriodsByNo = Context.Periods
            .Where(p => p.AcademicYearId == Year.Id)
            .ToDictionary(p => p.PeriodNo, p => p);
    }

    public AppDbContext Context { get; }

    public AcademicYear Year { get; }

    public Schedule Schedule { get; }

    public IReadOnlyList<Shift> Shifts { get; }

    public Dictionary<int, Period> PeriodsByNo { get; }

    // -------------------------------------------------------------------------
    // Servislar
    // -------------------------------------------------------------------------

    /// <summary>Servisni DI konteynerdan oladi (Desktop/Web ham shu yo'l bilan oladi).</summary>
    public T Get<T>() where T : notnull => _db.Get<T>();

    public ISchedulingStore Store() => new EfSchedulingStore(Context);

    public ISchedulingMapper Mapper() => new SchedulingMapper();

    public IUnitOfWork UnitOfWork() => new UnitOfWork(Context);

    public Application.Abstractions.ICardOccurrenceProjector Projector() =>
        new CardOccurrenceProjector(Context);

    public ScheduleGenerationService Service(ISchedulingStore? store = null) =>
        new(UnitOfWork(), store ?? Store(), Mapper(), Projector());

    public Task<SchedulingInput> LoadAsync() => Store().LoadAsync(Schedule.Id);

    // -------------------------------------------------------------------------
    // Seed
    // -------------------------------------------------------------------------

    /// <summary>Sinf + aSc'dagi standart 3 bo'linish / 5 guruh.</summary>
    public SchoolClass AddClass(string name, Shift? shift = null, int studentCount = 30)
    {
        var schoolClass = new SchoolClass
        {
            AcademicYearId = Year.Id,
            Name = name,
            ShortName = name.Replace("-", string.Empty),
            ShiftId = (shift ?? Shifts.FirstOrDefault())?.Id,
            StudentCount = studentCount
        };

        Context.SchoolClasses.Add(schoolClass);
        Context.SaveChanges();

        ClassStructureFactory.AddStandardStructure(Context, schoolClass, Array.Empty<int>());
        Context.SaveChanges();
        return schoolClass;
    }

    public StudentGroup Group(SchoolClass schoolClass, string name) =>
        Context.StudentGroups.Single(g => g.SchoolClassId == schoolClass.Id && g.Name == name);

    public StudentGroup EntireClass(SchoolClass schoolClass) =>
        Context.StudentGroups.Single(g => g.SchoolClassId == schoolClass.Id && g.IsEntireClass);

    public Teacher AddTeacher(string fullName)
    {
        var teacher = new Teacher
        {
            FullName = fullName,
            AcademicYearId = Year.Id,
            ShortName = fullName[..Math.Min(20, fullName.Length)]
        };

        Context.Teachers.Add(teacher);
        Context.SaveChanges();
        return teacher;
    }

    public Subject AddSubject(string name, string code)
    {
        var subject = new Subject
        {
            Name = name,
            Code = code,
            ShortName = code,
            AcademicYearId = Year.Id
        };

        Context.Subjects.Add(subject);
        Context.SaveChanges();
        return subject;
    }

    public Lesson AddLesson(
        Subject subject,
        Teacher teacher,
        SchoolClass schoolClass,
        StudentGroup? group = null,
        int periodsPerWeek = 2,
        int periodsPerCard = 1,
        int allowedWeeksMask = 0,
        int allowedDaysMask = 0)
    {
        var lesson = new Lesson
        {
            AcademicYearId = Year.Id,
            SubjectId = subject.Id,
            PeriodsPerWeek = periodsPerWeek,
            PeriodsPerCard = periodsPerCard,
            AllowedWeeksMask = allowedWeeksMask,
            AllowedDaysMask = allowedDaysMask
        };

        Context.Lessons.Add(lesson);
        Context.SaveChanges();

        Context.LessonTeachers.Add(new LessonTeacher { LessonId = lesson.Id, TeacherId = teacher.Id });
        Context.LessonClasses.Add(new LessonClass { LessonId = lesson.Id, SchoolClassId = schoolClass.Id });
        Context.LessonGroups.Add(new LessonGroup
        {
            LessonId = lesson.Id,
            StudentGroupId = (group ?? EntireClass(schoolClass)).Id
        });
        Context.SaveChanges();

        return lesson;
    }

    /// <param name="length">
    /// Kartochka egallaydigan soatlar soni. <c>null</c> — darsning <c>PeriodsPerCard</c> istagi.
    /// </param>
    public Card AddCard(
        Lesson lesson, int dayNo, int periodNo,
        int weeksMask = 1, bool isLocked = false, int? length = null)
    {
        var card = new Card
        {
            ScheduleId = Schedule.Id,
            LessonId = lesson.Id,
            PeriodId = PeriodsByNo[periodNo].Id,
            DayNo = dayNo,
            WeeksMask = weeksMask,
            IsLocked = isLocked,
            Length = Math.Max(1, length ?? lesson.PeriodsPerCard)
        };

        Context.Cards.Add(card);
        Context.SaveChanges();
        return card;
    }

    /// <summary>Parallel (sinf darajasi) — <c>ResourceOwnerKind.Grade</c> cheklovi uchun.</summary>
    public Grade AddGrade(int gradeNo)
    {
        var grade = new Grade
        {
            AcademicYearId = Year.Id,
            GradeNo = gradeNo,
            Name = $"{gradeNo}-sinflar",
            ShortName = gradeNo.ToString()
        };

        Context.Grades.Add(grade);
        Context.SaveChanges();
        return grade;
    }

    /// <summary>Xona (P1 — bo'lmasa ham hamma narsa ishlaydi).</summary>
    public Classroom AddClassroom(string name, int? capacity = null, bool isShared = false)
    {
        var classroom = new Classroom
        {
            AcademicYearId = Year.Id,
            Name = name,
            ShortName = name[..Math.Min(24, name.Length)],
            Capacity = capacity,
            IsShared = isShared
        };

        Context.Classrooms.Add(classroom);
        Context.SaveChanges();
        return classroom;
    }

    /// <summary>Kartochkaga xona tayinlaydi (<c>CardClassroom</c>).</summary>
    public CardClassroom AssignRoom(Card card, Classroom classroom)
    {
        var link = new CardClassroom { CardId = card.Id, ClassroomId = classroom.Id };
        Context.CardClassrooms.Add(link);
        Context.SaveChanges();
        return link;
    }

    public TimeOff AddTimeOff(
        ResourceOwnerKind ownerKind,
        int ownerId,
        int dayNo,
        int periodNo,
        AvailabilityLevel level,
        int weeksMask = 0,
        int penalty = 0)
    {
        var timeOff = new TimeOff
        {
            AcademicYearId = Year.Id,
            OwnerKind = ownerKind,
            OwnerId = ownerId,
            DayNo = dayNo,
            PeriodNo = periodNo,
            WeeksMask = weeksMask,
            Availability = level,
            Penalty = penalty
        };

        Context.TimeOffs.Add(timeOff);
        Context.SaveChanges();
        return timeOff;
    }

    public void Dispose() => _db.Dispose();
}
