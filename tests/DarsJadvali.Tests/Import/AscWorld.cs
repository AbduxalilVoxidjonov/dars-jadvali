using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Import;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Import.Xml;
using DarsJadvali.Infrastructure.Persistence;

namespace DarsJadvali.Tests.Import;

/// <summary>
/// Import testlari uchun tayyor "dunyo": izolyatsiyalangan baza, bitta o'quv yili va
/// qo'lda yig'ilgan <see cref="AscXmlImporter"/>.
/// </summary>
/// <remarks>
/// Importer ataylab DI dan olinmaydi: mavjud <c>TestDbFactory</c> ga tegmaslik uchun
/// u shu yerda qo'lda quriladi. Ikkala bog'liqlik ham <b>ayni</b> <see cref="AppDbContext"/>
/// nusxasidan foydalanadi — tranzaksiya umumiy bo'lishi shart.
/// </remarks>
internal sealed class AscWorld : IDisposable
{
    private readonly TestDbFactory _db;

    public AscWorld()
    {
        _db = new TestDbFactory();
        Context = _db.Context;

        Year = new AcademicYear
        {
            Name = "2025–2026",
            StartYear = 2025,
            DaysPerWeek = 6,
            WeeksInCycle = 1,
            TermsCount = 4
        };

        Context.AcademicYears.Add(Year);
        Context.SaveChanges();

        Projector = _db.Get<ICardOccurrenceProjector>();
        UnitOfWork = _db.Get<IUnitOfWork>();
        Importer = new AscXmlImporter(Context, UnitOfWork, Projector);
    }

    public AppDbContext Context { get; }

    /// <summary>Importer bilan AYNI kontekst ustidagi ish birligi.</summary>
    public IUnitOfWork UnitOfWork { get; }

    public AcademicYear Year { get; }

    public ICardOccurrenceProjector Projector { get; }

    public IAscXmlImporter Importer { get; }

    /// <summary>Standart parametrlar (birlashtirish rejimi, jonli yozuv).</summary>
    public ImportOptions Options(
        ImportMergeMode mode = ImportMergeMode.Merge,
        bool dryRun = false,
        bool importCards = true) => new()
    {
        AcademicYearId = Year.Id,
        MergeMode = mode,
        DryRun = dryRun,
        ImportCards = importCards
    };

    /// <summary>Namuna faylni import qiladi.</summary>
    public async Task<ImportResult> ImportFileAsync(string fileName, ImportOptions? options = null)
    {
        await using var stream = AscTestData.Open(fileName);
        return await Importer.ImportAsync(stream, options ?? Options());
    }

    /// <summary>Namuna faylni oldindan ko'radi.</summary>
    public async Task<ImportPreview> PreviewFileAsync(string fileName, ImportOptions? options = null)
    {
        await using var stream = AscTestData.Open(fileName);
        return await Importer.PreviewAsync(stream, options ?? Options());
    }

    /// <summary>Kuzatuv keshini tozalaydi — bazadan qayta o'qish uchun.</summary>
    public void Detach() => Context.ChangeTracker.Clear();

    public void Dispose() => _db.Dispose();
}
