using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace DarsJadvali.Application.DependencyInjection;

/// <summary>Application qatlami servislarini DI ga ro'yxatdan o'tkazadi.</summary>
public static class ApplicationServiceRegistration
{
    /// <summary>Barcha servis, validator va generatorni qo'shadi (Scoped).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IClassGroupService, ClassGroupService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IWorkDayService, WorkDayService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IScheduleService, ScheduleService>();

        // O'quv yillari va jadval variantlari (faol jadvalni boshqarish).
        services.AddScoped<IAcademicYearService, AcademicYearService>();
        services.AddScoped<IScheduleSetService, ScheduleSetService>();

        services.AddScoped<IScheduleValidator, ScheduleValidator>();

        // Prezentatsiya qatlami uchun: nusxani bir marta yuklab, keyin XOTIRADA baholash
        // (ScheduleValidator.Evaluate). Qoida shu bilan yagona manbada qoladi.
        services.AddScoped<IScheduleSnapshotProvider, ScheduleSnapshotProvider>();

        // Jadval to'rining Card/Lesson asosidagi servisi (juft dars, hafta maskasi,
        // qulflash, guruh bo'linmasi, joylashtirilmagan darslar).
        services.AddScoped<ICardBoardService, CardBoardService>();

        // Sinf ↔ smena ma'lumotnomasi (SchoolClass.ShiftId). Backfill hamma sinfni
        // 1-smenaga qo'yadi, keyin foydalanuvchi shu servis orqali to'g'rilaydi.
        services.AddScoped<IClassShiftService, ClassShiftService>();

        // Eski (ScheduleEntry asosidagi) generator — Desktop/Web hali shuni chaqiradi.
        // [Obsolete] bo'lgani uchun ogohlantirish shu YAGONA joyda o'chiriladi.
#pragma warning disable CS0618
        services.AddScoped<IScheduleGenerator, GreedyScheduleGenerator>();
#pragma warning restore CS0618

        // Yangi (Lesson + Card asosidagi) generatsiya yadrosi.
        // ISchedulingStore implementatsiyasi Infrastructure qatlamida ro'yxatdan o'tadi.
        services.AddScoped<ISchedulingMapper, SchedulingMapper>();
        services.AddScoped<IScheduleGenerationService, ScheduleGenerationService>();

        // PDF eksport uchun ma'lumot modelini quruvchi (chizuvchining o'zi Infrastructure'da).
        services.AddScoped<ITimetableExportModelBuilder, TimetableExportModelBuilder>();

        return services;
    }
}
