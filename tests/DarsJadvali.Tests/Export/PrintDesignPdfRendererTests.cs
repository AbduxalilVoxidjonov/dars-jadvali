using System.Text;
using System.Text.RegularExpressions;
using DarsJadvali.Infrastructure.Export;
using DarsJadvali.Infrastructure.Export.Printing;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Xunit;

namespace DarsJadvali.Tests.Export;

/// <summary>
/// Dizaynga asoslangan PDF chizuvchi: haqiqiy PDF baytlari yaratiladi (mock emas).
/// </summary>
public sealed class PrintDesignPdfRendererTests
{
    private static readonly PrintDesignPdfRenderer Renderer = new();

    // ------------------------------------------------------------------
    // Yordamchi
    // ------------------------------------------------------------------

    private static string Head(byte[] bytes) =>
        Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));

    private static int PageCount(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var match = Regex.Match(raw, @"/Count\s+(\d+)");
        Assert.True(match.Success, "PDF ichida /Count topilmadi.");
        return int.Parse(match.Groups[1].Value);
    }

    private static IReadOnlyList<PrintableDay> Days() => new[]
    {
        new PrintableDay(0, "Dushanba", "Du"),
        new PrintableDay(1, "Seshanba", "Se"),
        new PrintableDay(2, "Chorshanba", "Ch"),
        new PrintableDay(3, "Payshanba", "Pa"),
        new PrintableDay(4, "Juma", "Ju"),
    };

    /// <summary>Ikki smenali uzluksiz soatlar: 1..6 — 1-smena, 7..12 — 2-smena.</summary>
    private static IReadOnlyList<PrintablePeriod> TwoShiftPeriods() =>
        Enumerable.Range(1, 12)
            .Select(i => new PrintablePeriod(
                i,
                i.ToString(),
                $"{7 + i:00}:30-{8 + i:00}:15",
                i <= 6 ? "1-smena" : "2-smena"))
            .ToList();

    private static IReadOnlyList<PrintablePeriod> Periods(int count = 6) =>
        Enumerable.Range(1, count)
            .Select(i => new PrintablePeriod(i, i.ToString(), $"{7 + i:00}:30-{8 + i:00}:15"))
            .ToList();

    /// <summary>Kirill, o'zbek lotin (oʻ U+02BB, ʼ U+02BC), juft dars, guruh va A/B hafta bilan.</summary>
    private static PrintableTimetable Sample(int classCount = 1, PrintScope scope = PrintScope.Class)
    {
        var sections = new List<PrintableSection>(classCount);

        for (var c = 0; c < classCount; c++)
        {
            var cards = new List<PrintableCard>
            {
                new() { SubjectName = "Oʻzbek tili", DayIndex = 0, Period = 1, TeacherNames = new[] { "Gʻayratov Sanjar" }, RoomName = "101" },
                new() { SubjectName = "Русский язык", DayIndex = 0, Period = 2, TeacherNames = new[] { "Иванова Мария" }, RoomName = "102" },
                new() { SubjectName = "Mehnat taʼlimi", DayIndex = 1, Period = 1, Length = 2, TeacherNames = new[] { "Aʼzamov Bekzod" } },
                new() { SubjectName = "Ingliz tili", DayIndex = 2, Period = 3, GroupName = "1-guruh", TeacherNames = new[] { "Smith John" } },
                new() { SubjectName = "Nemis tili", DayIndex = 2, Period = 3, GroupName = "2-guruh", TeacherNames = new[] { "Weber Anna" } },
                new() { SubjectName = "Musiqa", DayIndex = 3, Period = 2, WeeksMask = PrintableCard.WeekA },
                new() { SubjectName = "Tasviriy sanʼat", DayIndex = 3, Period = 2, WeeksMask = PrintableCard.WeekB },
                new() { SubjectName = "Matematika", DayIndex = 4, Period = 5, TeacherNames = new[] { "Qodirov Aziz" }, RoomName = "205" },
            };

            sections.Add(new PrintableSection($"{5 + c}-A", "Xona: 101", cards));
        }

        return new PrintableTimetable
        {
            SchoolName = "12-sonli umumiy oʻrta taʼlim maktabi",
            AcademicYear = "2025/2026",
            Term = "1-chorak",
            Scope = scope,
            ScopeName = scope == PrintScope.Teacher ? "Gʻayratov Sanjar" : "5-A",
            Days = Days(),
            Periods = Periods(),
            Sections = sections,
            GeneratedAt = new DateTime(2026, 3, 14),
        };
    }

    // ------------------------------------------------------------------
    // Asosiy
    // ------------------------------------------------------------------

    [Fact]
    public void Natija_haqiqiy_pdf_va_bosh_emas()
    {
        var bytes = Renderer.Render(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue), Sample());

        Assert.Equal("%PDF-", Head(bytes));
        Assert.True(bytes.Length > 2000, $"PDF juda kichik: {bytes.Length} bayt");
        Assert.Contains("%%EOF", Encoding.Latin1.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(1, PageCount(bytes));
    }

    [Fact]
    public void Barcha_tayyor_dizaynlar_pdf_chiqaradi()
    {
        foreach (var key in BuiltInPrintDesigns.Keys)
        {
            var scope = BuiltInPrintDesigns.Get(key).Scope;
            var timetable = Sample(scope == PrintScope.School ? 6 : 1, scope);

            var bytes = Renderer.Render(BuiltInPrintDesigns.Get(key), timetable);

            Assert.Equal("%PDF-", Head(bytes));
            Assert.True(bytes.Length > 2000, $"{key}: PDF juda kichik ({bytes.Length} bayt)");
        }
    }

    [Fact]
    public void Kop_sahifali_chiqish_sahifalarga_bolinadi()
    {
        var design = BuiltInPrintDesigns.Get(BuiltInPrintDesigns.SchoolCompact);
        Assert.Equal(4, design.Grid!.SectionsPerPage);

        var timetable = Sample(classCount: 10, scope: PrintScope.School);

        Assert.Equal(3, PrintDesignPdfRenderer.CountPages(design, timetable));

        var bytes = Renderer.Render(design, timetable);
        Assert.Equal(3, PageCount(bytes));
    }

    [Fact]
    public void Bitta_sinf_bitta_sahifa()
    {
        var design = BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue);

        Assert.Equal(1, PrintDesignPdfRenderer.CountPages(design, Sample()));
        Assert.Equal(1, PageCount(Renderer.Render(design, Sample())));
    }

    [Fact]
    public void Bosh_jadval_istisno_bermaydi()
    {
        var timetable = new PrintableTimetable
        {
            SchoolName = "Maktab",
            Days = Days(),
            Periods = Periods(),
            Sections = new[] { new PrintableSection("5-A", null, Array.Empty<PrintableCard>()) },
        };

        var bytes = Renderer.Render(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue), timetable);

        Assert.Equal("%PDF-", Head(bytes));
        Assert.Equal(1, PageCount(bytes));
    }

    [Fact]
    public void Kunlar_va_soatlar_yoq_bolsa_ham_pdf_chiqadi()
    {
        var timetable = new PrintableTimetable { SchoolName = "Maktab" };

        var bytes = Renderer.Render(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue), timetable);

        Assert.Equal("%PDF-", Head(bytes));
    }

    [Fact]
    public void Ikki_smenali_uzluksiz_soatlar_chiziladi()
    {
        var timetable = Sample() with { Periods = TwoShiftPeriods() };

        // Soat raqamlari 1..12, ikkinchi smena 1 dan qayta boshlanmaydi.
        Assert.Equal(Enumerable.Range(1, 12), timetable.Periods.Select(p => p.Number));
        Assert.Equal(new[] { "1-smena", "2-smena" }, timetable.ShiftNames);

        var bytes = Renderer.Render(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue), timetable);

        Assert.Equal("%PDF-", Head(bytes));
        Assert.True(bytes.Length > 2000);
    }

    [Fact]
    public void Har_ikkala_oq_yonalishi_ishlaydi()
    {
        var timetable = Sample(4, PrintScope.School);

        foreach (var axis in new[] { PrintGridAxis.DaysAsColumns, PrintGridAxis.DaysAsRows })
        {
            var design = BuiltInPrintDesigns.Get(BuiltInPrintDesigns.SchoolCompact);
            var elements = design.Elements
                .Select(e => e is PrintTimetableElement g ? g with { Axis = axis } : e)
                .ToList();

            var bytes = Renderer.Render(design with { Elements = elements }, timetable);

            Assert.Equal("%PDF-", Head(bytes));
            Assert.True(bytes.Length > 2000, $"{axis}: PDF juda kichik");
        }
    }

    [Fact]
    public void Portret_va_landshaft_hajmi_farq_qiladi()
    {
        var design = BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue);
        var portrait = design with { Page = design.Page with { Orientation = PrintOrientation.Portrait } };

        var landscapeBytes = Renderer.Render(design, Sample());
        var portraitBytes = Renderer.Render(portrait, Sample());

        Assert.Equal("%PDF-", Head(portraitBytes));
        Assert.NotEqual(
            Encoding.Latin1.GetString(landscapeBytes),
            Encoding.Latin1.GetString(portraitBytes));
    }

    [Fact]
    public void Jadval_elementisiz_dizayn_ham_chiziladi()
    {
        var design = PrintDesignLoader.Load(
            "{ \"name\": \"Faqat matn\", \"elements\": [ { \"type\": \"text\", \"rect\": [0,0,1,0.2], \"text\": \"{School.Name}\" } ] }");

        var bytes = Renderer.Render(design, Sample());

        Assert.Equal("%PDF-", Head(bytes));
        Assert.Equal(1, PageCount(bytes));
    }

    [Fact]
    public void Bekor_qilish_kutilgan_istisno_beradi()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Renderer.Render(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue), Sample(), cts.Token));
    }

    // ------------------------------------------------------------------
    // Kirill / lotin — shrift
    // ------------------------------------------------------------------

    [Fact]
    public void Shrift_pdf_ichiga_ornatiladi()
    {
        var bytes = Renderer.Render(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue), Sample());
        var raw = Encoding.Latin1.GetString(bytes);

        // FontFile2 — TrueType shrift fayli PDF ichida. Ya'ni kirill/lotin
        // glifi hujjat bilan birga ketadi va boshqa kompyuterda ham chiqadi.
        Assert.Contains("FontFile2", raw, StringComparison.Ordinal);
        Assert.Contains("DejaVu", raw, StringComparison.Ordinal);
    }

    [Fact]
    public void Kirill_va_ozbek_lotin_belgilari_shriftda_bor()
    {
        EmbeddedFontResolver.EnsureInstalled();

        using var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var font = new XFont(EmbeddedFontResolver.FamilyName, 12, XFontStyleEx.Regular);

        // Kirill, o'zbek lotin (U+02BB, U+02BC) va lotin harflari.
        const string text = "Ўзбекистон Русский Oʻzbek gʻalaba taʼlim ABC";

        foreach (var ch in text.Where(c => !char.IsWhiteSpace(c)))
        {
            var width = gfx.MeasureString(ch.ToString(), font).Width;
            Assert.True(width > 0, $"'{ch}' (U+{(int)ch:X4}) shriftda topilmadi.");
        }

        Assert.True(gfx.MeasureString(text, font).Width > 0);
    }

    [Fact]
    public void Kirill_matnli_jadval_chiziladi()
    {
        var timetable = Sample() with
        {
            SchoolName = "Тошкент шаҳар 12-сонли мактаби",
            ScopeName = "5-А",
        };

        var bytes = Renderer.Render(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue), timetable);

        Assert.Equal("%PDF-", Head(bytes));
        Assert.True(bytes.Length > 2000);
    }
}
