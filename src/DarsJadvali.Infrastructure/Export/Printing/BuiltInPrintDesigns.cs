using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>
/// Dastur bilan birga keladigan tayyor dizaynlar. Fayllar assembly ichiga
/// o'rnatilgan (embedded resource) — tashqi papkaga bog'liqlik yo'q, ko'chirib
/// yurish (portable) rejimida ham ishlaydi.
/// </summary>
public static class BuiltInPrintDesigns
{
    private const string ResourcePrefix = "DarsJadvali.Infrastructure.Export.Printing.Designs.";

    /// <summary>Sinf jadvali — ko'k (aSc "Sample Blue" ekvivalenti).</summary>
    public const string ClassBlue = "sinf-kok";

    /// <summary>O'qituvchi jadvali — yashil.</summary>
    public const string TeacherGreen = "oqituvchi-yashil";

    /// <summary>Barcha sinflar bitta varaqda (aSc "internal_table" ekvivalenti).</summary>
    public const string SchoolCompact = "maktab-jamlanma";

    /// <summary>Rasmiy sinf blanki: jadval + darslar ro'yxati + imzo joylari.</summary>
    public const string ClassForm = "sinf-blank";

    private static readonly ConcurrentDictionary<string, PrintDesign> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mavjud dizayn kalitlari.</summary>
    public static IReadOnlyList<string> Keys { get; } = new[] { ClassBlue, TeacherGreen, SchoolCompact, ClassForm };

    /// <summary>Kalit bo'yicha dizaynni oladi (birinchi chaqiruvda o'qiydi va keshlaydi).</summary>
    /// <param name="key">Dizayn kaliti, masalan <see cref="ClassBlue"/>.</param>
    /// <exception cref="PrintDesignException">Bunday dizayn yo'q yoki ta'rifi buzilgan.</exception>
    public static PrintDesign Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Cache.GetOrAdd(key.Trim(), static k =>
        {
            var resource = ResourcePrefix + k + ".json";
            var assembly = typeof(BuiltInPrintDesigns).GetTypeInfo().Assembly;

            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                throw new PrintDesignException(
                    string.Empty,
                    $"tayyor dizayn topilmadi: \"{k}\". Mavjudlari: {string.Join(", ", Keys)}.");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return PrintDesignLoader.Load(reader.ReadToEnd(), k);
        });
    }

    /// <summary>Barcha tayyor dizaynlar.</summary>
    public static IReadOnlyList<PrintDesign> All() => Keys.Select(Get).ToList();

    /// <summary>Qamrovga mos standart dizayn.</summary>
    /// <param name="scope">Qamrov.</param>
    public static PrintDesign ForScope(PrintScope scope) => scope switch
    {
        PrintScope.Teacher => Get(TeacherGreen),
        PrintScope.School => Get(SchoolCompact),
        _ => Get(ClassBlue),
    };
}
