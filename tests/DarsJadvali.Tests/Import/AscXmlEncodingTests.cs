using System.Text;
using DarsJadvali.Infrastructure.Import.Xml;
using Xunit;

namespace DarsJadvali.Tests.Import;

/// <summary>
/// aSc eksporti kirill kodirovkalarida ham o'qilishini tekshiradi.
/// </summary>
/// <remarks>
/// <para>Foydalanuvchi maktabi ma'lumoti kirill yozuvida; aSc ba'zi tillarda
/// <c>encoding="windows-1251"</c> bilan eksport qiladi. .NET 8 bu kod sahifasini
/// o'zi bilmaydi — uni <see cref="LegacyEncodings"/> ulaydi.</para>
/// <para><b>Nega baytlar qo'lda yasaladi.</b> Agar sinov <c>Encoding.GetEncoding(1251)</c>
/// dan foydalansa, u ham o'sha provayderga bog'liq bo'lib qolardi va provayder olib
/// tashlanganini <b>sezmasdan</b> o'tib ketishi mumkin edi. Shu sababli cp1251 baytlari
/// sinov ichida qo'lda quriladi: endi sinovni faqat <see cref="AscXmlReader"/> ning o'zi
/// provayderni ro'yxatdan o'tkazsagina o'tadi.</para>
/// </remarks>
public sealed class AscXmlEncodingTests
{
    private const string SchoolName = "Зиё интелект";
    private const string TeacherRu = "Иванова Мария";

    /// <summary>O'zbek kirilligi: <c>ҳ</c> — U+04B3.</summary>
    private const string TeacherUz = "Раҳимов Шуҳрат";

    /// <summary>Bitta o'qituvchi va bitta fandan iborat eng kichik aSc hujjati.</summary>
    private static string Xml(string encodingName, string schoolName, string teacherName) =>
        $"""
         <?xml version="1.0" encoding="{encodingName}"?>
         <timetable displayname="{schoolName}">
           <teachers>
             <teacher id="T1" name="{teacherName}" short="{teacherName[..1]}" />
           </teachers>
           <subjects>
             <subject id="S1" name="Математика" short="Мат" />
           </subjects>
         </timetable>
         """;

    /// <summary>
    /// Matnni <c>windows-1251</c> baytlariga o'giradi — .NET kodirovkalariga
    /// tayanmasdan, jadval bo'yicha.
    /// </summary>
    /// <remarks>
    /// cp1251 da kirill bloki uzluksiz: U+0410..U+044F → 0xC0..0xFF; <c>Ё</c>/<c>ё</c>
    /// alohida (0xA8 / 0xB8). Sinovlarda ishlatilgan barcha harflar shu doirada.
    /// </remarks>
    private static byte[] Cp1251(string text)
    {
        var bytes = new byte[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            bytes[i] = ch switch
            {
                < (char)0x80 => (byte)ch,
                'Ё' => 0xA8,
                'ё' => 0xB8,
                >= 'А' and <= 'я' => (byte)(ch - 'А' + 0xC0),
                _ => throw new InvalidOperationException(
                    $"'{ch}' (U+{(int)ch:X4}) windows-1251 ga sig'maydi — sinov matnini o'zgartiring.")
            };
        }

        return bytes;
    }

    [Fact]
    public void Windows1251_kirill_fayli_toliq_oqiladi()
    {
        using var stream = new MemoryStream(Cp1251(Xml("windows-1251", SchoolName, TeacherRu)));

        var doc = AscXmlReader.Read(stream);

        Assert.Equal(SchoolName, doc.DisplayName);
        var teacher = Assert.Single(doc.Teachers);
        Assert.Equal(TeacherRu, teacher.Name);
        Assert.Equal("Математика", Assert.Single(doc.Subjects).Name);
    }

    [Fact]
    public async Task Windows1251_fayli_bazaga_import_qilinadi()
    {
        using var world = new AscWorld();
        await using var stream = new MemoryStream(Cp1251(Xml("windows-1251", SchoolName, TeacherRu)));

        var result = await world.Importer.ImportAsync(stream, world.Options());

        Assert.True(result.Success, result.ToReport());
        world.Detach();

        Assert.Equal(TeacherRu, Assert.Single(world.Context.Teachers.ToList()).FullName);
    }

    /// <summary>
    /// <c>windows-1251</c> — rus/bolgar/serb kirilligi uchun kod sahifasi. O'zbek
    /// kirilligining <c>ҳ</c> (U+04B3), <c>қ</c> (U+049B), <c>ғ</c> (U+0493) harflari
    /// unga UMUMAN sig'maydi: aSc bunday faylni eksport qilganda o'sha harflar
    /// manbaning o'zida <c>?</c> ga aylanib bo'lgan bo'ladi. Shu sababli to'liq o'zbek
    /// kirilligi faqat UTF-8 da sinaladi — bu cheklov dasturning kamchiligi emas.
    /// </summary>
    [Fact]
    public void Ozbek_kirill_harflari_windows1251_ga_sigmaydi()
    {
        LegacyEncodings.EnsureRegistered();

        var cp1251 = Encoding.GetEncoding(1251);
        var roundTrip = cp1251.GetString(cp1251.GetBytes(TeacherUz));

        Assert.NotEqual(TeacherUz, roundTrip);
        Assert.Equal("Ра?имов Шу?рат", roundTrip);
    }

    [Fact]
    public void Utf8_da_ozbek_kirill_harflari_saqlanadi()
    {
        var bytes = new UTF8Encoding(false).GetBytes(Xml("utf-8", SchoolName, TeacherUz));
        using var stream = new MemoryStream(bytes);

        var doc = AscXmlReader.Read(stream);

        var teacher = Assert.Single(doc.Teachers);
        Assert.Equal(TeacherUz, teacher.Name);
        Assert.Contains('ҳ', teacher.Name);
        Assert.Equal(SchoolName, doc.DisplayName);
    }

    [Fact]
    public void Utf8_BOM_bilan_ham_oqiladi()
    {
        var bytes = new UTF8Encoding(true).GetBytes(Xml("utf-8", SchoolName, TeacherUz));
        using var stream = new MemoryStream(bytes);

        var doc = AscXmlReader.Read(stream);

        Assert.Equal(TeacherUz, Assert.Single(doc.Teachers).Name);
        Assert.Equal(SchoolName, doc.DisplayName);
    }

    [Fact]
    public void Kodirovka_deklaratsiyasiz_utf8_ham_oqiladi()
    {
        var xml = $"""
                   <timetable displayname="{SchoolName}">
                     <teachers><teacher id="T1" name="{TeacherUz}" /></teachers>
                   </timetable>
                   """;

        using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(xml));

        var doc = AscXmlReader.Read(stream);

        Assert.Equal(TeacherUz, Assert.Single(doc.Teachers).Name);
    }
}
