using DarsJadvali.Application.Export;
using DarsJadvali.Application.Generation;
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
        services.AddScoped<IScheduleGenerator, GreedyScheduleGenerator>();

        // PDF eksport uchun ma'lumot modelini quruvchi (chizuvchining o'zi Infrastructure'da).
        services.AddScoped<ITimetableExportModelBuilder, TimetableExportModelBuilder>();

        return services;
    }
}
