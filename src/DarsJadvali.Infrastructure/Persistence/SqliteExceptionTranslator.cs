using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>
/// Baza cheklovi buzilganini bildiruvchi <b>tipli</b> istisnolarning umumiy asosi.
/// </summary>
/// <remarks>
/// <b>Nima uchun kerak</b> (00 §5.4). Ilgari UI xato MATNINI tekshirardi
/// (<c>message.Contains("UNIQUE constraint failed")</c>). Bunday tekshiruv indeks nomi
/// yoki SQLite xabari o'zgarishi bilan <b>jimgina</b> buziladi va foydalanuvchi
/// "Noma'lum xato" ko'radi. Endi sabab tur bo'yicha aniqlanadi.
/// <para>
/// <b>Ataylab <see cref="DbUpdateException"/> dan meros olinadi:</b> shu tufayli
/// <c>catch (DbUpdateException)</c> yozgan mavjud kod (va testlar) buzilmaydi, ichki
/// istisnolar zanjiri ham saqlanadi.
/// </para>
/// </remarks>
public abstract class PersistenceConstraintException : DbUpdateException
{
    /// <summary>Yangi istisno yaratadi.</summary>
    /// <param name="message">Foydalanuvchiga ko'rsatiladigan o'zbekcha xabar.</param>
    /// <param name="constraintName">Buzilgan indeks/cheklov nomi (aniqlansa).</param>
    /// <param name="inner">Asl EF/SQLite istisnosi — zanjir uzilmaydi.</param>
    protected PersistenceConstraintException(string message, string? constraintName, Exception inner)
        : base(message, inner)
    {
        ConstraintName = constraintName;
    }

    /// <summary>Buzilgan cheklov/indeks nomi. Aniqlanmasa <c>null</c>.</summary>
    public string? ConstraintName { get; }
}

/// <summary>Unikal indeks buzildi (dublikat qiymat).</summary>
public sealed class UniqueConstraintViolationException : PersistenceConstraintException
{
    /// <summary>Yangi istisno yaratadi.</summary>
    public UniqueConstraintViolationException(string message, string? constraintName, Exception inner)
        : base(message, constraintName, inner)
    {
    }
}

/// <summary>
/// Tashqi kalit buzildi: bog'liq yozuvlari bor ma'lumotnomani o'chirib bo'lmaydi
/// (<c>Restrict</c>) yoki mavjud bo'lmagan yozuvga havola qilindi.
/// </summary>
public sealed class ReferenceConstraintViolationException : PersistenceConstraintException
{
    /// <summary>Yangi istisno yaratadi.</summary>
    public ReferenceConstraintViolationException(string message, string? constraintName, Exception inner)
        : base(message, constraintName, inner)
    {
    }
}

/// <summary><c>CHECK</c> cheklovi buzildi (qiymat ruxsat etilgan oraliqdan tashqarida).</summary>
public sealed class CheckConstraintViolationException : PersistenceConstraintException
{
    /// <summary>Yangi istisno yaratadi.</summary>
    public CheckConstraintViolationException(string message, string? constraintName, Exception inner)
        : base(message, constraintName, inner)
    {
    }
}

/// <summary>
/// SQLite xatolarini tipli istisnolarga o'giradi — matn parsing UI'da emas, shu YAGONA joyda.
/// </summary>
/// <remarks>
/// SQLite kengaytirilgan xato kodlari ishlatiladi (matn emas):
/// <c>1555/2067</c> — unikal, <c>275</c> — CHECK, <c>787</c> — tashqi kalit,
/// <c>1299</c> — NOT NULL. Cheklov nomi faqat <b>xabarni bezash</b> uchun ajratiladi;
/// qaror har doim KODGA asoslanadi.
/// </remarks>
public static class SqliteExceptionTranslator
{
    private const int SqliteConstraintCheck = 275;
    private const int SqliteConstraintForeignKey = 787;
    private const int SqliteConstraintNotNull = 1299;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    /// <summary>
    /// Istisnoni tipli variantiga o'giradi. Mos kelmasa <b>o'zini</b> qaytaradi —
    /// chaqiruvchi har doim <c>throw Translate(ex)</c> yozishi mumkin.
    /// </summary>
    public static Exception Translate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Allaqachon o'girilgan bo'lsa qayta o'ralmaydi.
        if (exception is PersistenceConstraintException) return exception;

        var sqlite = Find(exception);
        if (sqlite is null) return exception;

        var name = ExtractConstraintName(sqlite.Message);

        return sqlite.SqliteExtendedErrorCode switch
        {
            SqliteConstraintUnique or SqliteConstraintPrimaryKey =>
                new UniqueConstraintViolationException(
                    name is null
                        ? "Bunday yozuv allaqachon mavjud — takrorlanishga ruxsat yo'q."
                        : $"Bunday yozuv allaqachon mavjud ({name}) — takrorlanishga ruxsat yo'q.",
                    name, exception),

            SqliteConstraintForeignKey =>
                new ReferenceConstraintViolationException(
                    "Bu yozuvga bog'liq ma'lumotlar bor — avval ularni o'chiring yoki boshqasiga bog'lang.",
                    name, exception),

            SqliteConstraintCheck =>
                new CheckConstraintViolationException(
                    name is null
                        ? "Kiritilgan qiymat ruxsat etilgan oraliqdan tashqarida."
                        : $"Kiritilgan qiymat ruxsat etilgan oraliqdan tashqarida ({name}).",
                    name, exception),

            SqliteConstraintNotNull =>
                new CheckConstraintViolationException(
                    name is null
                        ? "Majburiy maydon to'ldirilmagan."
                        : $"Majburiy maydon to'ldirilmagan ({name}).",
                    name, exception),

            _ => exception,
        };
    }

    /// <summary>Istisno unikal indeks buzilishimi (ichki zanjir bilan birga).</summary>
    public static bool IsUniqueViolation(Exception? exception)
        => exception is UniqueConstraintViolationException
           || Find(exception) is { SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey };

    /// <summary>Istisno tashqi kalit buzilishimi.</summary>
    public static bool IsReferenceViolation(Exception? exception)
        => exception is ReferenceConstraintViolationException
           || Find(exception) is { SqliteExtendedErrorCode: SqliteConstraintForeignKey };

    /// <summary>Zanjirdagi birinchi <see cref="SqliteException"/>.</summary>
    private static SqliteException? Find(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite) return sqlite;
        }

        return null;
    }

    /// <summary>
    /// "UNIQUE constraint failed: Subjects.Code" ko'rinishidagi xabardan cheklov nomini oladi.
    /// Topilmasa <c>null</c> — bu QAROR uchun emas, faqat xabar uchun ishlatiladi.
    /// </summary>
    private static string? ExtractConstraintName(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var index = message.IndexOf(": ", StringComparison.Ordinal);
        if (index < 0 || index + 2 >= message.Length) return null;

        var tail = message[(index + 2)..].Trim();
        var stop = tail.IndexOfAny(new[] { '\r', '\n' });
        if (stop > 0) tail = tail[..stop].Trim();

        return string.IsNullOrWhiteSpace(tail) ? null : tail;
    }
}
