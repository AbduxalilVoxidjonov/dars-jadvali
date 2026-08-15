using System.Globalization;
using DarsJadvali.Application.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>
/// SQLite <c>VACUUM INTO</c> yordamida zaxira nusxa oluvchi servis.
/// </summary>
/// <remarks>
/// <b>Nega <c>VACUUM INTO</c>, faylni ko'chirish emas.</b> Baza WAL rejimida ishlaydi:
/// oddiy <c>File.Copy</c> <c>-wal</c> va <c>-shm</c> yordamchi fayllarni qoldirib ketadi
/// va nusxa <b>chala</b> bo'lishi mumkin. <c>VACUUM INTO</c> esa tranzaksion jihatdan
/// izchil, siqilgan bitta fayl yozadi va ochiq ulanishni bloklamaydi.
/// <para>
/// Nusxalar <c>&lt;baza papkasi&gt;/backups/</c> ichida saqlanadi;
/// eng so'nggi <see cref="KeepCount"/> tasi qoldiriladi.
/// </para>
/// </remarks>
public sealed class DatabaseBackupService : IDatabaseBackupService
{
    /// <summary>Saqlanadigan zaxira nusxalar soni.</summary>
    public const int KeepCount = 10;

    /// <summary>Zaxira papkasining nomi.</summary>
    public const string FolderName = "backups";

    private readonly AppDbContext _context;

    /// <summary>Yangi servis yaratadi.</summary>
    public DatabaseBackupService(AppDbContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public async Task<string?> CreateBackupAsync(
        bool onlyIfMigrationsPending = true, CancellationToken ct = default)
    {
        var source = ResolveFilePath();

        // Xotiradagi baza (testlar) yoki hali yaratilmagan fayl — zaxira mavzusi yo'q.
        if (source is null || !File.Exists(source)) return null;

        if (onlyIfMigrationsPending)
        {
            var pending = await _context.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false);
            if (!pending.Any()) return null;
        }

        var folder = Path.Combine(Path.GetDirectoryName(source)!, FolderName);
        Directory.CreateDirectory(folder);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(source)}-{stamp}.db");

        // Bir soniya ichida ikkinchi chaqiruv bo'lsa nom to'qnashmasin.
        var attempt = 1;
        while (File.Exists(target))
        {
            target = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(source)}-{stamp}-{attempt++}.db");
        }

        // VACUUM INTO parametrni qabul qilmaydi — yo'l SQL satriga qo'shiladi,
        // shuning uchun tirnoq EKRANLANADI (SQL injection va buzilgan yo'llarga qarshi).
        var escaped = target.Replace("'", "''", StringComparison.Ordinal);
        var sql = string.Concat("VACUUM INTO '", escaped, "';");
        await _context.Database.ExecuteSqlRawAsync(sql, ct).ConfigureAwait(false);

        Prune(folder, Path.GetFileNameWithoutExtension(source));
        return target;
    }

    /// <summary>Eng so'nggi <see cref="KeepCount"/> tadan ortiqchasini o'chiradi.</summary>
    private static void Prune(string folder, string prefix)
    {
        try
        {
            var files = new DirectoryInfo(folder)
                .GetFiles($"{prefix}-*.db")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(KeepCount)
                .ToList();

            foreach (var file in files) file.Delete();
        }
        catch (IOException)
        {
            // Eski nusxani o'chira olmaslik — zaxira olishning o'zi uchun halokat emas.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Ulanish satridan baza fayli yo'lini oladi (xotiradagi baza uchun <c>null</c>).</summary>
    private string? ResolveFilePath()
    {
        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) return null;

        var source = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(source)) return null;

        // ":memory:" va "Mode=Memory" holatlari.
        if (source.Contains(":memory:", StringComparison.OrdinalIgnoreCase)) return null;
        if (connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)) return null;

        var full = Path.GetFullPath(source);
        return string.IsNullOrEmpty(Path.GetDirectoryName(full)) ? null : full;
    }
}
