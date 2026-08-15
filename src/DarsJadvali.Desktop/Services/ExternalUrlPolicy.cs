namespace DarsJadvali.Desktop.Services;

/// <summary>
/// Tashqi brauzerda ochishga ruxsat etilgan havolalarni tekshiradi (U-01).
/// </summary>
/// <remarks>
/// <c>Process.Start(UseShellExecute: true)</c> operatsion tizimga havolani "ochib ber" deydi.
/// Agar manzil <b>tarmoqdan</b> kelgan bo'lsa (GitHub reliz javobidagi <c>html_url</c>),
/// u <c>file:</c>, <c>ms-msdt:</c> yoki ixtiyoriy dastur sxemasi bo'lishi mumkin —
/// bu esa foydalanuvchi kompyuterida buyruq bajarilishiga olib keladi.
/// <para>
/// Shuning uchun bu yerda <b>oq ro'yxat</b> ishlatiladi: faqat <c>https</c> va faqat
/// dasturning o'z hostlari. Infrastructure qatlamida ham alohida tekshiruv bor —
/// himoya ataylab ikki qatlamli.
/// </para>
/// </remarks>
public static class ExternalUrlPolicy
{
    /// <summary>Ruxsat etilgan hostlar (kichik harfda, aniq moslik).</summary>
    private static readonly string[] AllowedHosts =
    {
        "github.com",       // reliz sahifasi va repozitoriy
        "www.github.com",
        "t.me",             // muallifning Telegram havolasi (AppInfo.TelegramUrl)
    };

    /// <summary>Havolani brauzerda ochish mumkinmi.</summary>
    /// <param name="url">Tekshiriladigan manzil.</param>
    public static bool IsAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Foydalanuvchi ma'lumoti bo'lgan manzil ("https://github.com@zararli.example") — rad etiladi.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Rad etilgan havola uchun foydalanuvchiga ko'rsatiladigan xabar.</summary>
    /// <param name="url">Rad etilgan manzil.</param>
    public static string RejectionMessage(string? url)
        => "Bu havola xavfsiz emas, shuning uchun ochilmadi.\n\nManzil: " +
           (string.IsNullOrWhiteSpace(url) ? "(bo'sh)" : url) +
           "\n\nDastur faqat rasmiy GitHub va Telegram sahifalarini ochadi.";
}
