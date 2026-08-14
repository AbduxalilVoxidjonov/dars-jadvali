using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>
/// <c>dotnet ef</c> buyruqlari uchun design-time kontekst fabrikasi.
/// Ish vaqtida ishlatilmaydi — vaqtinchalik fayl yo'lidan foydalanadi.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var path = Path.Combine(Path.GetTempPath(), "darsjadvali_design.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        return new AppDbContext(options);
    }
}
