using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Backfill;
using DarsJadvali.Infrastructure.Persistence.Projection;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// Sxema v2 testlari uchun tayyor "dunyo": o'quv yili, ikki smena, 12 ta dars soati
/// (1-smena 1..6, 2-smena 7..12) va faol jadval varianti.
/// </summary>
/// <remarks>
/// Dars soatlari smenalar bo'ylab UZLUKSIZ raqamlanadi — aynan shu tufayli
/// <c>CardOccurrence</c> ning yagona indeksi o'qituvchi bandligini ikkala smenada
/// yaxlit ko'radi.
/// </remarks>
internal sealed class V2World : IDisposable
{
    /// <summary>1-smenadagi dars soatlari soni.</summary>
    public const int PeriodsPerShift = 6;

    private readonly TestDbFactory _db;

    public V2World()
    {
        _db = new TestDbFactory();
        Context = _db.Context;
        Projector = new CardOccurrenceProjector(Context);

        Year = new AcademicYear
        {
            Name = "2025–2026",
            StartYear = 2025,
            DaysPerWeek = 6,
            WeeksInCycle = 2,
            TermsCount = 4
        };
        Context.AcademicYears.Add(Year);
        Context.SaveChanges();

        Term = new Term { AcademicYearId = Year.Id, Ordinal = 1, Name = "I chorak", ShortName = "I" };
        Context.Terms.Add(Term);

        Shift1 = new Shift { AcademicYearId = Year.Id, ShiftNo = 1, Name = "1-smena", ShortName = "1" };
        Shift2 = new Shift { AcademicYearId = Year.Id, ShiftNo = 2, Name = "2-smena", ShortName = "2" };
        Context.Shifts.AddRange(Shift1, Shift2);
        Context.SaveChanges();

        // I chorak uchun ALOHIDA jadval varianti (tasdiqlangan qaror: chorak = variant).
        Schedule = new Schedule
        {
            AcademicYearId = Year.Id,
            TermId = Term.Id,
            Name = "I chorak — asosiy",
            IsActive = true,
            WeeksInCycle = 2,
            CreatedAt = DateTime.UtcNow
        };
        Context.Schedules.Add(Schedule);

        for (var no = 1; no <= PeriodsPerShift * 2; no++)
        {
            var startMinutes = 8 * 60 + 30 + (no - 1) * 55;
            Context.Periods.Add(new Period
            {
                AcademicYearId = Year.Id,
                ShiftId = no <= PeriodsPerShift ? Shift1.Id : Shift2.Id,
                PeriodNo = no,
                StartTime = new TimeOnly(startMinutes / 60, startMinutes % 60),
                EndTime = new TimeOnly((startMinutes + 45) / 60, (startMinutes + 45) % 60),
                Name = $"{no}-dars",
                ShortName = no.ToString()
            });
        }

        Context.SaveChanges();

        Periods = Context.Periods
            .Where(p => p.AcademicYearId == Year.Id)
            .ToDictionary(p => p.PeriodNo, p => p);
    }

    public AppDbContext Context { get; }
    public ICardOccurrenceProjector Projector { get; }
    public AcademicYear Year { get; }
    public Term Term { get; }
    public Shift Shift1 { get; }
    public Shift Shift2 { get; }
    public Schedule Schedule { get; }
    public Dictionary<int, Period> Periods { get; }

    // -------------------------------------------------------------------------

    /// <summary>Sinf + standart 3 bo'linish / 5 guruh yaratadi.</summary>
    public SchoolClass AddClass(string name, Shift? shift = null)
    {
        var schoolClass = new SchoolClass
        {
            AcademicYearId = Year.Id,
            Name = name,
            ShortName = name.Replace("-", string.Empty),
            ShiftId = (shift ?? Shift1).Id,
            StudentCount = 30
        };

        Context.SchoolClasses.Add(schoolClass);
        Context.SaveChanges();

        ClassStructureFactory.AddStandardStructure(Context, schoolClass, Array.Empty<int>());
        Context.SaveChanges();

        return schoolClass;
    }

    /// <summary>Sinfning nomlangan guruhini qaytaradi.</summary>
    public StudentGroup Group(SchoolClass schoolClass, string name)
        => Context.StudentGroups.Single(g => g.SchoolClassId == schoolClass.Id && g.Name == name);

    /// <summary>Sinfning "Butun sinf" guruhini qaytaradi.</summary>
    public StudentGroup EntireClass(SchoolClass schoolClass)
        => Context.StudentGroups.Single(g => g.SchoolClassId == schoolClass.Id && g.IsEntireClass);

    public Teacher AddTeacher(string fullName)
    {
        var teacher = new Teacher
        {
            FullName = fullName,
            AcademicYearId = Year.Id,
            ShortName = fullName[..Math.Min(20, fullName.Length)],
            ContractPeriodsPerWeek = 24,
            ContractRate = 1.0m
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

    /// <summary>Dars ta'rifi: fan + o'qituvchi + sinf + guruh.</summary>
    public Lesson AddLesson(
        Subject subject,
        Teacher teacher,
        SchoolClass schoolClass,
        StudentGroup group,
        int periodsPerWeek = 2,
        int periodsPerCard = 1)
    {
        var lesson = new Lesson
        {
            AcademicYearId = Year.Id,
            SubjectId = subject.Id,
            PeriodsPerWeek = periodsPerWeek,
            PeriodsPerCard = periodsPerCard
        };

        Context.Lessons.Add(lesson);
        Context.SaveChanges();

        Context.LessonTeachers.Add(new LessonTeacher { LessonId = lesson.Id, TeacherId = teacher.Id });
        Context.LessonClasses.Add(new LessonClass { LessonId = lesson.Id, SchoolClassId = schoolClass.Id });
        Context.LessonGroups.Add(new LessonGroup { LessonId = lesson.Id, StudentGroupId = group.Id });
        Context.SaveChanges();

        return lesson;
    }

    /// <summary>Kartochka: darsni kun + dars soatiga qo'yadi.</summary>
    /// <param name="length">
    /// Egallanadigan ketma-ket soatlar soni. <c>null</c> — darsning
    /// <c>PeriodsPerCard</c> istagi olinadi. Uzunlik endi KARTOCHKADA saqlanadi
    /// ("2 + 2 + 1" holati uchun — bir darsning kartochkalari turli uzunlikda bo'ladi).
    /// </param>
    public Card AddCard(Lesson lesson, int dayNo, int periodNo, int weeksMask = 1, int? length = null)
    {
        var card = new Card
        {
            ScheduleId = Schedule.Id,
            LessonId = lesson.Id,
            PeriodId = Periods[periodNo].Id,
            DayNo = dayNo,
            WeeksMask = weeksMask,
            Length = Math.Max(1, length ?? lesson.PeriodsPerCard)
        };

        Context.Cards.Add(card);
        Context.SaveChanges();
        return card;
    }

    /// <summary>Bandlik jadvalini qayta quradi. To'qnashuv bo'lsa xato tashlaydi.</summary>
    public Task<int> RebuildAsync() => Projector.RebuildForScheduleAsync(Schedule.Id);

    /// <summary>Berilgan resursning bandlik qatorlari.</summary>
    public List<CardOccurrence> Occurrences() =>
        Context.CardOccurrences.AsNoTracking()
            .Where(o => o.ScheduleId == Schedule.Id)
            .OrderBy(o => o.DayNo).ThenBy(o => o.PeriodNo).ThenBy(o => o.WeekNo)
            .ToList();

    public void Dispose() => _db.Dispose();
}
