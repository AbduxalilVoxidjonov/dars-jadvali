using System.Text;
using System.Text.RegularExpressions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Infrastructure.Export.Printing;
using DarsJadvali.Tests.Generation;
using Xunit;

namespace DarsJadvali.Tests.Export;

/// <summary>
/// Chop etish adapterining <c>Card</c>/<c>Lesson</c> modeliga ko'chishi.
/// </summary>
/// <remarks>
/// Eski <see cref="ScheduleEntryPrintableAdapter"/> uchta maydonni STANDART qiymat bilan
/// to'ldirardi (<c>Length=1</c>, "har hafta", guruh yo'q) — chunki eski modelda ular
/// umuman yo'q edi. Bu testlar aynan o'sha uchtasi endi HAQIQIY manbadan kelishini va
/// chizish dvigateli (juft blok, yonma-yon yo'lakchalar, smena polosasi) ularni
/// to'g'ri qabul qilishini tekshiradi.
/// </remarks>
public sealed class CardPrintableAdapterTests
{
    private static string Head(byte[] bytes) =>
        Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));

    private static int PageCount(byte[] bytes)
    {
        var match = Regex.Match(Encoding.Latin1.GetString(bytes), @"/Count\s+(\d+)");
        Assert.True(match.Success, "PDF ichida /Count topilmadi.");
        return int.Parse(match.Groups[1].Value);
    }

    private static DesignBasedTimetablePdfExporter Exporter(
        GenerationWorld world, DesignExportOptions? options = null) =>
        new(world.Get<ICardBoardService>(), world.Store(), world.UnitOfWork(), options);

    private static async Task<CardPrintAxes> AxesAsync(GenerationWorld world)
    {
        var input = await world.LoadAsync();
        return CardPrintableAdapter.ToAxes(
            input.WorkDays, input.Periods, input.Shifts, Math.Max(1, input.Schedule.WeeksInCycle));
    }

    // =================================================================
    // 1. Uchta "yo'qolgan" maydon: Length, WeeksMask, GroupName
    // =================================================================

    [Fact]
    public async Task Adapter_juft_darsni_hafta_maskasini_va_guruh_nomini_beradi()
    {
        // Arrange — 2 haftalik sikl (A/B ma'noga ega), guruhli juft dars.
        using var world = new GenerationWorld(weeksInCycle: 2, periodsPerShift: 7);
        var teacher = world.AddTeacher("Voxidjonov Abduxalil");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var group = world.Group(cls, "1-guruh");

        var lesson = world.AddLesson(subject, teacher, cls, group, periodsPerWeek: 4, periodsPerCard: 2);
        var card = world.AddCard(lesson, dayNo: 1, periodNo: 3, weeksMask: 0b10, length: 2);

        card.LegacyRoomNumber = "204";
        world.Context.SaveChanges();

        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);
        var axes = await AxesAsync(world);

        // Act
        var printable = Assert.Single(CardPrintableAdapter.ToCards(cards, axes));

        // Assert — aynan shu uchtasi eski adapterda STANDART qiymat edi.
        Assert.Equal(2, printable.Length);
        Assert.Equal(PrintableCard.WeekB, printable.WeeksMask);
        Assert.Equal("B", printable.WeekLabel);
        Assert.False(printable.IsEveryWeek);
        Assert.Equal("1-guruh", printable.GroupName);

        // Qolgan maydonlar ham manbadan.
        Assert.Equal("Matematika", printable.SubjectName);
        Assert.Equal(new[] { "Voxidjonov Abduxalil" }, printable.TeacherNames);
        Assert.Equal("204", printable.RoomName);
        Assert.Equal(1, printable.DayIndex);
        Assert.Equal(3, printable.Period);
        Assert.Equal(4, printable.EndPeriod);
    }

    /// <summary>Butun sinf darsida guruh nishoni chiqmaydi (bo'sh satr → <c>null</c>).</summary>
    [Fact]
    public async Task Butun_sinf_darsida_guruh_nomi_korsatilmaydi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Fizika", "FIZ");
        var cls = world.AddClass("6-B");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);
        world.AddCard(lesson, dayNo: 0, periodNo: 1);

        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);
        var printable = Assert.Single(CardPrintableAdapter.ToCards(cards, await AxesAsync(world)));

        Assert.Null(printable.GroupName);
    }

    /// <summary>
    /// Bir haftalik siklda A/B tushunchasi yo'q — mask 1 bo'lsa ham nishon chiqmaydi.
    /// </summary>
    [Theory]
    [InlineData(1, 1, PrintableCard.AllWeeks)]
    [InlineData(1, 2, PrintableCard.WeekA)]
    [InlineData(2, 2, PrintableCard.WeekB)]
    [InlineData(3, 2, PrintableCard.AllWeeks)]
    [InlineData(0, 2, PrintableCard.AllWeeks)]
    public void Hafta_maskasi_sikl_uzunligiga_qarab_ogiriladi(int mask, int weeksInCycle, int expected)
        => Assert.Equal(expected, CardPrintableAdapter.ToPrintWeeksMask(mask, weeksInCycle));

    // =================================================================
    // 2. Juft dars — YAXLIT blok (layout darajasida)
    // =================================================================

    [Fact]
    public async Task Juft_dars_yaxlit_blok_bolib_ikki_qatorni_egallaydi()
    {
        using var world = new GenerationWorld(periodsPerShift: 7);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Kimyo", "KIM");
        var cls = world.AddClass("7-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2, periodsPerCard: 2);
        world.AddCard(lesson, dayNo: 0, periodNo: 2, length: 2);

        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);
        var axes = await AxesAsync(world);
        var printable = CardPrintableAdapter.ToCards(cards, axes);

        var layout = TimetableGridLayout.Build(
            new PrintableSection("7-A", null, printable), axes.Days, axes.Periods);

        // Bitta blok — ikkiga bo'linib ketmagan.
        var block = Assert.Single(layout.Blocks);
        Assert.Equal(2, block.RowSpan);
        Assert.True(block.IsDouble);
        Assert.Equal(1, block.RowIndex);      // 2-soat → indeks 1
        Assert.Equal(2, block.LastRowIndex);  // 3-soatni ham egallaydi
        Assert.Empty(layout.Dropped);
    }

    // =================================================================
    // 3. Guruh darslari — bitta katakda YONMA-YON
    // =================================================================

    [Fact]
    public async Task Guruh_darslari_bir_katakda_yonma_yon_chiziladi()
    {
        using var world = new GenerationWorld(periodsPerShift: 7);
        var cls = world.AddClass("8-A");
        var first = world.Group(cls, "1-guruh");
        var second = world.Group(cls, "2-guruh");

        var english = world.AddSubject("Ingliz tili", "ING");
        var german = world.AddSubject("Nemis tili", "NEM");

        var t1 = world.AddTeacher("Karimova Dilnoza");
        var t2 = world.AddTeacher("Yusupov Anvar");

        world.AddCard(world.AddLesson(english, t1, cls, first, periodsPerWeek: 2), dayNo: 2, periodNo: 4);
        world.AddCard(world.AddLesson(german, t2, cls, second, periodsPerWeek: 2), dayNo: 2, periodNo: 4);

        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);
        var axes = await AxesAsync(world);
        var printable = CardPrintableAdapter.ToCards(cards, axes);

        Assert.Equal(2, printable.Count);
        Assert.Equal(new[] { "1-guruh", "2-guruh" }, printable.Select(c => c.GroupName).OrderBy(x => x).ToArray());

        var layout = TimetableGridLayout.Build(
            new PrintableSection("8-A", null, printable), axes.Days, axes.Periods);

        Assert.Equal(2, layout.Blocks.Count);

        // Ikkalasi ham bitta katakda: bir xil kun/qator, lekin har xil yo'lakcha.
        Assert.Single(layout.Blocks.Select(b => b.RowIndex).Distinct());
        Assert.Single(layout.Blocks.Select(b => b.DayIndex).Distinct());
        Assert.Equal(new[] { 0, 1 }, layout.Blocks.Select(b => b.Lane).OrderBy(x => x).ToArray());
        Assert.All(layout.Blocks, b => Assert.Equal(2, b.LaneCount));
        Assert.All(layout.Blocks, b => Assert.True(b.IsShared));
    }

    // =================================================================
    // 4. Ikki smena — UZLUKSIZ soat raqamlari
    // =================================================================

    [Fact]
    public async Task Ikki_smenada_soat_raqamlari_uzluksiz_qoladi()
    {
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var axes = await AxesAsync(world);

        Assert.Equal(12, axes.Periods.Count);

        // 2-smena 1 dan QAYTA boshlanmaydi.
        Assert.Equal(Enumerable.Range(1, 12), axes.Periods.Select(p => p.Number));
        Assert.Equal("7", axes.Periods[6].Label);

        Assert.Equal("1-smena", axes.Periods[5].ShiftName);
        Assert.Equal("2-smena", axes.Periods[6].ShiftName);
    }

    /// <summary>Bitta smenada polosa umuman chiqmaydi.</summary>
    [Fact]
    public async Task Bitta_smenada_smena_polosasi_yoq()
    {
        using var world = new GenerationWorld(shiftCount: 1, periodsPerShift: 6);
        var axes = await AxesAsync(world);

        Assert.Equal(6, axes.Periods.Count);
        Assert.All(axes.Periods, p => Assert.Null(p.ShiftName));
    }

    /// <summary>Ikki smenali kartochka to'g'ri qatorga tushadi (7-soat — 7-qator).</summary>
    [Fact]
    public async Task Ikkinchi_smenadagi_dars_ozining_qatorida_turadi()
    {
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Tarix", "TAR");
        var cls = world.AddClass("9-A", world.Shifts[1]);
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 2);
        world.AddCard(lesson, dayNo: 0, periodNo: 7);

        var cards = await world.Get<ICardBoardService>().GetCardsAsync(world.Schedule.Id);
        var axes = await AxesAsync(world);
        var printable = CardPrintableAdapter.ToCards(cards, axes);

        var layout = TimetableGridLayout.Build(
            new PrintableSection("9-A", null, printable), axes.Days, axes.Periods);

        var block = Assert.Single(layout.Blocks);
        Assert.Equal(7, block.Card.Period);
        Assert.Equal(6, block.RowIndex);  // 7-soat → 7-qator (indeks 6)
    }

    // =================================================================
    // 5. Uchidan-uchiga: Card modeli → dizayn → PDF / HTML
    // =================================================================

    /// <summary>Kichik maktab: guruhli juft dars, A/B hafta, ikki smena.</summary>
    private static (int ClassId, int TeacherId) SeedSchool(GenerationWorld world, int classCount = 2)
    {
        var mat = world.AddSubject("Matematika", "MAT");
        var ing = world.AddSubject("Ingliz tili", "ING");
        var nem = world.AddSubject("Nemis tili", "NEM");

        var classes = new List<int>();
        var teachers = new List<int>();

        for (var i = 0; i < classCount; i++)
        {
            var main = world.AddTeacher($"Aliyev Vali {i}");
            var t2 = world.AddTeacher($"Karimova Dilnoza {i}");
            var cls = world.AddClass($"{5 + i}-A");

            // Juft dars (2 soat), 1-smena.
            var doubleLesson = world.AddLesson(mat, main, cls, periodsPerWeek: 4, periodsPerCard: 2);
            var card = world.AddCard(doubleLesson, dayNo: 0, periodNo: 1, length: 2);
            card.LegacyRoomNumber = $"{101 + i}";

            // Guruh darslari — bitta katakda yonma-yon, A va B haftalarda.
            var g1 = world.Group(cls, "1-guruh");
            var g2 = world.Group(cls, "2-guruh");
            world.AddCard(world.AddLesson(ing, main, cls, g1, periodsPerWeek: 2), dayNo: 1, periodNo: 3, weeksMask: 0b01);
            world.AddCard(world.AddLesson(nem, t2, cls, g2, periodsPerWeek: 2), dayNo: 1, periodNo: 3, weeksMask: 0b10);

            classes.Add(cls.Id);
            teachers.Add(main.Id);
        }

        world.Context.SaveChanges();
        return (classes[0], teachers[0]);
    }

    [Fact]
    public async Task Card_modelidan_sinf_jadvali_pdf_beradi()
    {
        using var world = new GenerationWorld(weeksInCycle: 2, shiftCount: 2, periodsPerShift: 6);
        var (classId, _) = SeedSchool(world);

        var document = await Exporter(world).ExportClassScheduleAsync(
            classId, new PdfExportOptions { SchoolName = "12-sonli maktab" });

        Assert.Equal("%PDF-", Head(document.Content));
        Assert.True(document.Content.Length > 2000);
        Assert.Equal(1, PageCount(document.Content));
        Assert.Contains("5-A", document.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Card_modelidan_oqituvchi_jadvali_pdf_beradi()
    {
        using var world = new GenerationWorld(weeksInCycle: 2, periodsPerShift: 7);
        var (_, teacherId) = SeedSchool(world);

        var document = await Exporter(world).ExportTeacherScheduleAsync(teacherId);

        Assert.Equal("%PDF-", Head(document.Content));
        Assert.True(document.Content.Length > 2000);
    }

    [Fact]
    public async Task Card_modelidan_maktab_jadvali_pdf_beradi()
    {
        using var world = new GenerationWorld(weeksInCycle: 2, periodsPerShift: 7);
        SeedSchool(world, classCount: 3);

        var document = await Exporter(world).ExportSchoolScheduleAsync(
            new PdfExportOptions { SchoolName = "12-sonli maktab" });

        Assert.Equal("%PDF-", Head(document.Content));
        Assert.Equal(1, PageCount(document.Content));
    }

    /// <summary>HTML eksport ham yangi modeldan ishlaydi va OFFLINE qoladi.</summary>
    [Fact]
    public async Task Card_modelidan_html_offline_chiqadi()
    {
        using var world = new GenerationWorld(weeksInCycle: 2, periodsPerShift: 7);
        var (classId, _) = SeedSchool(world);

        var document = await Exporter(world).ExportClassScheduleHtmlAsync(classId);
        var html = Encoding.UTF8.GetString(document.Content);

        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);

        // Juft dars, guruh va A/B — hammasi chiqishda.
        Assert.Contains("Matematika", html, StringComparison.Ordinal);
        Assert.Contains("1-guruh", html, StringComparison.Ordinal);
    }

    /// <summary>Mavjud bo'lmagan sinf jim qolmaydi.</summary>
    [Fact]
    public async Task Mavjud_bolmagan_sinfda_xato_beriladi()
    {
        using var world = new GenerationWorld();
        SeedSchool(world, classCount: 1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Exporter(world).ExportClassScheduleAsync(9999));
    }

    /// <summary>Eksport yangi modeldan o'qiyotgani aniq ko'rinadi.</summary>
    [Fact]
    public void Yangi_konstruktor_card_modelini_ishlatadi()
    {
        using var world = new GenerationWorld();
        Assert.True(Exporter(world).UsesCardModel);
    }
}
