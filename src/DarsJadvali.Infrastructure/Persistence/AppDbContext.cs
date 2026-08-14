using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>Dastur ma'lumotlar bazasi konteksti (SQLite).</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<LessonSlot> LessonSlots => Set<LessonSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
