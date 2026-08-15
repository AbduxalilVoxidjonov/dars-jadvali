using DarsJadvali.Infrastructure.Export.Printing;
using Xunit;

namespace DarsJadvali.Tests.Export;

/// <summary>
/// To'r joylashuvi: juft dars yaxlit blok, guruh darslari bir katakda,
/// A/B hafta, ko'p sahifali bo'linish.
/// </summary>
public sealed class TimetableGridLayoutTests
{
    private static IReadOnlyList<PrintableDay> Days(int count = 5) =>
        Enumerable.Range(0, count).Select(i => new PrintableDay(i, $"Kun{i + 1}")).ToList();

    private static IReadOnlyList<PrintablePeriod> Periods(int count = 6) =>
        Enumerable.Range(1, count).Select(i => new PrintablePeriod(i, i.ToString())).ToList();

    private static PrintableCard Card(
        string subject,
        int day,
        int period,
        int length = 1,
        string? group = null,
        int weeks = PrintableCard.AllWeeks) => new()
        {
            SubjectName = subject,
            DayIndex = day,
            Period = period,
            Length = length,
            GroupName = group,
            WeeksMask = weeks,
        };

    // ------------------------------------------------------------------
    // Juft dars
    // ------------------------------------------------------------------

    [Fact]
    public void Juft_dars_yaxlit_blok_boladi()
    {
        var section = new PrintableSection("5-A", null, new[] { Card("Mehnat", day: 1, period: 3, length: 2) });

        var layout = TimetableGridLayout.Build(section, Days(), Periods());

        var block = Assert.Single(layout.Blocks);
        Assert.Equal(2, block.RowSpan);          // BITTA blok, ikki qator
        Assert.True(block.IsDouble);
        Assert.Equal(2, block.RowIndex);         // 3-soat → 2-indeks
        Assert.Equal(3, block.LastRowIndex);
        Assert.Equal(1, block.LaneCount);
        Assert.Empty(layout.Dropped);
    }

    [Fact]
    public void Juft_dars_ikki_alohida_blokka_bolinmaydi()
    {
        var section = new PrintableSection("5-A", null, new[] { Card("Mehnat", 0, 1, length: 2) });

        var layout = TimetableGridLayout.Build(section, Days(), Periods());

        Assert.Single(layout.Blocks);
    }

    [Fact]
    public void Juft_dars_jadval_oxiridan_oshsa_qisqaradi()
    {
        // 6 soatli jadvalda 6-soatda boshlangan juft dars.
        var section = new PrintableSection("5-A", null, new[] { Card("Mehnat", 0, 6, length: 2) });

        var layout = TimetableGridLayout.Build(section, Days(), Periods(6));

        var block = Assert.Single(layout.Blocks);
        Assert.Equal(1, block.RowSpan);
        Assert.Equal(5, block.RowIndex);
    }

    [Fact]
    public void Juft_dars_keyingi_soatni_band_qiladi()
    {
        var section = new PrintableSection("5-A", null, new[]
        {
            Card("Mehnat", 0, 1, length: 2),
            Card("Jismoniy", 0, 2),          // bir vaqtda — boshqa yo'lakchaga tushadi
        });

        var layout = TimetableGridLayout.Build(section, Days(), Periods());

        var mehnat = layout.Blocks.Single(b => b.Card.SubjectName == "Mehnat");
        var jismoniy = layout.Blocks.Single(b => b.Card.SubjectName == "Jismoniy");

        Assert.NotEqual(mehnat.Lane, jismoniy.Lane);
        Assert.Equal(2, mehnat.LaneCount);   // kesishgani uchun katak ikkiga bo'lindi
    }

    // ------------------------------------------------------------------
    // Guruh darslari
    // ------------------------------------------------------------------

    [Fact]
    public void Ikki_guruh_bir_katakda_yonma_yon_chiqadi()
    {
        var section = new PrintableSection("5-A", null, new[]
        {
            Card("Ingliz tili", 2, 4, group: "1-guruh"),
            Card("Nemis tili", 2, 4, group: "2-guruh"),
        });

        var layout = TimetableGridLayout.Build(section, Days(), Periods());

        Assert.Equal(2, layout.Blocks.Count);
        Assert.All(layout.Blocks, b =>
        {
            Assert.Equal(2, b.LaneCount);       // katak ikkiga bo'lingan
            Assert.True(b.IsShared);
            Assert.Equal(2, b.DayIndex);
            Assert.Equal(3, b.RowIndex);
        });

        // Yo'lakchalar TAKRORLANMAYDI — ustma-ust tushmaydi.
        Assert.Equal(new[] { 0, 1 }, layout.Blocks.Select(b => b.Lane).OrderBy(l => l).ToArray());
    }

    [Fact]
    public void Uch_guruh_uch_yolakchaga_bolinadi()
    {
        var section = new PrintableSection("5-A", null, new[]
        {
            Card("Ingliz", 0, 1, group: "A"),
            Card("Nemis", 0, 1, group: "B"),
            Card("Fransuz", 0, 1, group: "C"),
        });

        var layout = TimetableGridLayout.Build(section, Days(), Periods());

        Assert.Equal(3, layout.Blocks.Count);
        Assert.All(layout.Blocks, b => Assert.Equal(3, b.LaneCount));
        Assert.Equal(new[] { 0, 1, 2 }, layout.Blocks.Select(b => b.Lane).OrderBy(l => l).ToArray());
    }

