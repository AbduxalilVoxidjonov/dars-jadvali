using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Abstractions;

/// <summary>
/// Barcha repozitoriylarni birlashtiruvchi ish birligi.
/// Tranzaksiya va <c>SaveChangesAsync</c> <see cref="ITransactionalUnitOfWork"/> dan meros olinadi
/// (00 §10.8 TODO-2: interfeys Infrastructure'dan shu yerga ko'chirildi).
/// </summary>
public interface IUnitOfWork : ITransactionalUnitOfWork
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
}
