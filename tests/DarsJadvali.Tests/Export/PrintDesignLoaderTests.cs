using DarsJadvali.Infrastructure.Export.Printing;
using Xunit;

namespace DarsJadvali.Tests.Export;

/// <summary>
/// Dizayn ta'rifi (JSON) o'qilishi va NOTO'G'RI ta'rif uchun tushunarli xato berishi.
/// </summary>
public sealed class PrintDesignLoaderTests
{
    private const string MinimalJson = """
    {
      "name": "Sinov",
      "scope": "class",
      "page": { "size": "A4", "orientation": "landscape", "marginMm": 10 },
      "theme": { "accent": "#112233" },
      "elements": [
        { "type": "text", "rect": [0, 0, 1, 0.1], "text": "{School.Name}", "fontRatio": 0.03, "bold": true, "align": "center" },
        { "type": "timetable", "rect": [0, 0.15, 1, 0.9], "axis": "days-as-columns", "sectionsPerPage": 3 },
        { "type": "legend", "rect": [0, 0.92, 1, 1.0], "legend": "teachers", "columns": 2 },
        { "type": "line", "rect": [0, 0.12, 1, 0.121], "thickness": 1.5, "color": "#FF0000" }
      ]
    }
    """;

    [Fact]
    public void To_gri_tarif_toliq_oqiladi()
    {
        var design = PrintDesignLoader.Load(MinimalJson, "sinov");

        Assert.Equal("sinov", design.Key);
        Assert.Equal("Sinov", design.Name);
        Assert.Equal(PrintScope.Class, design.Scope);
        Assert.Equal(PrintPageSize.A4, design.Page.Size);
        Assert.Equal(PrintOrientation.Landscape, design.Page.Orientation);
        Assert.Equal(10, design.Page.MarginMm);
        Assert.Equal("#112233", design.Theme.Accent);
        Assert.Equal(4, design.Elements.Count);

        var text = Assert.IsType<PrintTextElement>(design.Elements[0]);
        Assert.Equal("{School.Name}", text.Text);
        Assert.True(text.Bold);
        Assert.Equal(PrintAlign.Center, text.Align);
        Assert.Equal(0.03, text.FontRatio, 6);
        Assert.Equal(new PrintRect(0, 0, 1, 0.1), text.Rect);

        var grid = Assert.IsType<PrintTimetableElement>(design.Elements[1]);
        Assert.Equal(PrintGridAxis.DaysAsColumns, grid.Axis);
        Assert.Equal(3, grid.SectionsPerPage);
        Assert.Same(grid, design.Grid);

        var legend = Assert.IsType<PrintLegendElement>(design.Elements[2]);
        Assert.Equal(PrintLegendKind.Teachers, legend.Legend);
        Assert.Equal(2, legend.Columns);

        var line = Assert.IsType<PrintLineElement>(design.Elements[3]);
        Assert.Equal(1.5, line.Thickness, 6);
        Assert.Equal("#FF0000", line.Color);
    }

    [Fact]
    public void Barcha_tayyor_dizaynlar_oqiladi()
    {
        Assert.NotEmpty(BuiltInPrintDesigns.Keys);

        foreach (var key in BuiltInPrintDesigns.Keys)
        {
            var design = BuiltInPrintDesigns.Get(key);

            Assert.Equal(key, design.Key);
            Assert.False(string.IsNullOrWhiteSpace(design.Name));
            Assert.NotEmpty(design.Elements);
            Assert.NotNull(design.Grid);
        }
    }

    [Fact]
    public void Kerakli_uchta_dizayn_mavjud_va_qamrovi_togri()
    {
        Assert.Equal(PrintScope.Class, BuiltInPrintDesigns.Get(BuiltInPrintDesigns.ClassBlue).Scope);
        Assert.Equal(PrintScope.Teacher, BuiltInPrintDesigns.Get(BuiltInPrintDesigns.TeacherGreen).Scope);
        Assert.Equal(PrintScope.School, BuiltInPrintDesigns.Get(BuiltInPrintDesigns.SchoolCompact).Scope);

        // Maktab dizayni bitta varaqqa bir nechta sinf sig'diradi.
        Assert.True(BuiltInPrintDesigns.Get(BuiltInPrintDesigns.SchoolCompact).Grid!.SectionsPerPage > 1);

        // Legenda qo'llab-quvvatlanishi: fanlar / o'qituvchilar / xonalar / darslar.
        var kinds = BuiltInPrintDesigns.All()
            .SelectMany(d => d.Elements.OfType<PrintLegendElement>())
            .Select(l => l.Legend)
            .Distinct()
            .ToList();

        Assert.Contains(PrintLegendKind.Subjects, kinds);
        Assert.Contains(PrintLegendKind.Rooms, kinds);
        Assert.Contains(PrintLegendKind.Lessons, kinds);
    }

