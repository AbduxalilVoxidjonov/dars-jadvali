using System.Text;
using System.Text.RegularExpressions;
using DarsJadvali.Infrastructure.Export.Printing;
using Xunit;

namespace DarsJadvali.Tests.Export;

/// <summary>
/// HTML eksport: bitta mustaqil (offline) fayl, tashqi resurssiz.
/// </summary>
public sealed class TimetableHtmlExporterTests
{
    private static readonly TimetableHtmlExporter Exporter = new();

    private static IReadOnlyList<PrintableDay> Days() => new[]
    {
        new PrintableDay(0, "Dushanba", "Du"),
        new PrintableDay(1, "Seshanba", "Se"),
        new PrintableDay(2, "Chorshanba", "Ch"),
    };

    private static IReadOnlyList<PrintablePeriod> Periods(int count = 6) =>
        Enumerable.Range(1, count).Select(i => new PrintablePeriod(i, i.ToString(), $"{7 + i:00}:30-{8 + i:00}:15")).ToList();

    private static PrintableTimetable Sample(int classCount = 1) => new()
    {
        SchoolName = "12-sonli umumiy oʻrta taʼlim maktabi",
        AcademicYear = "2025/2026",
        Term = "1-chorak",
        Scope = classCount > 1 ? PrintScope.School : PrintScope.Class,
        ScopeName = classCount > 1 ? "Maktab" : "5-A",
        Days = Days(),
        Periods = Periods(),
        GeneratedAt = new DateTime(2026, 3, 14),
        Sections = Enumerable.Range(0, classCount).Select(c => new PrintableSection(
            $"{5 + c}-A",
            "Xona: 101",
            new PrintableCard[]
            {
                new() { SubjectName = "Oʻzbek tili", DayIndex = 0, Period = 1, TeacherNames = new[] { "Gʻayratov Sanjar" }, RoomName = "101" },
                new() { SubjectName = "Русский язык", DayIndex = 0, Period = 2, TeacherNames = new[] { "Иванова Мария" } },
                new() { SubjectName = "Mehnat taʼlimi", DayIndex = 1, Period = 1, Length = 2 },
                new() { SubjectName = "Ingliz tili", DayIndex = 2, Period = 3, GroupName = "1-guruh" },
                new() { SubjectName = "Nemis tili", DayIndex = 2, Period = 3, GroupName = "2-guruh" },
                new() { SubjectName = "Musiqa", DayIndex = 1, Period = 4, WeeksMask = PrintableCard.WeekA },
            })).ToList(),
    };

    // ------------------------------------------------------------------

