using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence.Repositories;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>Bitta <see cref="AppDbContext"/> ustida ishlaydigan repozitoriylar to'plami.</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    private IRepository<Teacher>? _teachers;
    private IRepository<Subject>? _subjects;
    private IRepository<ClassGroup>? _classGroups;
    private IRepository<TeacherAssignment>? _assignments;
    private IRepository<WorkDay>? _workDays;
    private IRepository<TeacherAvailability>? _availabilities;
    private IRepository<AcademicYear>? _academicYears;
    private IRepository<Schedule>? _schedules;
    private IRepository<ScheduleEntry>? _scheduleEntries;
    private IRepository<LessonSlot>? _lessonSlots;

    public UnitOfWork(AppDbContext context) => _context = context;

    public IRepository<Teacher> Teachers => _teachers ??= new EfRepository<Teacher>(_context);
    public IRepository<Subject> Subjects => _subjects ??= new EfRepository<Subject>(_context);
    public IRepository<ClassGroup> ClassGroups => _classGroups ??= new EfRepository<ClassGroup>(_context);
    public IRepository<TeacherAssignment> Assignments => _assignments ??= new EfRepository<TeacherAssignment>(_context);
    public IRepository<WorkDay> WorkDays => _workDays ??= new EfRepository<WorkDay>(_context);
    public IRepository<TeacherAvailability> Availabilities => _availabilities ??= new EfRepository<TeacherAvailability>(_context);
    public IRepository<AcademicYear> AcademicYears => _academicYears ??= new EfRepository<AcademicYear>(_context);
    public IRepository<Schedule> Schedules => _schedules ??= new EfRepository<Schedule>(_context);
    public IRepository<ScheduleEntry> ScheduleEntries => _scheduleEntries ??= new EfRepository<ScheduleEntry>(_context);
    public IRepository<LessonSlot> LessonSlots => _lessonSlots ??= new EfRepository<LessonSlot>(_context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
