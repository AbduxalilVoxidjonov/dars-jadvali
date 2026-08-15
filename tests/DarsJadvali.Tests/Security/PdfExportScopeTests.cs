using DarsJadvali.Application.Export;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Export;
using Xunit;

namespace DarsJadvali.Tests.Security;

/// <summary>
/// E-01: PDF eksport qamrovi. Ilgari <c>ClassGroupId = null</c> bo'lsa jimgina
/// BUTUN MAKTAB jadvali chiqardi. Endi qamrov metod nomida aniq ko'rsatiladi
/// va noto'g'ri qiymatda xato tashlanadi.
/// </summary>
public class PdfExportScopeTests
{
    /// <summary>Bazaga umuman bormaydigan soxta quruvchi — faqat tekshiruv yo'lini sinash uchun.</summary>
    private sealed class NeverCalledBuilder : ITimetableExportModelBuilder
    {
        public bool WasCalled { get; private set; }

        public Task<TimetableDocumentModel> BuildAsync(PdfExportOptions options, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new TimetableDocumentModel(
                null,
                Array.Empty<WeekDay>(),
                Array.Empty<string>(),
                Array.Empty<TimetableClassBlockModel>(),
                0));
        }
    }

    // -----------------------------------------------------------------
    // 1. Qamrov ko'rsatilmaganda xato
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Sinf_korsatilmasa_ArgumentException_tashlanadi(int classGroupId)
    {
        var builder = new NeverCalledBuilder();
        var exporter = new SchoolTimetablePdfExporter(builder);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => exporter.ExportClassScheduleAsync(classGroupId));

        Assert.Equal("classGroupId", error.ParamName);
        Assert.False(builder.WasCalled, "Xato holatida bazaga umuman borilmasligi kerak.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Oqituvchi_korsatilmasa_ArgumentException_tashlanadi(int teacherId)
    {
        var exporter = new SchoolTimetablePdfExporter(new NeverCalledBuilder());

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => exporter.ExportTeacherScheduleAsync(teacherId));

        Assert.Equal("teacherId", error.ParamName);
    }

    /// <summary>Mavjud bo'lmagan o'qituvchi ham jim qolmaydi.</summary>
    [Fact]
    public async Task Mavjud_bolmagan_oqituvchida_ArgumentException_tashlanadi()
    {
        using var db = new TestDbFactory();
        var exporter = CreateFullExporter(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => exporter.ExportTeacherScheduleAsync(9999));
    }

    // -----------------------------------------------------------------
    // 2. Qamrov to'g'ri qo'llanishi
    // -----------------------------------------------------------------

    /// <summary>Sinf qamrovi sozlamadagi qiymatdan QAT'I NAZAR metod argumentidan olinadi.</summary>
    [Fact]
    public async Task Sinf_qamrovi_metod_argumentidan_olinadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 4);
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var first = db.AddClassGroup("5-A");
        var second = db.AddClassGroup("6-B");
        db.AddEntry(first, subject, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(second, subject, teacher, WeekDay.Seshanba, 2);

        var exporter = CreateFullExporter(db);

        // Sozlamada boshqa sinf ko'rsatilgan bo'lsa ham — argument ustun.
        var document = await exporter.ExportClassScheduleAsync(
            second.Id,
            new PdfExportOptions { ClassGroupId = first.Id });

        Assert.NotEmpty(document.Content);
        Assert.Contains("6-B", document.FileName, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", document.FileName, StringComparison.Ordinal);
    }

    /// <summary>Butun maktab qamrovi endi ATAYLAB so'raladi.</summary>
    [Fact]
    public async Task Maktab_qamrovi_alohida_metod_bilan_soraladi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 4);
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup("5-A");
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        var exporter = CreateFullExporter(db);

        var document = await exporter.ExportSchoolScheduleAsync();

        Assert.NotEmpty(document.Content);
        Assert.StartsWith("Maktab-jadvali", document.FileName, StringComparison.Ordinal);
    }

    /// <summary>O'qituvchi jadvali alohida qamrov sifatida ishlaydi.</summary>
    [Fact]
    public async Task Oqituvchi_jadvali_chiqariladi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 4);
        var teacher = db.AddTeacher("Karimova Nodira");
        var other = db.AddTeacher("Salimov Anvar");
        var subject = db.AddSubject();
        var group = db.AddClassGroup("5-A");
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);
        db.AddEntry(group, subject, other, WeekDay.Dushanba, 2);

        var exporter = CreateFullExporter(db);

        var document = await exporter.ExportTeacherScheduleAsync(teacher.Id);

        Assert.NotEmpty(document.Content);
        Assert.Contains("Karimova", document.FileName, StringComparison.Ordinal);
    }

    /// <summary>Jadval servislarisiz qurilgan eksportchi o'qituvchi jadvalini chiqara olmaydi.</summary>
    [Fact]
    public async Task Toliqsiz_qurilma_oqituvchi_jadvalini_chiqara_olmaydi()
    {
        var exporter = new SchoolTimetablePdfExporter(new NeverCalledBuilder());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => exporter.ExportTeacherScheduleAsync(1));
    }

    private static SchoolTimetablePdfExporter CreateFullExporter(TestDbFactory db)
        => new(
            db.Get<ITimetableExportModelBuilder>(),
            db.Get<IScheduleService>(),
            db.Get<IWorkDayService>(),
            db.Get<ITeacherService>(),
            db.Get<IClassGroupService>());
}
