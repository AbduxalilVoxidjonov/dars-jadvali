using DarsJadvali.Application.DependencyInjection;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.DependencyInjection;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DarsJadvali.Tests.Security;

/// <summary>
/// W-02: Web va Desktop AYNI baza faylini ochadi. Ulanish WAL rejimida,
/// kutish vaqti va tashqi kalitlar yoqilgan holda ochilishi shart —
/// aks holda "database is locked" chiqadi.
/// </summary>
public class SqliteConnectionSettingsTests : IDisposable
{
    private readonly string _folder;
    private readonly string _dbPath;

    public SqliteConnectionSettingsTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "darsjadvali-tests", Guid.NewGuid().ToString("N"));
        _dbPath = Path.Combine(_folder, "test.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Vaqtinchalik papkani tozalay olmaslik testni yiqitmasin.
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Ulanish satri fayl yo'lini va tashqi kalitlarni o'z ichiga oladi.</summary>
    [Fact]
    public void Ulanish_satri_fayl_yolini_va_foreign_keys_ni_oz_ichiga_oladi()
    {
        var connectionString = InfrastructureServiceRegistration.BuildSqliteConnectionString(_dbPath);

        var builder = new SqliteConnectionStringBuilder(connectionString);

        Assert.Equal(Path.GetFullPath(_dbPath), builder.DataSource);
        Assert.True(builder.ForeignKeys);
        Assert.True(Directory.Exists(_folder), "Baza papkasi yaratilishi kerak edi.");
    }

    /// <summary>Yo'l bo'sh bo'lsa standart yo'l ishlatiladi — istisno tashlanmaydi.</summary>
    [Fact]
    public void Bosh_yol_berilsa_standart_yol_ishlatiladi()
    {
        var connectionString = InfrastructureServiceRegistration.BuildSqliteConnectionString("  ");

        var builder = new SqliteConnectionStringBuilder(connectionString);

        Assert.Equal(
            Path.GetFullPath(InfrastructureServiceRegistration.DefaultDbPath),
            builder.DataSource);
    }

    /// <summary>Haqiqiy ulanishda uchala PRAGMA ham qo'yilgan bo'ladi.</summary>
    [Fact]
    public async Task Ochilgan_ulanishda_WAL_busy_timeout_va_foreign_keys_yoqilgan()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        Assert.Equal("wal", await ScalarAsync(context, "PRAGMA journal_mode;"));
        Assert.Equal(
            SqlitePragmaInterceptor.BusyTimeoutMilliseconds.ToString(),
            await ScalarAsync(context, "PRAGMA busy_timeout;"));
        Assert.Equal("1", await ScalarAsync(context, "PRAGMA foreign_keys;"));
    }

    /// <summary>
    /// WAL ning maqsadi: bir ulanish yozayotganda ikkinchisi o'qiy olishi.
    /// Eski (DELETE) rejimda bu yerda "database is locked" chiqardi.
    /// </summary>
    [Fact]
    public async Task Ochiq_tranzaksiya_paytida_ikkinchi_ulanish_oqiy_oladi()
    {
        await using var provider = BuildProvider();

        using var writerScope = provider.CreateScope();
        var writer = writerScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await writer.Database.EnsureCreatedAsync();

        using var readerScope = provider.CreateScope();
        var reader = readerScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await writer.Database.BeginTransactionAsync();
        writer.Teachers.Add(new Teacher { FullName = "Aliyev Vali" });
        await writer.SaveChangesAsync();

        // Yozuv hali tasdiqlanmagan — o'qish esa bloklanmasligi kerak.
        var count = await reader.Teachers.CountAsync();
        Assert.Equal(0, count);

        await transaction.CommitAsync();

        Assert.Equal(1, await reader.Teachers.CountAsync());
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructureSqlite(_dbPath);
        return services.BuildServiceProvider();
    }

    private static async Task<string?> ScalarAsync(AppDbContext context, string sql)
    {
        // Ulanish AYNAN EF orqali ochiladi — faqat shunda interceptor ishga tushadi
        // (qo'lda DbConnection.Open() qilinsa PRAGMA'lar qo'yilmay qolardi).
        await context.Database.OpenConnectionAsync();

        var connection = context.Database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();

        return value?.ToString()?.ToLowerInvariant();
    }
}
