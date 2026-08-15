using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Import;
using DarsJadvali.Infrastructure.Import.Xml;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Import;

/// <summary>
/// Oldindan ko'rish (dry run) va tranzaksiya butunligi.
/// </summary>
public class AscImportTransactionTests
{
    private static int[] Snapshot(AscWorld world) => new[]
    {
        world.Context.Periods.Count(),
        world.Context.Terms.Count(),
        world.Context.Schedules.Count(),
        world.Context.Grades.Count(),
        world.Context.Subjects.Count(),
        world.Context.Teachers.Count(),
        world.Context.Classrooms.Count(),
        world.Context.SchoolClasses.Count(),
        world.Context.ClassDivisions.Count(),
        world.Context.StudentGroups.Count(),
        world.Context.Lessons.Count(),
        world.Context.Cards.Count(),
        world.Context.CardOccurrences.Count()
    };

    [Fact]
    public async Task Oldindan_koʻrish_bazani_oʻzgartirmaydi()
    {
        using var world = new AscWorld();

        var before = Snapshot(world);
        Assert.Equal(new int[before.Length], before);

        var preview = await world.PreviewFileAsync("school-small.xml");

        Assert.True(preview.IsValid, preview.ToReport());
        Assert.True(preview.Result.DryRun);

        world.Detach();
        Assert.Equal(before, Snapshot(world));

        // O'quv yilining o'lchovlari ham qaytariladi.
        Assert.Equal(1, world.Context.AcademicYears.Single(y => y.Id == world.Year.Id).WeeksInCycle);
    }

    [Fact]
    public async Task Oldindan_koʻrish_haqiqiy_import_bilan_bir_xil_hisobot_beradi()
    {
        using var world = new AscWorld();

        var preview = await world.PreviewFileAsync("school-small.xml");
        world.Detach();

        var actual = await world.ImportFileAsync("school-small.xml");

        var previewStats = preview.Stats
            .Where(s => s.HasAny)
            .Select(s => (s.Kind, s.Found, s.Created, s.Updated, s.Skipped))
            .ToList();

        var actualStats = actual.Stats
            .Where(s => s.HasAny)
            .Select(s => (s.Kind, s.Found, s.Created, s.Updated, s.Skipped))
            .ToList();

        Assert.Equal(previewStats, actualStats);
        Assert.Equal(
            preview.Messages.Select(m => m.Code).OrderBy(c => c, StringComparer.Ordinal),
            actual.Messages.Select(m => m.Code).OrderBy(c => c, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Oldindan_koʻrish_mavjud_maʼlumotni_hisobga_oladi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var preview = await world.PreviewFileAsync("school-small.xml");

        var subjects = preview.Stats.Single(s => s.Kind == ImportEntityKind.Subject);
        Assert.Equal(0, subjects.Created);
        Assert.Equal(3, subjects.Updated);
    }

    [Fact]
    public async Task Import_oʻrtasidagi_xato_hammasini_qaytaradi()
    {
        using var world = new AscWorld();

        // Bandlik projektori portlaydi — bu importning ENG OXIRGI qadami, ya'ni
        // shu paytga qadar hamma narsa allaqachon yozilgan bo'ladi.
        var importer = new AscXmlImporter(
            world.Context, world.UnitOfWork, new ExplodingProjector());

        await using var stream = AscTestData.Open("school-small.xml");
        var result = await importer.ImportAsync(stream, world.Options());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, m => m.Code == "ASC-DB-CONSTRAINT");

        world.Detach();

        // Yarim import qolmadi.
        Assert.Equal(new int[13], Snapshot(world));
    }

    /// <summary>Tranzaksiya qaytishini sinash uchun ataylab portlaydigan projektor.</summary>
    private sealed class ExplodingProjector : ICardOccurrenceProjector
    {
        public Task<int> RebuildForCardAsync(int cardId, CancellationToken ct = default) => Explode();

        public Task<int> RebuildForCardsAsync(IReadOnlyList<int> cardIds, CancellationToken ct = default)
            => Explode();

        public Task<int> RebuildForScheduleAsync(int scheduleId, CancellationToken ct = default)
            => Explode();

        private static Task<int> Explode() =>
            throw new DbUpdateException("Sinov uchun sun'iy baza xatosi.");
    }
}
