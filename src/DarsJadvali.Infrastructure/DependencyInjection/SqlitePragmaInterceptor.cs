using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DarsJadvali.Infrastructure.DependencyInjection;

/// <summary>
/// Har bir ochilgan SQLite ulanishiga majburiy PRAGMA'larni qo'yadi.
/// <para>
/// Sabab: Web va Desktop AYNI <c>darsjadvali.db</c> faylini bir vaqtda ochishi mumkin.
/// Standart (DELETE) jurnal rejimida bitta yozuvchi butun faylni qulflaydi va ikkinchi
/// dastur darhol <c>database is locked</c> xatosini oladi.
/// </para>
/// <list type="bullet">
///   <item><c>journal_mode=WAL</c> — o'qish va yozish bir-birini bloklamaydi.</item>
///   <item><c>busy_timeout=5000</c> — qulf band bo'lsa darhol yiqilmay, 5 soniya kutiladi.</item>
///   <item><c>foreign_keys=ON</c> — SQLite'da bu standart bo'yicha O'CHIQ; yoqilmasa
///         bog'liq yozuvlar tekshirilmay qoladi.</item>
/// </list>
/// PRAGMA'lar ulanish satriga sig'maydi (Microsoft.Data.Sqlite ularni kalit-so'z sifatida
/// qo'llab-quvvatlamaydi), shuning uchun ular ulanish ochilgach yuboriladi.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    /// <summary>Qulf band bo'lganda kutiladigan vaqt (millisekund).</summary>
    public const int BusyTimeoutMilliseconds = 5000;

    /// <summary>Ulanish ochilgach yuboriladigan buyruq.</summary>
    public static readonly string PragmaCommandText =
        "PRAGMA journal_mode=WAL; " +
        "PRAGMA busy_timeout=" + BusyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture) + "; " +
        "PRAGMA foreign_keys=ON;";

    private readonly ILogger<SqlitePragmaInterceptor>? _logger;

    /// <summary>Jurnalsiz nusxa (sinovlar uchun).</summary>
    public SqlitePragmaInterceptor()
    {
    }

    /// <summary>DI konteyner ishlatadigan qurilma.</summary>
    public SqlitePragmaInterceptor(ILogger<SqlitePragmaInterceptor> logger)
        => _logger = logger;

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection);
        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Apply(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PRAGMA'larni yuboradi. Xato yuz bersa dastur YIQILMAYDI: xotiradagi baza
    /// (<c>:memory:</c>) WAL ni qabul qilmaydi, bu esa halokat emas — faqat jurnalga yoziladi.
    /// </summary>
    private void Apply(DbConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = PragmaCommandText;
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "SQLite PRAGMA sozlamalarini qo'llab bo'lmadi.");
        }
    }
}
