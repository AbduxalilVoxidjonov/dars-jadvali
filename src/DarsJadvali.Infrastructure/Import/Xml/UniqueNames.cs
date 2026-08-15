using System.Text.RegularExpressions;

namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>
/// Bitta unikal indeks doirasidagi nomlarni band qilib boradi va to'qnashuvni
/// avtomatik hal qiladi.
/// </summary>
/// <remarks>
/// <para><b>Nima uchun kerak.</b> aSc'da qisqartma unikal emas: bitta eksportda ikkita
/// <c>short="Mat"</c> bo'lishi mumkin, uzunligi ham bizning ustun chegaramizdan
/// oshib ketishi mumkin. Bizda esa qat'iy unikal indekslar bor
/// (<c>UX_Subjects_Code</c>, <c>UX_Teachers_AcademicYearId_ShortName</c>, ...).
/// Import bunda YIQILMASLIGI kerak — nom kesiladi va oxiriga <c>-2</c>, <c>-3</c>
/// qo'shiladi.</para>
/// <para>Idempotentlik uchun <see cref="Release"/> muhim: mavjud yozuvni yangilashdan
/// oldin uning joriy nomi to'plamdan chiqariladi, aks holda ikkinchi importda
/// o'ziga o'zi to'qnashib "Mat-2" ga aylanib ketardi.</para>
/// </remarks>
internal sealed class UniqueNames
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxLength;

    public UniqueNames(int maxLength, IEnumerable<string?>? seed = null)
    {
        _maxLength = Math.Max(1, maxLength);

        if (seed is null) return;
        foreach (var value in seed) Reserve(value);
    }

    /// <summary>Nomni band qilingan deb belgilaydi (tekshirmasdan).</summary>
    public void Reserve(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) _used.Add(value.Trim());
    }

    /// <summary>Nomni bo'shatadi — yozuvni yangilashdan oldin chaqiriladi.</summary>
    public void Release(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) _used.Remove(value.Trim());
    }

    /// <summary>Nom band qilinganmi.</summary>
    public bool IsTaken(string? value) =>
        !string.IsNullOrWhiteSpace(value) && _used.Contains(value.Trim());

    /// <summary>
    /// Nomzod nomni tozalaydi, chegaraga kesadi, bo'sh bo'lsa <paramref name="fallback"/>
    /// ni oladi va band bo'lsa oxiriga tartib raqami qo'shib UNIKAL qiladi.
    /// Natija darhol band qilinadi.
    /// </summary>
    public string Take(string? candidate, string fallback)
    {
        var value = Normalize(candidate);
        if (value.Length == 0) value = Normalize(fallback);
        if (value.Length == 0) value = "x";

        var head = Truncate(value, _maxLength);
        if (_used.Add(head)) return head;

        for (var index = 2; index <= 9999; index++)
        {
            var suffix = "-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var candidateValue = Truncate(value, Math.Max(1, _maxLength - suffix.Length)) + suffix;
            if (_used.Add(candidateValue)) return candidateValue;
        }

        // Amalda yetib bo'lmaydigan tarmoq — baribir unikal qiymat qaytariladi.
        var unique = Truncate(value, Math.Max(1, _maxLength - 9))
                     + "-" + Guid.NewGuid().ToString("N")[..8];
        _used.Add(unique);
        return unique;
    }

    /// <summary>Bo'shliqlarni siqadi va chetlarini kesadi.</summary>
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Whitespace.Replace(value.Trim(), " ");

    /// <summary>Satrni belgilangan uzunlikkacha kesadi. Natija hech qachon bo'sh bo'lmaydi.</summary>
    public static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;

        var cut = value[..maxLength];
        var trimmed = cut.TrimEnd();
        return trimmed.Length == 0 ? cut : trimmed;
    }
}
