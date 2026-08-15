using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Import;
using DarsJadvali.Application.Services;
using DarsJadvali.Infrastructure.Export;
using DarsJadvali.Infrastructure.Export.Printing;
using DarsJadvali.Infrastructure.Import.Xml;
using DarsJadvali.Infrastructure.Persistence;
using DarsJadvali.Infrastructure.Persistence.Projection;
using DarsJadvali.Infrastructure.Persistence.Repositories;
using DarsJadvali.Infrastructure.Persistence.Scheduling;
using DarsJadvali.Infrastructure.Update;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
        // Har bir ochilgan ulanishga WAL / busy_timeout / foreign_keys qo'yiladi:
        // Web va Desktop AYNI faylni bir vaqtda ochganda "database is locked" chiqmasligi uchun.
        services.TryAddSingleton<SqlitePragmaInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) => options
                .UseSqlite(connectionString)
                .AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>()),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Scoped);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        // Migratsiya oldidan avtomatik zaxira (VACUUM INTO) — 00 §4.4.
        services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        services.AddSchedulingPersistence();
        services.AddExportServices();
        services.AddImportServices();
        services.AddUpdateChecker();

        return services;
    }

    /// <summary>
    /// Import servislari: aSc TimeTables XML eksportini o'qish va bazaga yuklash.
    /// </summary>
    /// <remarks>
    /// <see cref="AddSchedulingPersistence"/> ga bog'liq — importer bandlik projektorini
    /// (<c>ICardOccurrenceProjector</c>) chaqiradi. Testlar ikkalasini alohida qo'shishi
    /// mumkin, shuning uchun bu yerda ham chaqiriladi (<c>TryAdd</c> tufayli takroriy
    /// ro'yxatdan o'tkazish zararsiz).
    /// </remarks>
    public static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSchedulingPersistence();
        services.TryAddScoped<IAscXmlImporter, AscXmlImporter>();

        return services;
    }

    /// <summary>
    /// Kartochka (v2) generatsiyasining ma'lumot qatlami: bandlik projektori va
    /// qamrovi aniq o'qish/yozish servisi. <c>AddApplication()</c> bilan birga ishlaydi.
    /// </summary>
    public static IServiceCollection AddSchedulingPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<CardOccurrenceProjector>();

        // Bitta nusxa — ikkala kontrakt uchun: yangi (Application) va eski (Infrastructure) nom.
        services.TryAddScoped<Application.Abstractions.ICardOccurrenceProjector>(
            sp => sp.GetRequiredService<CardOccurrenceProjector>());
        services.TryAddScoped<Persistence.Projection.ICardOccurrenceProjector>(
            sp => sp.GetRequiredService<CardOccurrenceProjector>());
        services.TryAddScoped<ISchedulingStore, EfSchedulingStore>();

        // Jadval varianti nusxalanganda kartochkalar ham ko'chsin (ScheduleSetService
        // buni IXTIYORIY bog'liqlik sifatida oladi — ro'yxatdan o'tmasa eski xatti-harakat).
        services.TryAddScoped<IScheduleCardCopier, ScheduleCardCopier>();

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

        // Jurnal majburiy: ishonchsiz manzil rad etilganda sabab yozilishi kerak.
        services.AddLogging();

        services.TryAddSingleton<IUpdateChecker>(sp =>
            new GitHubUpdateChecker(
                GitHubUpdateChecker.CreateHttpClient(),
                sp.GetRequiredService<ILogger<GitHubUpdateChecker>>()));

        return services;
    }

    /// <summary>Baza fayli yo'li bilan ro'yxatdan o'tkazish — papkani ham yaratadi.</summary>
    public static IServiceCollection AddInfrastructureSqlite(this IServiceCollection services, string dbFilePath)
        => services.AddInfrastructure(BuildSqliteConnectionString(dbFilePath));

    /// <summary>
    /// Baza fayli yo'lidan ulanish satrini quradi va papkani yaratadi.
    /// Yo'l bo'sh bo'lsa <see cref="DefaultDbPath"/> ishlatiladi.
    /// <para>
    /// Satr QO'LDA yopishtirilmaydi — <see cref="SqliteConnectionStringBuilder"/> ishlatiladi:
    /// yo'lda nuqta-vergul yoki tirnoq bo'lsa ham satr buzilmaydi. <c>Foreign Keys=True</c>
    /// shu yerda qo'yiladi (ulanish darajasidagi kafolat), qolgan PRAGMA'lar esa
    /// <see cref="SqlitePragmaInterceptor"/> orqali.
    /// </para>
    /// </summary>
    public static string BuildSqliteConnectionString(string? dbFilePath)
    {
        if (string.IsNullOrWhiteSpace(dbFilePath))
            dbFilePath = DefaultDbPath;

        var fullPath = Path.GetFullPath(dbFilePath);
        var folder = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            ForeignKeys = true,
            // Buyruq kutish vaqti (soniya) — PRAGMA busy_timeout bilan bir xil byudjet.
            DefaultTimeout = SqlitePragmaInterceptor.BusyTimeoutMilliseconds / 1000,
        };

        return builder.ToString();
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

        // Butun maktab jadvali (eski kontrakt) hamon PDFsharp bilan chiziladi.
        services.TryAddScoped<SchoolTimetablePdfExporter>();
        services.TryAddScoped<ISchoolTimetablePdfExporter>(sp => sp.GetRequiredService<SchoolTimetablePdfExporter>());

        // Qamrovi aniq eksport (sinf/o'qituvchi/maktab) — DIZAYN shabloniga asoslangan
        // eksportchi. U ma'lumotni YANGI Card/Lesson modelidan o'qiydi, shuning uchun
        // juft dars, guruh bo'linmasi va A/B hafta PDF ga ham tushadi (eski
        // SchoolTimetablePdfExporter da bu uchtasi umuman yo'q edi).
        services.TryAddScoped<IScopedTimetablePdfExporter>(sp => new DesignBasedTimetablePdfExporter(
            sp.GetRequiredService<ICardBoardService>(),
            sp.GetRequiredService<ISchedulingStore>(),
            sp.GetRequiredService<IUnitOfWork>()));

        return services;
    }
}
