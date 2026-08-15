using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>Bitta <see cref="AppDbContext"/> ustida ishlaydigan repozitoriylar to'plami.</summary>
public sealed class UnitOfWork : ITransactionalUnitOfWork
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

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await ExecuteInTransactionAsync<object?>(async token =>
        {
            await action(token).ConfigureAwait(false);
            return null;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Qayta kirishga xavfsiz: tashqarida allaqachon tranzaksiya ochiq bo'lsa
        // ichkarida yangisi ochilmaydi — hammasi bitta atomik amal bo'lib qoladi.
        if (_context.Database.CurrentTransaction is not null)
        {
            return await action(ct).ConfigureAwait(false);
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(ct)
            .ConfigureAwait(false);

        try
        {
            var result = await action(ct).ConfigureAwait(false);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);

            // Rollback'dan keyin kontekst keshida "saqlangan" deb belgilangan, lekin
            // aslida bazaga tushmagan o'zgarishlar qolmasligi kerak.
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}
