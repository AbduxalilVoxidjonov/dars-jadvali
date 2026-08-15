using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence.Backfill;

/// <summary>
/// Eski ma'lumot hali <c>Card</c> modeliga ko'chirilmagan bo'lsa tashlanadi.
/// </summary>
/// <remarks>
/// Bu istisno <b>ataylab</b> dastur startini to'sadi: alternativa — eski jadvalni
/// ko'chirilmagan qatorlari bilan birga tashlash, ya'ni <b>jimgina ma'lumot yo'qotish</b>.
/// Foydalanuvchi zaxira nusxadan tiklanib, avvalgi versiyada bir marta ishga tushirishi
/// (ko'chirish o'sha yerda bajariladi) va keyin yangilanishi kerak.
/// </remarks>
public sealed class LegacyBackfillIncompleteException : InvalidOperationException
{
    /// <summary>Yangi istisno yaratadi.</summary>
    /// <param name="unmigrated">Ko'chirilmagan eski dars yozuvlari soni.</param>
    /// <param name="migration">Rad etilgan (buzuvchi) migratsiya nomi.</param>
    public LegacyBackfillIncompleteException(int unmigrated, string migration)
        : base($"«{migration}» migratsiyasi TO'XTATILDI: {unmigrated} ta eski dars yozuvi " +
               "hali kartochkaga (Card) ko'chirilmagan. Migratsiya bu jadvalni tashlaydi, " +
               "ya'ni bu yozuvlar butunlay yo'qolardi. Avval ko'chirish (backfill) " +
               "muvaffaqiyatli tugashi shart.")
    {
        Unmigrated = unmigrated;
        Migration = migration;
    }

    /// <summary>Ko'chirilmagan eski dars yozuvlari soni.</summary>
    public int Unmigrated { get; }

    /// <summary>Rad etilgan migratsiya nomi.</summary>
    public string Migration { get; }
}

/// <summary>
/// Eski <c>ScheduleEntry</c> jadvalini <b>tashlaydigan</b> migratsiyalar uchun qo'riqchi.
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak.</b> <see cref="DatabaseInitializer"/> ilgari <c>MigrateAsync()</c> ni
/// BIR marta, ko'chirishdan (<c>RunLegacyBackfillAsync</c>) OLDIN chaqirardi. Bu tartibda
/// <c>V2_04</c> gacha yangilanmagan foydalanuvchi bazasida buzuvchi migratsiya
/// <c>ScheduleEntries</c> jadvalini ko'chirish bajarilishidan OLDIN tashlab yuborardi va
/// butun dars jadvali <b>jimgina</b> yo'qolardi.
/// <para>
/// <b>Ikki qavatli himoya.</b>
/// <list type="number">
/// <item><b>Tartib:</b> initsializator migratsiyalarni ikki bosqichga bo'ladi —
/// avval xavfsizlari qo'llanadi, keyin ko'chirish ishlaydi, eng oxirida buzuvchilari
/// (<see cref="Split"/>).</item>
/// <item><b>Qo'riqchi:</b> buzuvchi migratsiya qo'llanishidan oldin
/// <see cref="EnsureBackfilledAsync"/> bazani o'qiydi va ko'chirilmagan qator qolgan
/// bo'lsa <see cref="LegacyBackfillIncompleteException"/> tashlaydi. Ya'ni ko'chirish
/// qandaydir sababga ko'ra bajarilmasa (proyektor yo'q, xato bo'ldi, yetim yozuv
/// o'tkazib yuborildi) migratsiya <b>jimgina o'tib ketmaydi</b>.</item>
/// </list>
/// </para>
/// </remarks>
public static class LegacyBackfillGuard
{
    /// <summary>Eski jadvallarni tashlaydigan migratsiyalar (timestamp'siz nomi).</summary>
    /// <remarks>
    /// Ro'yxat ATAYLAB aniq (naqsh emas): tasodifan biror migratsiya "buzuvchi" deb
    /// belgilanib, kutilmaganda ikkinchi bosqichga surilib qolmasin.
    /// </remarks>
    public static readonly IReadOnlyList<string> DestructiveMigrationNames = new[]
    {
        "V2_08_DropLegacyEntry",
    };

    /// <summary>Eski dars yozuvlari jadvali.</summary>
    private const string LegacyTable = "ScheduleEntries";

    /// <summary>Yangi kartochka jadvali.</summary>
    private const string CardTable = "Cards";

