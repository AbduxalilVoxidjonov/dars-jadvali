using DarsJadvali.Infrastructure.Export.Printing;
using Xunit;

namespace DarsJadvali.Tests.Export;

/// <summary>Bog'lash tokenlari (<c>{Class.Name}</c> ...) almashishi.</summary>
public sealed class PrintTokenResolverTests
{
    private static PrintableTimetable Sample() => new()
    {
        SchoolName = "42-umumta'lim maktabi",
        AcademicYear = "2025/2026",
        Term = "1-chorak",
        Scope = PrintScope.Class,
        ScopeName = "5-A",
        GeneratedAt = new DateTime(2026, 3, 14, 9, 5, 0),
        Days = new[] { new PrintableDay(0, "Dushanba") },
        Periods = new[] { new PrintablePeriod(1, "1") },
        Sections = new[]
        {
            new PrintableSection("5-A", "Xona: 101", Array.Empty<PrintableCard>()),
            new PrintableSection("5-B", null, Array.Empty<PrintableCard>()),
        },
    };

    [Fact]
    public void Mavjud_tokenlar_qiymatga_almashadi()
    {
        var resolver = new PrintTokenResolver(Sample());

        Assert.Equal("42-umumta'lim maktabi", resolver.Resolve("{School.Name}"));
        Assert.Equal("2025/2026", resolver.Resolve("{AcademicYear}"));
        Assert.Equal("1-chorak", resolver.Resolve("{Term}"));
        Assert.Equal("5-A", resolver.Resolve("{Class.Name}"));
        Assert.Equal("5-A", resolver.Resolve("{Scope.Name}"));
        Assert.Equal("14.03.2026", resolver.Resolve("{Date}"));
        Assert.Equal("09:05", resolver.Resolve("{Time}"));
        Assert.Equal("Sinf", resolver.Resolve("{Scope.Kind}"));
        Assert.Equal("5-A sinf dars jadvali", resolver.Resolve("{Scope.Title}"));
        Assert.Empty(resolver.UnknownTokens);
    }

    [Fact]
    public void Bir_nechta_token_va_oddiy_matn_aralashadi()
    {
        var resolver = new PrintTokenResolver(Sample());

        Assert.Equal(
            "42-umumta'lim maktabi — 5-A (2025/2026)",
            resolver.Resolve("{School.Name} — {Class.Name} ({AcademicYear})"));
    }

    [Fact]
    public void Yetishmayotgan_token_bosh_qoladi_va_royxatga_tushadi()
    {
        var resolver = new PrintTokenResolver(Sample());

        var result = resolver.Resolve("Sinf rahbari: {Class.Rahbar}");

        // Token yo'qolgani UCHUN osilib qolgan ":" ham tozalanadi.
        Assert.Equal("Sinf rahbari", result);
        Assert.Contains("Class.Rahbar", resolver.UnknownTokens);
    }

    [Fact]
    public void Mavjud_lekin_qiymati_yoq_token_bosh_matn_beradi()
    {
        var timetable = Sample() with { Term = null };
        var resolver = new PrintTokenResolver(timetable);

        Assert.Equal("2025/2026", resolver.Resolve("{AcademicYear} · {Term}"));
        Assert.Empty(resolver.UnknownTokens);   // token MA'LUM, faqat qiymati yo'q
    }

    [Fact]
    public void Boshqa_qamrovning_tokeni_bosh_boladi()
    {
        var resolver = new PrintTokenResolver(Sample());

        // Sinf jadvalida {Teacher.Name} ma'lum token, lekin qiymati yo'q.
        Assert.Equal(string.Empty, resolver.Resolve("{Teacher.Name}"));
        Assert.Empty(resolver.UnknownTokens);
    }

    [Fact]
    public void Sahifa_konteksti_yangilanadi()
    {
        var timetable = Sample();
        var resolver = new PrintTokenResolver(timetable);

        Assert.Equal("1 / 1", resolver.Resolve("{Page} / {PageCount}"));

        resolver.SetPageContext(2, 7, timetable.Sections[1]);

        Assert.Equal("2 / 7", resolver.Resolve("{Page} / {PageCount}"));
        Assert.Equal("5-B", resolver.Resolve("{Section.Caption}"));
    }

    [Fact]
    public void Qochirilgan_qavs_matn_boladi()
    {
        var resolver = new PrintTokenResolver(Sample());

        Assert.Equal("{Class.Name}", resolver.Resolve("{{Class.Name}}"));
        Assert.Empty(resolver.UnknownTokens);
    }

    [Fact]
    public void Yopilmagan_qavs_matnni_yomotmaydi()
    {
        var resolver = new PrintTokenResolver(Sample());

        Assert.Equal("5-A {Term", resolver.Resolve("{Class.Name} {Term"));
    }

    [Fact]
    public void Qoshimcha_token_qoyish_mumkin()
    {
        var resolver = new PrintTokenResolver(Sample());
        resolver.Set("Direktor", "Karimova Nodira");

        Assert.Equal("Karimova Nodira", resolver.Resolve("{Direktor}"));
    }

    [Fact]
    public void Barcha_malum_tokenlar_haqiqatan_ishlaydi()
    {
        var resolver = new PrintTokenResolver(Sample());

        foreach (var token in PrintTokenResolver.KnownTokens)
        {
            resolver.Resolve("{" + token + "}");
            Assert.DoesNotContain(token, resolver.UnknownTokens);
        }
    }

    [Fact]
    public void Kirill_va_lotin_matn_ozgarmaydi()
    {
        var timetable = Sample() with { SchoolName = "Ўзбекистон — Oʻzbekiston maktabi" };
        var resolver = new PrintTokenResolver(timetable);

        Assert.Equal("Ўзбекистон — Oʻzbekiston maktabi", resolver.Resolve("{School.Name}"));
    }
}
