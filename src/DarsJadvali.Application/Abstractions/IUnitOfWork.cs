using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Abstractions;

/// <summary>Barcha repozitoriylarni birlashtiruvchi ish birligi.</summary>
public interface IUnitOfWork
{
    /// <summary>O'qituvchilar.</summary>
    IRepository<Teacher> Teachers { get; }

    /// <summary>Fanlar.</summary>
    IRepository<Subject> Subjects { get; }

    /// <summary>Sinflar.</summary>
    IRepository<ClassGroup> ClassGroups { get; }

    /// <summary>Biriktirmalar.</summary>
    IRepository<TeacherAssignment> Assignments { get; }

    /// <summary>Ish kunlari.</summary>
    IRepository<WorkDay> WorkDays { get; }

    /// <summary>O'qituvchi ish vaqtlari.</summary>
    IRepository<TeacherAvailability> Availabilities { get; }

    /// <summary>O'quv yillari.</summary>
    IRepository<AcademicYear> AcademicYears { get; }

    /// <summary>Dars jadvallari (variantlari).</summary>
    IRepository<Schedule> Schedules { get; }

    /// <summary>Jadval yozuvlari.</summary>
    IRepository<ScheduleEntry> ScheduleEntries { get; }

    /// <summary>Dars soatlari (vaqt oraliqlari).</summary>
    IRepository<LessonSlot> LessonSlots { get; }

    /// <summary>O'zgarishlarni saqlaydi.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
