using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Import;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>
/// <see cref="IAscXmlImporter"/> ning EF Core ustidagi amalga oshirilishi.
/// </summary>
/// <remarks>
/// <para><b>Tranzaksiya.</b> Butun import <see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/>
/// ichida bajariladi — yarim yuklangan holat qolmaydi.</para>
/// <para><b>Oldindan ko'rish qanday ishlaydi.</b> Quruq rejim alohida "hisoblovchi"
/// kod emas: HAQIQIY import bajariladi, natija yig'iladi, so'ng maxsus signal-istisno
/// orqali tranzaksiya QAYTARILADI. Shu sababli oldindan ko'rish hisoboti bilan
/// haqiqiy import natijasi bir xil bo'lishi kafolatlanadi va ikkita mantiq
/// parallel ravishda eskirib ketmaydi.</para>
/// </remarks>
public sealed class AscXmlImporter : IAscXmlImporter
{
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICardOccurrenceProjector _projector;

    public AscXmlImporter(
        AppDbContext db,
        IUnitOfWork unitOfWork,
        ICardOccurrenceProjector projector)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _projector = projector;
    }

    /// <inheritdoc />
    public async Task<ImportPreview> PreviewAsync(
        Stream xml, ImportOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await ImportAsync(xml, options with { DryRun = true }, ct).ConfigureAwait(false);
        return new ImportPreview(result);
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportAsync(
        Stream xml, ImportOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(options);

        // Parse tranzaksiyadan TASHQARIDA: buzuq XML uchun baza umuman ochilmaydi.
        var document = AscXmlReader.Read(xml);

        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var session = new AscImportSession(_db, _projector, document, options);

                ImportResult result;
                try
                {
                    result = await session.RunAsync(token).ConfigureAwait(false);
                }
                catch (DbUpdateException ex)
                {
                    // Baza cheklovi buzildi — import butunlay qaytariladi, lekin
                    // chaqiruvchi tushunarli hisobot oladi (istisno emas).
                    throw new RollbackSignal(new ImportResult
                    {
                        Success = false,
                        DryRun = options.DryRun,
                        AcademicYearId = options.AcademicYearId,
                        Messages = new[]
                        {
                            new ImportMessage(ImportSeverity.Error, "ASC-DB-CONSTRAINT",
                                "Baza cheklovi buzilgani uchun import to'liq qaytarildi. " +
                                $"Tafsilot: {ex.GetBaseException().Message}")
                        }
                    });
                }

                // Quruq rejim yoki muvaffaqiyatsizlik — hech narsa yozilmasin.
                if (options.DryRun || !result.Success) throw new RollbackSignal(result);

                return result;
            }, ct).ConfigureAwait(false);
        }
        catch (RollbackSignal signal)
        {
            return signal.Result;
        }
    }

    /// <summary>
    /// Tranzaksiyani qaytarish uchun ishlatiladigan ichki signal. Natijani o'zi bilan
    /// olib chiqadi — shuning uchun "qaytarildi" holati ham to'liq hisobot beradi.
    /// </summary>
    private sealed class RollbackSignal : Exception
    {
        public RollbackSignal(ImportResult result) : base("Import qaytarildi.") => Result = result;

        public ImportResult Result { get; }
    }
}