    [Fact]
    public void Bosh_tarif_xato_beradi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load("   "));
        Assert.Contains("bo'sh", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Buzilgan_json_qator_raqamini_korsatadi()
    {
        var ex = Assert.Throws<PrintDesignException>(() =>
            PrintDesignLoader.Load("{ \"name\": \"x\",\n  \"elements\": [ { \"type\": } ] }"));

        Assert.Contains("JSON sintaksisi", ex.Message, StringComparison.Ordinal);
        Assert.Contains("qator", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Elements_yoq_bolsa_aniq_xato()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load("{ \"name\": \"x\" }"));

        Assert.Equal("elements", ex.Path);
        Assert.Contains("majburiy", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nomalum_element_turi_ruxsat_etilganlarni_sanaydi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"rasm\", \"rect\": [0,0,1,1] } ] }"));

        Assert.Equal("elements[0].type", ex.Path);
        Assert.Contains("rasm", ex.Message, StringComparison.Ordinal);
        Assert.Contains("timetable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rect_tortta_son_bolishi_shart()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"text\", \"rect\": [0, 0, 1] } ] }"));

        Assert.Equal("elements[0].rect", ex.Path);
        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rect_normallashtirilgan_oraliqdan_chiqmasin()
    {
        // aSc 0..1000000 ishlatadi; bizda 0..1 — 1000000 yozib qo'yish tipik xato.
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"text\", \"rect\": [0, 0, 1000000, 1] } ] }"));

        Assert.Equal("elements[0].rect", ex.Path);
        Assert.Contains("0..1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Teskari_rect_xato_beradi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"text\", \"rect\": [0.8, 0, 0.2, 1] } ] }"));

        Assert.Contains("chap chegaradan", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Notogri_rang_formati_namuna_korsatadi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"text\", \"rect\": [0,0,1,1], \"color\": \"ko'k\" } ] }"));

        Assert.Equal("elements[0].color", ex.Path);
        Assert.Contains("#RRGGBB", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nomalum_enum_qiymati_ruxsat_etilganlarni_sanaydi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"timetable\", \"rect\": [0,0,1,1], \"axis\": \"diagonal\" } ] }"));

        Assert.Equal("elements[0].axis", ex.Path);
        Assert.Contains("days-as-columns", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Notogri_tur_maydonda_kutilgan_tur_aytiladi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"text\", \"rect\": [0,0,1,1], \"bold\": \"ha\" } ] }"));

        Assert.Equal("elements[0].bold", ex.Path);
        Assert.Contains("true", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ikkita_jadval_elementi_taqiqlanadi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            """
            { "elements": [
                { "type": "timetable", "rect": [0,0,1,0.5] },
                { "type": "timetable", "rect": [0,0.5,1,1] } ] }
            """));

        Assert.Equal("elements", ex.Path);
        Assert.Contains("BITTA", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Shrift_nisbati_chegaradan_chiqmasin()
    {
        var ex = Assert.Throws<PrintDesignException>(() => PrintDesignLoader.Load(
            "{ \"elements\": [ { \"type\": \"text\", \"rect\": [0,0,1,1], \"fontRatio\": 12 } ] }"));

        Assert.Equal("elements[0].fontRatio", ex.Path);
    }

    [Fact]
    public void Yoq_fayl_uchun_tushunarli_xato()
    {
        var ex = Assert.Throws<PrintDesignException>(() =>
            PrintDesignLoader.LoadFile(Path.Combine(Path.GetTempPath(), "yoq-dizayn-12345.json")));

        Assert.Contains("topilmadi", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nomalum_tayyor_dizayn_mavjudlarini_sanaydi()
    {
        var ex = Assert.Throws<PrintDesignException>(() => BuiltInPrintDesigns.Get("yoq-dizayn"));

        Assert.Contains(BuiltInPrintDesigns.ClassBlue, ex.Message, StringComparison.Ordinal);
    }
}
