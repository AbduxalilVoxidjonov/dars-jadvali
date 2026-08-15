namespace DarsJadvali.Application.Import;

/// <summary>
/// aSc TimeTables XML eksportini (<c>asctt2012</c> va <c>asctt2008</c> sxemalari)
/// dasturning ma'lumotlar modeliga yuklaydi.
/// </summary>
/// <remarks>
/// <para><b>Kafolatlar.</b></para>
/// <list type="number">
/// <item><b>Atomik.</b> Butun import bitta tranzaksiya ichida bajariladi —
/// yarim yuklangan holat qolmaydi.</item>
/// <item><b>Idempotent.</b> aSc <c>id</c> qiymatlari <c>ExternalId</c> ga yoziladi;
/// ikkinchi marta import qilinganda dublikat yaratilmaydi, mavjudi yangilanadi.</item>
/// <item><b>Xatoga chidamli.</b> Bo'sh maydon, noma'lum havola yoki <c>-1</c> qiymat
/// importni yiqitmaydi — yozuv o'tkazib yuboriladi va ogohlantirishga tushadi.</item>
/// <item><b>Oldindan ko'rish aniq.</b> <see cref="PreviewAsync"/> haqiqiy importni
/// bajarib, oxirida uni qaytaradi (rollback). Shu sababli oldindan ko'rish hisoboti
/// bilan haqiqiy import natijasi mos keladi.</item>
/// </list>
/// </remarks>
public interface IAscXmlImporter
{
    /// <summary>
    /// Bazaga yozmasdan importni "quruq" bajaradi va nima bo'lishini hisobot qiladi.
    /// </summary>
    /// <param name="xml">aSc XML oqimi.</param>
    /// <param name="options">
    /// Parametrlar. <see cref="ImportOptions.DryRun"/> qiymati e'tiborga olinmaydi —
    /// bu metod har doim quruq ishlaydi. Maqsad <see cref="ImportOptions.AcademicYearId"/>
    /// baribir kerak: "yangi" va "yangilanadi" farqi aynan mavjud ma'lumotga bog'liq.
    /// </param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    /// <exception cref="AscImportException">XML o'qib bo'lmasa yoki buzuq bo'lsa.</exception>
    Task<ImportPreview> PreviewAsync(
        Stream xml, ImportOptions options, CancellationToken ct = default);

    /// <summary>
    /// XML'ni bazaga yuklaydi.
    /// </summary>
    /// <param name="xml">aSc XML oqimi.</param>
    /// <param name="options">Parametrlar.</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    /// <exception cref="AscImportException">XML o'qib bo'lmasa yoki buzuq bo'lsa.</exception>
    Task<ImportResult> ImportAsync(
        Stream xml, ImportOptions options, CancellationToken ct = default);
}

/// <summary>
/// aSc XML'ni umuman o'qib bo'lmaganda (buzuq XML, noto'g'ri ildiz elementi,
/// qo'llab-quvvatlanmaydigan kodirovka) tashlanadigan istisno.
/// </summary>
/// <remarks>
/// Bu <b>yagona</b> holat: qolgan barcha muammolar (noma'lum id, bo'sh maydon,
/// <c>-1</c> qiymat) istisno emas, <see cref="ImportMessage"/> ogohlantirishi bo'ladi.
/// </remarks>
public sealed class AscImportException : Exception
{
    /// <summary>Yangi istisno.</summary>
    public AscImportException(string message) : base(message)
    {
    }

    /// <summary>Ichki sabab bilan.</summary>
    public AscImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
