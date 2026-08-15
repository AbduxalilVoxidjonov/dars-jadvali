using System.Text;

namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>
/// Eski (bir baytli) kod sahifalarini .NET ga tanitadi — <c>windows-1251</c> kabi
/// kirill kodirovkalarida yozilgan aSc eksportlarini o'qish uchun.
/// </summary>
/// <remarks>
/// <para><b>Nega alohida paket kerak.</b> .NET Framework'da barcha kod sahifalari
/// ichida bo'lgan; .NET Core / .NET 8 esa faqat Unicode va ASCII bilan keladi.
/// Shu sababli <c>Encoding.GetEncoding("windows-1251")</c> standart holatda
/// <c>ArgumentException</c> beradi va <see cref="System.Xml.XmlReader"/> XML
/// deklaratsiyasidagi <c>encoding="windows-1251"</c> ni o'qiy olmaydi.
/// <c>System.Text.Encoding.CodePages</c> paketi (Microsoft, MIT) aynan shu
/// kod sahifalarini qaytaradi — boshqa maqsadda ishlatilmaydi.</para>
/// <para><b>Nega bu yerda.</b> aSc XML dasturga faqat
/// <see cref="AscXmlReader.Read"/> orqali kiradi, shuning uchun ro'yxatdan o'tkazish
/// aynan shu nuqtada bajariladi: DI sozlamasiga ham, dastur boshlanish nuqtasiga ham
/// bog'liq emas (konsol, Desktop, Web va sinovlarda bir xil ishlaydi).</para>
/// <para><b>Diqqat.</b> <c>windows-1251</c> — rus/bolgar/serb kirilligi uchun kod
/// sahifasi. O'zbek kirilligining <c>Ҳ</c> (U+04B3), <c>Қ</c> (U+049A),
/// <c>Ғ</c> (U+0492) harflari unga umuman sig'maydi — aSc bunday faylni eksport
/// qilganda o'sha harflar manbada allaqachon <c>?</c> ga aylangan bo'ladi.
/// To'liq o'zbek kirilligi faqat UTF-8 eksportida saqlanadi.</para>
/// </remarks>
public static class LegacyEncodings
{
    /// <summary>0 — hali ro'yxatdan o'tkazilmagan, 1 — o'tkazilgan.</summary>
    private static int _registered;

    /// <summary>
    /// Kod sahifalari provayderini bir marta ro'yxatdan o'tkazadi (takroriy chaqiruv bekor).
    /// </summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
