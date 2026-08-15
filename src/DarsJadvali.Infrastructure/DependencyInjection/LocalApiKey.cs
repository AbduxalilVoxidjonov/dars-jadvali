using System.Security.Cryptography;
using System.Text;

namespace DarsJadvali.Infrastructure.DependencyInjection;

/// <summary>So'rovni o'tkazish yoki rad etish qarori.</summary>
public enum ApiKeyDecision
{
    /// <summary>So'rovga ruxsat berildi.</summary>
    Allow,

    /// <summary>Kalit berilmagan yoki noto'g'ri — 401 qaytarilishi kerak.</summary>
    Unauthorized,
}

/// <summary>
/// Lokal (offline) dastur uchun API kalit qoidalari.
/// <para>
/// NIMA UCHUN JWT EMAS: dastur bitta kompyuterda, bitta foydalanuvchi bilan ishlaydi;
/// foydalanuvchilar jadvali, rollar, token muddati va yangilash oqimi yo'q. JWT bularning
/// hech birini bermay, faqat murakkablik qo'shardi. Bir kompyuterda saqlanadigan uzun
/// tasodifiy kalit — shu tahdid modeliga (tarmoqdagi begona qurilma yozuvga tegmasin)
/// yetarli va tushunarli.
/// </para>
/// </summary>
public static class LocalApiKey
{
    /// <summary>Kalit yuboriladigan sarlavha nomi.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>Kalitning bayt uzunligi (256 bit).</summary>
    public const int KeyByteLength = 32;

    /// <summary>Kalit saqlanadigan fayl nomi (baza fayli yonida).</summary>
    public const string KeyFileName = "api-key.txt";

    /// <summary>Yozuv (ma'lumotni o'zgartiradigan) usullar — ular uchun kalit MAJBURIY.</summary>
    public static bool IsWriteMethod(string? httpMethod)
        => httpMethod is not null &&
           (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
            httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// So'rovga ruxsat berilishini hal qiladi.
    /// </summary>
    /// <param name="httpMethod">So'rov usuli.</param>
    /// <param name="providedKey">So'rovdagi <see cref="HeaderName"/> qiymati.</param>
    /// <param name="expectedKey">Serverdagi haqiqiy kalit.</param>
    /// <param name="requireKeyForReads">
    /// <c>true</c> bo'lsa GET/HEAD uchun ham kalit talab qilinadi (masalan dastur
    /// tarmoqqa ochilganda). Standart holatda o'qish erkin — sahifaning o'zi ham
    /// shu server orqali ochiladi.
    /// </param>
    public static ApiKeyDecision Evaluate(
        string? httpMethod,
        string? providedKey,
        string? expectedKey,
        bool requireKeyForReads = false)
    {
        var mustCheck = requireKeyForReads || IsWriteMethod(httpMethod);
        if (!mustCheck)
            return ApiKeyDecision.Allow;

        // Kalit sozlanmagan bo'lsa YOZUVGA RUXSAT YO'Q: "kalit yo'q — hammaga ruxsat"
        // xatosi aynan shu joyda tug'iladi.
        if (string.IsNullOrWhiteSpace(expectedKey))
            return ApiKeyDecision.Unauthorized;

        return KeysMatch(providedKey, expectedKey) ? ApiKeyDecision.Allow : ApiKeyDecision.Unauthorized;
    }

    /// <summary>
    /// Kalitlarni doimiy vaqtda solishtiradi — oddiy <c>==</c> birinchi farq qilgan
    /// belgida to'xtaydi va kalitni belgima-belgi topish (timing attack) imkonini beradi.
    /// </summary>
    public static bool KeysMatch(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
            return false;

        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>Kriptografik tasodifiy yangi kalit yaratadi (URL uchun xavfsiz belgilar).</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyByteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Kalitni fayldan o'qiydi, fayl bo'lmasa yangi kalit yaratib saqlaydi.
    /// Kalit KODDA emas, shu faylda yoki sozlamada turadi.
    /// </summary>
    /// <param name="filePath">Kalit fayli yo'li.</param>
    /// <returns>Kalit va u endigina yaratilganmi.</returns>
    public static (string Key, bool Created) LoadOrCreate(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        if (File.Exists(filePath))
        {
            var existing = File.ReadAllText(filePath).Trim();
            if (existing.Length > 0)
                return (existing, false);
        }

        var key = Generate();
        File.WriteAllText(filePath, key);
        RestrictToOwner(filePath);

        return (key, true);
    }

    /// <summary>Kalit faylini faqat egasi o'qiy oladigan qilib belgilaydi (Unix).</summary>
    private static void RestrictToOwner(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // Fayl tizimi ruxsatni qo'llab-quvvatlamasa — kalit baribir ishlaydi.
        }
    }
}