    [Fact]
    public void Guruh_darsi_juft_bolsa_ham_kesishmaydi()
    {
        var section = new PrintableSection("5-A", null, new[]
        {
            Card("Mehnat", 0, 1, length: 2, group: "Oʻgʻil bolalar"),
            Card("Texnologiya", 0, 1, length: 2, group: "Qizlar"),
        });

        var layout = TimetableGridLayout.Build(section, Days(), Periods());

        Assert.Equal(2, layout.Blocks.Count);
        Assert.All(layout.Blocks, b =>
        {
            Assert.Equal(2, b.RowSpan);
            Assert.Equal(2, b.LaneCount);
        });
        Assert.Equal(new[] { 0, 1 }, layout.Blocks.Select(b => b.Lane).OrderBy(l => l).ToArray());
    }

    [Fact]
    public void Guruhlar_tartibi_barqaror()
    {
        var forward = new PrintableSection("5-A", null, new[]
        {
            Card("Alfa", 0, 1, group: "1-guruh"),
            Card("Beta", 0, 1, group: "2-guruh"),
        });

        var backward = new PrintableSection("5-A", null, new[]
        {
            Card("Beta", 0, 1, group: "2-guruh"),
            Card("Alfa", 0, 1, group: "1-guruh"),
        });

        var a = TimetableGridLayout.Build(forward, Days(), Periods());
        var b = TimetableGridLayout.Build(backward, Days(), Periods());

        Assert.Equal(
            a.Blocks.Select(x => (x.Card.SubjectName, x.Lane)).OrderBy(x => x.Lane),
            b.Blocks.Select(x => (x.Card.SubjectName, x.Lane)).OrderBy(x => x.Lane));
    }

    // ------------------------------------------------------------------
    // A/B hafta
    // ------------------------------------------------------------------

    [Fact]
    public void Hafta_maskasi_belgiga_aylanadi()
    {
        Assert.Equal("A", Card("x", 0, 1, weeks: PrintableCard.WeekA).WeekLabel);
        Assert.Equal("B", Card("x", 0, 1, weeks: PrintableCard.WeekB).WeekLabel);
        Assert.Null(Card("x", 0, 1, weeks: PrintableCard.AllWeeks).WeekLabel);
        Assert.True(Card("x", 0, 1, weeks: PrintableCard.AllWeeks).IsEveryWeek);
        Assert.False(Card("x", 0, 1, weeks: PrintableCard.WeekA).IsEveryWeek);
    }

    [Fact]
    public void A_va_B_hafta_darslari_bir_katakda_yonma_yon()
    {
        var section = new PrintableSection("5-A", null, new[]
        {
            Card("Musiqa", 1, 2, weeks: PrintableCard.WeekA),
            Card("Tasviriy san'at", 1, 2, weeks: PrintableCard.WeekB),
        });

        var layout = TimetableGridLayout.Build(section, Days(), Periods());

        Assert.Equal(2, layout.Blocks.Count);
        Assert.All(layout.Blocks, b => Assert.Equal(2, b.LaneCount));
        Assert.Equal(
            new[] { "A", "B" },
            layout.Blocks.Select(b => b.Card.WeekLabel).OrderBy(x => x).ToArray());
    }

    // ------------------------------------------------------------------
    // Chegara holatlar
    // ------------------------------------------------------------------

    [Fact]
    public void Jadvalda_yoq_soat_tashlab_yuboriladi()
    {
        var section = new PrintableSection("5-A", null, new[]
        {
            Card("Bor", 0, 1),
            Card("Yoq", 0, 99),
            Card("Yoq kun", 42, 1),
        });

        var layout = TimetableGridLayout.Build(section, Days(), Periods(6));

        Assert.Single(layout.Blocks);
        Assert.Equal(2, layout.Dropped.Count);
    }

    [Fact]
    public void Bosh_tor_bosh_joylashuv_beradi()
    {
        var layout = TimetableGridLayout.Build(
            new PrintableSection("5-A", null, Array.Empty<PrintableCard>()), Days(), Periods());

        Assert.Empty(layout.Blocks);
    }

    [Fact]
    public void Kunlar_yoq_bolsa_hamma_karta_tashlanadi()
    {
        var section = new PrintableSection("5-A", null, new[] { Card("Bor", 0, 1) });

        var layout = TimetableGridLayout.Build(section, Array.Empty<PrintableDay>(), Periods());

        Assert.Empty(layout.Blocks);
        Assert.Single(layout.Dropped);
    }

    // ------------------------------------------------------------------
    // Ko'p sahifali bo'linish
    // ------------------------------------------------------------------

    [Fact]
    public void Sahifalarga_bolinish_togri_hisoblanadi()
    {
        var layouts = Enumerable.Range(0, 10)
            .Select(i => TimetableGridLayout.Build(
                new PrintableSection($"{i}-sinf", null, new[] { Card("Fan", 0, 1) }),
                Days(),
                Periods()))
            .ToList();

        var pages = TimetableGridLayout.Paginate(layouts, sectionsPerPage: 4);

        Assert.Equal(3, pages.Count);
        Assert.Equal(4, pages[0].Count);
        Assert.Equal(4, pages[1].Count);
        Assert.Equal(2, pages[2].Count);

        // Hech bir to'r yo'qolmasin va takrorlanmasin.
        Assert.Equal(
            layouts.Select(l => l.Section.Caption),
            pages.SelectMany(p => p).Select(l => l.Section.Caption));
    }

    [Fact]
    public void Bitta_tor_bitta_sahifa()
    {
        var layouts = new[]
        {
            TimetableGridLayout.Build(
                new PrintableSection("5-A", null, new[] { Card("Fan", 0, 1) }), Days(), Periods()),
        };

        Assert.Single(TimetableGridLayout.Paginate(layouts, 1));
    }

    [Fact]
    public void Torlar_yoq_bolsa_ham_bitta_sahifa_qoladi()
    {
        Assert.Single(TimetableGridLayout.Paginate(Array.Empty<TimetableLayout>(), 4));
    }
}
