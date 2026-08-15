using System.Runtime.CompilerServices;
using System.Text;

namespace DarsJadvali.Tests.Import;

/// <summary>
/// Test uchun aSc XML namunalarini <c>Import/Data</c> papkasidan o'qiydi.
/// </summary>
/// <remarks>
/// Fayllar ataylab <c>.csproj</c> ga qo'shilmagan (loyiha fayllariga tegilmasin):
/// avval kompilyatsiya paytidagi manba yo'li (<see cref="CallerFilePathAttribute"/>)
/// sinaladi, u topilmasa chiqish papkasidan yuqoriga qarab qidiriladi.
/// </remarks>
internal static class AscTestData
{
    /// <summary>Namuna XML matnini qaytaradi.</summary>
    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(DataDirectory(), fileName));

    /// <summary>Namuna XML oqimini qaytaradi.</summary>
    public static Stream Open(string fileName) =>
        new MemoryStream(Encoding.UTF8.GetBytes(Read(fileName)));

    /// <summary>Matndan oqim yasaydi (qo'lda yozilgan XML uchun).</summary>
    public static Stream Stream(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    private static string DataDirectory([CallerFilePath] string? callerPath = null)
    {
        var sourceDirectory = Path.GetDirectoryName(callerPath);
        if (sourceDirectory is not null)
        {
            var fromSource = Path.Combine(sourceDirectory, "Data");
            if (Directory.Exists(fromSource)) return fromSource;
        }

        // Zaxira yo'l: bin/Debug/net8.0 dan yuqoriga chiqib "Import/Data" ni qidirish.
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Import", "Data");
            if (Directory.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "aSc test namunalari papkasi topilmadi (tests/DarsJadvali.Tests/Import/Data).");
    }
}