    /// <summary>
    /// Migratsiya eski jadvalni tashlaydimi. Nom EF formatida
    /// (<c>20260815120000_V2_08_DropLegacyEntry</c>) yoki timestamp'siz bo'lishi mumkin.
    /// </summary>
    public static bool IsDestructive(string migrationId)
    {
        if (string.IsNullOrWhiteSpace(migrationId)) return false;

        foreach (var name in DestructiveMigrationNames)
        {
            if (migrationId.Equals(name, StringComparison.Ordinal)) return true;
            if (migrationId.EndsWith("_" + name, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    /// Kutilayotgan migratsiyalarni ikki bosqichga ajratadi: birinchi buzuvchisigacha
    /// bo'lganlari <c>Safe</c>, undan boshlab qolganlarining HAMMASI <c>Destructive</c>.
    /// </summary>
    /// <remarks>
    /// Buzuvchidan KEYINGI migratsiyalar ham ikkinchi bosqichga tushadi — ular
    /// buzuvchining natijasi ustiga quriladi, undan oldin qo'llab bo'lmaydi.
    /// </remarks>
    public static (IReadOnlyList<string> Safe, IReadOnlyList<string> Destructive) Split(
        IEnumerable<string> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var safe = new List<string>();
        var destructive = new List<string>();

        foreach (var migration in pending)
        {
            if (destructive.Count > 0 || IsDestructive(migration))
            {
                destructive.Add(migration);
            }
            else
            {
                safe.Add(migration);
            }
        }

        return (safe, destructive);
    }

    /// <summary>
    /// Hali kartochkaga ko'chirilmagan eski dars yozuvlari soni.
    /// </summary>
    /// <remarks>
    /// So'rov <b>xom SQL</b> bilan bajariladi va buning ikki sababi bor:
    /// <list type="number">
    /// <item>buzuvchi migratsiyadan keyin entity modeli eski jadvalni umuman bilmaydi,
    /// ya'ni LINQ yo'li kompilyatsiya bo'lmaydi;</item>
    /// <item>qo'riqchi HECH QANDAY global filtrga tayanmasligi kerak — u haqiqiy
    /// qatorlarni sanaydi, ko'rinadiganlarini emas.</item>
    /// </list>
    /// </remarks>
    /// <returns>
    /// Ko'chirilmagan qatorlar soni. Eski jadval umuman bo'lmasa (juda yangi baza yoki
    /// migratsiya allaqachon o'tgan) — <c>0</c>.
    /// </returns>
    public static async Task<int> CountUnmigratedAsync(
        AppDbContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!await TableExistsAsync(context, LegacyTable, ct).ConfigureAwait(false))
        {
            return 0;
        }

        // Kartochka jadvali hali yo'q — demak BIRORTA yozuv ko'chirilmagan.
        var sql = await TableExistsAsync(context, CardTable, ct).ConfigureAwait(false)
            ? $"""
               SELECT COUNT(*) FROM {LegacyTable} e
               WHERE NOT EXISTS (
                   SELECT 1 FROM {CardTable} c
                   WHERE c.LegacyScheduleEntryId IS NOT NULL
                     AND c.LegacyScheduleEntryId = e.Id)
               """
            : $"SELECT COUNT(*) FROM {LegacyTable}";

        return await ScalarAsync(context, sql, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Buzuvchi migratsiyadan OLDIN chaqiriladi: ko'chirilmagan qator qolgan bo'lsa
    /// <see cref="LegacyBackfillIncompleteException"/> tashlaydi.
    /// </summary>
    /// <param name="context">Baza konteksti.</param>
    /// <param name="migration">Qo'llanmoqchi bo'lgan migratsiya nomi (xabar uchun).</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    /// <exception cref="LegacyBackfillIncompleteException">Ko'chirilmagan qator bor.</exception>
    public static async Task EnsureBackfilledAsync(
        AppDbContext context, string migration, CancellationToken ct = default)
    {
        var unmigrated = await CountUnmigratedAsync(context, ct).ConfigureAwait(false);
        if (unmigrated > 0)
        {
            throw new LegacyBackfillIncompleteException(unmigrated, migration);
        }
    }

    // ---------------------------------------------------------------------

    private static async Task<bool> TableExistsAsync(
        AppDbContext context, string table, CancellationToken ct)
    {
        var count = await ScalarAsync(
                context,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table",
                ct,
                ("$table", table))
            .ConfigureAwait(false);

        return count > 0;
    }

    private static async Task<int> ScalarAsync(
        AppDbContext context, string sql, CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        var connection = context.Database.GetDbConnection();
        var opened = false;

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
            opened = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is null or DBNull ? 0 : Convert.ToInt32(result);
        }
        finally
        {
            if (opened)
            {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }
}