    [Fact]
    public void Toliq_html_hujjat_chiqadi()
    {
        var html = Exporter.Export(Sample());

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"uz\">", html, StringComparison.Ordinal);
        Assert.Contains("</html>", html, StringComparison.Ordinal);
        Assert.Contains("charset=\"utf-8\"", html, StringComparison.Ordinal);
        Assert.Contains("<table class=\"tt\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Tashqi_resurs_yoq_offline_ochiladi()
    {
        var html = Exporter.Export(Sample(3));

        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("//cdn", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<frame", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);

        // Tashqi CSS/shrift havolasi yo'q — faqat ichki <style>.
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<style>", html, StringComparison.Ordinal);

        // Bironta src="..." yoki href="http..." atributi bo'lmasin;
        // yagona havolalar — sahifa ichidagi "#s0" langarlari.
        Assert.DoesNotContain("src=", html, StringComparison.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(html, "href=\"([^\"]*)\""))
            Assert.StartsWith("#", match.Groups[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Kirill_va_ozbek_lotin_matn_ozgarmaydi()
    {
        var html = Exporter.Export(Sample());

        Assert.Contains("Oʻzbek tili", html, StringComparison.Ordinal);       // U+02BB
        Assert.Contains("Mehnat taʼlimi", html, StringComparison.Ordinal);     // U+02BC
        Assert.Contains("Gʻayratov Sanjar", html, StringComparison.Ordinal);
        Assert.Contains("Русский язык", html, StringComparison.Ordinal);
        Assert.Contains("Иванова Мария", html, StringComparison.Ordinal);
        Assert.Contains("12-sonli umumiy oʻrta taʼlim maktabi", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Utf8_baytlari_bomsiz_va_qayta_oqiladi()
    {
        var bytes = Exporter.ExportBytes(Sample());

        Assert.NotEqual(0xEF, bytes[0]);   // BOM yo'q
        Assert.Contains("Oʻzbek tili", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Juft_dars_ikki_qatorni_egallaydi()
    {
        var html = Exporter.Export(Sample());

        Assert.Contains("rowspan=\"2\"", html, StringComparison.Ordinal);
        Assert.Contains("card double", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Guruh_darslari_bir_katakda_yonma_yon()
    {
        var html = Exporter.Export(Sample());

        // BITTA <td> ichida ikkala guruh ham bo'lishi kerak (yonma-yon yo'lakchalar).
        var cell = html
            .Split("<td", StringSplitOptions.None)
            .FirstOrDefault(c => c.Contains("Ingliz tili", StringComparison.Ordinal));

        Assert.NotNull(cell);
        Assert.Contains("Nemis tili", cell!, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(cell!, "class=\"lane\"").Count);

        Assert.Contains("1-guruh", html, StringComparison.Ordinal);
        Assert.Contains("2-guruh", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hafta_belgisi_chiqadi()
    {
        var html = Exporter.Export(Sample());

        Assert.Contains("class=\"week\">A<", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Ikki_smena_polosasi_chiqadi()
    {
        var timetable = Sample() with
        {
            Periods = Enumerable.Range(1, 12)
                .Select(i => new PrintablePeriod(i, i.ToString(), null, i <= 6 ? "1-smena" : "2-smena"))
                .ToList(),
        };

        var html = Exporter.Export(timetable);

        Assert.Contains("class=\"shift\"", html, StringComparison.Ordinal);
        Assert.Contains("1-smena", html, StringComparison.Ordinal);
        Assert.Contains("2-smena", html, StringComparison.Ordinal);

        // Uzluksiz raqamlash: 12-soat sarlavhasi bor.
        Assert.Contains(">12<", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Kop_sinf_uchun_navigatsiya_qoshiladi()
    {
        var html = Exporter.Export(Sample(4));

        Assert.Contains("<nav class=\"sections\">", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#s3\"", html, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Matches(html, "<section class=\"grid\"").Count);
    }

    [Fact]
    public void Bitta_sinfda_navigatsiya_bolmaydi()
    {
        Assert.DoesNotContain("<nav", Exporter.Export(Sample()), StringComparison.Ordinal);
    }

    [Fact]
    public void Bosh_jadval_xabar_beradi()
    {
        var timetable = new PrintableTimetable
        {
            SchoolName = "Maktab",
            Days = Days(),
            Periods = Periods(),
            Sections = new[] { new PrintableSection("5-A", null, Array.Empty<PrintableCard>()) },
        };

        var html = Exporter.Export(timetable);

        // Apostrof HTML da qochiriladi: "qo'yilmagan" → "qo&#39;yilmagan".
        Assert.Contains("Hali dars qo&#39;yilmagan", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Xavfli_belgilar_qochiriladi()
    {
        var timetable = Sample() with
        {
            SchoolName = "<script>alert('x')</script> & \"maktab\"",
        };

        var html = Exporter.Export(timetable);

        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&amp;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Oqituvchi_qamrovida_katakda_sinf_korinadi()
    {
        var timetable = new PrintableTimetable
        {
            SchoolName = "Maktab",
            Scope = PrintScope.Teacher,
            ScopeName = "Gʻayratov Sanjar",
            Days = Days(),
            Periods = Periods(),
            Sections = new[]
            {
                new PrintableSection("Gʻayratov Sanjar", null, new PrintableCard[]
                {
                    new() { SubjectName = "Matematika", ClassName = "7-B", DayIndex = 0, Period = 1, TeacherNames = new[] { "Gʻayratov Sanjar" } },
                }),
            },
        };

        var html = Exporter.Export(timetable, HtmlExportOptions.ForScope(PrintScope.Teacher));

        Assert.Contains("7-B", html, StringComparison.Ordinal);

        // O'qituvchi ismi katak ichida takrorlanmaydi (faqat sarlavhalarda).
        Assert.DoesNotContain("<div class=\"line\">Gʻayratov Sanjar</div>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Fanlar_legendasi_qoshiladi()
    {
        var html = Exporter.Export(Sample());

        Assert.Contains("class=\"legend\"", html, StringComparison.Ordinal);
        Assert.Contains("<h3>Fanlar</h3>", html, StringComparison.Ordinal);
        Assert.Contains("soat", html, StringComparison.Ordinal);
    }
}
