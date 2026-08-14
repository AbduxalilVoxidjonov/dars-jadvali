using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Export;
using DarsJadvali.Infrastructure.Export;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Repositories;
using DarsJadvali.Infrastructure.Update;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DarsJadvali.Infrastructure.DependencyInjection;

/// <summary>Infrastructure qatlamini DI konteynerga ro'yxatdan o'tkazadi.</summary>
public static class InfrastructureServiceRegistration
{
    /// <summary>
    /// Baza fayli uchun standart yo'l:
    /// Windows — <c>%LOCALAPPDATA%\DarsJadvali\darsjadvali.db</c>,
    /// aks holda — <c>~/.local/share/DarsJadvali/darsjadvali.db</c>.
    /// </summary>
    public static string DefaultDbPath
    {
        get
        {
            var baseDir = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create);

            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = Path.Combine(AppContext.BaseDirectory, "data");

            return Path.Combine(baseDir, "DarsJadvali", "darsjadvali.db");
        }
    }

    /// <summary>SQLite ulanish satri bilan ro'yxatdan o'tkazish.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Scoped);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        services.AddExportServices();
        services.AddUpdateChecker();

        return services;
    }

    /// <summary>
    /// Yangilanishni tekshirish servisi. <see cref="HttpClient"/> qo'shimcha paketsiz,
    /// bitta uzoq yashovchi nusxa sifatida yaratiladi (dastur davomida bitta manzilga
    /// kamdan-kam murojaat qilinadi). Kutish vaqti tekshiruvchining o'zida cheklanadi.
    /// Mijoz <see cref="GitHubUpdateChecker.CreateHttpClient"/> orqali yaratiladi —
    /// u redirect'ni AVTOMATIK KUZATMAYDI, aks holda <c>Location</c> sarlavhasi
    /// (so'nggi reliz tegi) yo'qoladi.
    /// </summary>
    public static IServiceCollection AddUpdateChecker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUpdateChecker>(_ =>
            new GitHubUpdateChecker(GitHubUpdateChecker.CreateHttpClient()));

        return services;
    }

    /// <summary>Baza fayli yo'li bilan ro'yxatdan o'tkazish — papkani ham yaratadi.</summary>
    public static IServiceCollection AddInfrastructureSqlite(this IServiceCollection services, string dbFilePath)
    {
        if (string.IsNullOrWhiteSpace(dbFilePath))
            dbFilePath = DefaultDbPath;

        var fullPath = Path.GetFullPath(dbFilePath);
        var folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        return services.AddInfrastructure($"Data Source={fullPath}");
    }

    /// <summary>
    /// PDF eksport servislari: <see cref="ISchoolTimetablePdfExporter"/> (PDFsharp bilan chizadi)
    /// va unga kerak bo'ladigan <see cref="ITimetableExportModelBuilder"/>.
    /// <c>AddApplication()</c> allaqachon quruvchini qo'shgan bo'lsa, u qayta yozilmaydi.
    /// </summary>
    public static IServiceCollection AddExportServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ITimetableExportModelBuilder, TimetableExportModelBuilder>();
        services.TryAddScoped<ISchoolTimetablePdfExporter, SchoolTimetablePdfExporter>();

        return services;
    }
}
