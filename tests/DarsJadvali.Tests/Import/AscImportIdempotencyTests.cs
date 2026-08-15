using DarsJadvali.Application.Import;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Import;

/// <summary>
/// Qayta import: <c>ExternalId</c> tufayli dublikat yaratilmasligi shart.
/// </summary>
public class AscImportIdempotencyTests
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
        world.Context.LessonTeachers.Count(),
        world.Context.LessonGroups.Count(),
        world.Context.LessonClasses.Count(),
        world.Context.LessonClassrooms.Count(),
        world.Context.CardClassrooms.Count(),
        world.Context.CardOccurrences.Count()
    };

    [Fact]
    public async Task Ikkinchi_import_dublikat_yaratmaydi()
    {
        using var world = new AscWorld();

        var first = await world.ImportFileAsync("school-small.xml");
        Assert.True(first.Success, first.ToReport());

        world.Detach();
        var afterFirst = Snapshot(world);

        var second = await world.ImportFileAsync("school-small.xml");
        Assert.True(second.Success, second.ToReport());

        world.Detach();
        var afterSecond = Snapshot(world);

        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task Ikkinchi_importda_hech_narsa_yaratilmaydi_faqat_yangilanadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var second = await world.ImportFileAsync("school-small.xml");

        foreach (var kind in new[]
                 {
                     ImportEntityKind.Period, ImportEntityKind.Term, ImportEntityKind.Schedule,
                     ImportEntityKind.Grade, ImportEntityKind.Subject, ImportEntityKind.Teacher,
                     ImportEntityKind.Classroom, ImportEntityKind.SchoolClass,
                     ImportEntityKind.ClassDivision, ImportEntityKind.StudentGroup,
                     ImportEntityKind.Lesson
                 })
        {
            var stat = second.Stats.Single(s => s.Kind == kind);
            Assert.True(stat.Created == 0, $"{stat.Title}: {stat.Created} ta yangi yozuv yaratildi.");
        }

        // Kartochkalar har importda qayta yoziladi (ko'chirilgan kartochka qolib
        // ketmasligi uchun) — shuning uchun ular "yaratildi" deb sanaladi, lekin
        // umumiy soni o'zgarmaydi.
        var cards = second.Stats.Single(s => s.Kind == ImportEntityKind.Card);
        Assert.Equal(13, cards.Created);
        Assert.Equal(13, world.Context.Cards.Count());
    }

    [Fact]
    public async Task Qisqartmalar_ikkinchi_importda_oʻzgarmaydi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var before = world.Context.Subjects
            .Where(s => s.ExternalId != null)
            .ToDictionary(s => s.ExternalId!, s => (s.Code, s.ShortName));

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        var after = world.Context.Subjects
            .Where(s => s.ExternalId != null)
            .ToDictionary(s => s.ExternalId!, s => (s.Code, s.ShortName));

        Assert.Equal(before, after);
        Assert.Equal("Mat", after["SMAT"].Code);
    }

    [Fact]
    public async Task ExternalId_barcha_entitylarda_saqlanadi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        Assert.All(world.Context.Subjects.ToList(), s => Assert.False(string.IsNullOrEmpty(s.ExternalId)));
        Assert.All(world.Context.Teachers.ToList(), t => Assert.False(string.IsNullOrEmpty(t.ExternalId)));
        Assert.All(world.Context.Classrooms.ToList(), c => Assert.False(string.IsNullOrEmpty(c.ExternalId)));
        Assert.All(world.Context.SchoolClasses.ToList(), c => Assert.False(string.IsNullOrEmpty(c.ExternalId)));
        Assert.All(world.Context.StudentGroups.ToList(), g => Assert.False(string.IsNullOrEmpty(g.ExternalId)));
        Assert.All(world.Context.Lessons.ToList(), l => Assert.False(string.IsNullOrEmpty(l.ExternalId)));
    }

    [Fact]
    public async Task Koʻchirilgan_kartochka_eski_oʻrnida_qolmaydi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        // L5 ni chorshanbaga (00100) ko'chiramiz.
        var moved = AscTestData.Read("school-small.xml")
            .Replace(
                "<card lessonid=\"L5\" period=\"5\" days=\"00010\"",
                "<card lessonid=\"L5\" period=\"5\" days=\"00100\"",
                StringComparison.Ordinal);

        await using var stream = AscTestData.Stream(moved);
        var result = await world.Importer.ImportAsync(stream, world.Options());
        Assert.True(result.Success, result.ToReport());

        world.Detach();

        var lesson = world.Context.Lessons.Single(l => l.ExternalId == "L5");
        var card = Assert.Single(world.Context.Cards.Where(c => c.LessonId == lesson.Id));
        Assert.Equal(2, card.DayNo);
        Assert.Equal(13, world.Context.Cards.Count());
    }

    [Fact]
    public async Task Almashtirish_rejimi_eski_darslarni_tozalaydi()
    {
        using var world = new AscWorld();

        await world.ImportFileAsync("school-small.xml");
        world.Detach();

        // Faqat bitta darsli qisqartirilgan variant.
        var trimmed = AscTestData.Read("school-small.xml");
        foreach (var id in new[] { "L2", "L3", "L4", "L5", "L6" })
        {
            trimmed = RemoveLines(trimmed, $"lessonid=\"{id}\"");
            trimmed = RemoveLines(trimmed, $"<lesson id=\"{id}\"");
        }

        await using var stream = AscTestData.Stream(trimmed);
        var result = await world.Importer.ImportAsync(
            stream, world.Options(ImportMergeMode.Replace));

        Assert.True(result.Success, result.ToReport());
        Assert.Contains(result.Messages, m => m.Code == "ASC-REPLACE");

        world.Detach();

        Assert.Equal(1, world.Context.Lessons.Count());
        Assert.Equal(4, world.Context.Cards.Count());   // L1: 2 kartochka × 2 chorak

        // Ma'lumotnomalar Replace rejimida ham saqlanadi.
        Assert.Equal(3, world.Context.Subjects.Count());
        Assert.Equal(10, world.Context.StudentGroups.Count());
    }

    private static string RemoveLines(string text, string marker)
    {
        var lines = text.Split('\n');
        return string.Join('\n', lines.Where(l => !l.Contains(marker, StringComparison.Ordinal)));
    }
}
