using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.SchemaV2;

/// <summary>
/// Yaxlitlik kafolatlari: <c>Restrict</c> tashqi kalitlar, filtrlangan unikal indekslar,
/// <c>CHECK</c> cheklovlari, audit maydonlari va konkurentlik tokeni.
/// </summary>
public class SchemaIntegrityTests
{
    [Fact]
    public async Task Bogliq_darsi_bor_oqituvchi_ochirilsa_xato_beradi()
    {
        // Arrange — sxema v2 da o'qituvchi MA'LUMOTNOMA: unga bog'liq dars borligida
        // o'chirish JIMGINA kaskad qilmaydi, balki xato beradi.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");
        w.AddLesson(matematika, teacher, klass, w.EntireClass(klass));

        // Act — kuzatuvni tozalaymiz, aks holda EF bog'liq yozuvlarni xotirada uzib,
        // DB'gacha bormasdan xato beradi. Bizga DB darajasidagi kafolat kerak.
        w.Context.ChangeTracker.Clear();
        w.Context.Teachers.Remove(w.Context.Teachers.Single(t => t.Id == teacher.Id));

        // Assert — DB darajasida rad etiladi (jimgina kaskad EMAS).
        await Assert.ThrowsAsync<DbUpdateException>(() => w.Context.SaveChangesAsync());

        w.Context.ChangeTracker.Clear();
        Assert.True(await w.Context.Teachers.AnyAsync(t => t.Id == teacher.Id));
        Assert.True(await w.Context.LessonTeachers.AnyAsync(lt => lt.TeacherId == teacher.Id));
    }

    [Fact]
    public async Task Bogliq_darsi_bor_fan_ochirilsa_xato_beradi()
    {
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");
        w.AddLesson(matematika, teacher, klass, w.EntireClass(klass));

        w.Context.ChangeTracker.Clear();
        w.Context.Subjects.Remove(w.Context.Subjects.Single(s => s.Id == matematika.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => w.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Kartochka_ochirilsa_bandlik_qatorlari_ham_ochadi()
    {
        // Arrange — egalik zanjiri: Card -> CardOccurrence Cascade.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");
        var lesson = w.AddLesson(matematika, teacher, klass, w.EntireClass(klass));
        var card = w.AddCard(lesson, dayNo: 0, periodNo: 1);
        await w.RebuildAsync();

        Assert.NotEmpty(w.Occurrences());

        // Act
        w.Context.Cards.Remove(w.Context.Cards.Single(c => c.Id == card.Id));
        await w.Context.SaveChangesAsync();

        // Assert
        Assert.Empty(w.Occurrences());
    }

    [Fact]
    public async Task Har_sinfda_aynan_bitta_butun_sinf_guruhi_boladi()
    {
        // Arrange
        using var w = new V2World();
        var klass = w.AddClass("5-A");

        Assert.Equal(ClassStructureFactoryConstants.GroupsPerClass,
            w.Context.StudentGroups.Count(g => g.SchoolClassId == klass.Id));

        // Act — ikkinchi "butun sinf" guruhini qo'shishga urinish.
        var division = w.Context.ClassDivisions.First(d => d.SchoolClassId == klass.Id);
        w.Context.StudentGroups.Add(new StudentGroup
        {
            SchoolClassId = klass.Id,
            ClassDivisionId = division.Id,
            Name = "Yana butun sinf",
            IsEntireClass = true
        });

        // Assert — filtrlangan unikal indeks to'sadi va sabab TIPLI istisno bilan keladi.
        await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => w.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Manosiz_dars_tarifi_CHECK_bilan_toziladi()
    {
        // Arrange — PeriodsPerWeek < PeriodsPerCard.
        using var w = new V2World();
        var matematika = w.AddSubject("Matematika", "MAT");

        w.Context.Lessons.Add(new Lesson
        {
            AcademicYearId = w.Year.Id,
            SubjectId = matematika.Id,
            PeriodsPerWeek = 1,
            PeriodsPerCard = 2
        });

        // Act + Assert — CHECK cheklovi tipli istisnoga o'giriladi.
        await Assert.ThrowsAsync<CheckConstraintViolationException>(
            () => w.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Joylashtirilmagan_kartochka_CHECK_bilan_toziladi()
    {
        // Arrange — WeeksMask = 0 ma'nosiz: kartochka hech qachon turmaydi.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");
        var lesson = w.AddLesson(matematika, teacher, klass, w.EntireClass(klass));

        w.Context.Cards.Add(new Card
        {
            ScheduleId = w.Schedule.Id,
            LessonId = lesson.Id,
            PeriodId = w.Periods[1].Id,
            DayNo = 0,
            WeeksMask = 0
        });

        await Assert.ThrowsAsync<CheckConstraintViolationException>(
            () => w.Context.SaveChangesAsync());
    }

    [Fact]
    public void Audit_maydonlari_avtomatik_toldiriladi()
    {
        // Arrange + Act
        using var w = new V2World();
        var teacher = w.AddTeacher("Aliyev Vali");

        // Assert — yaratilishda.
        Assert.NotEqual(Guid.Empty, teacher.Uid);
        Assert.NotEqual(default, teacher.CreatedAtUtc);
        Assert.Null(teacher.UpdatedAtUtc);
        var firstVersion = teacher.RowVersion;

        // Act — yangilashda.
        teacher.Phone = "+998901234567";
        w.Context.SaveChanges();

        // Assert
        Assert.NotNull(teacher.UpdatedAtUtc);
        Assert.NotEqual(firstVersion, teacher.RowVersion);
    }

    [Fact]
    public void Uid_har_jadvalda_noyob()
    {
        using var w = new V2World();
        var first = w.AddTeacher("Aliyev Vali");
        var second = w.AddTeacher("Karimova Nodira");

        Assert.NotEqual(first.Uid, second.Uid);

        // Mavjud yozuvning Uid'ini o'zgartirib bo'lmaydi — u o'zgarmas (StampAudit).
        second.Uid = first.Uid;
        w.Context.SaveChanges();
        w.Context.ChangeTracker.Clear();
        Assert.NotEqual(first.Uid, w.Context.Teachers.Single(t => t.Id == second.Id).Uid);

        // Yangi yozuvga takroriy Uid berilsa — unikal indeks to'sadi.
        w.Context.Teachers.Add(new Teacher
        {
            FullName = "Uchinchi o'qituvchi",
            AcademicYearId = w.Year.Id,
            Uid = first.Uid
        });

        Assert.Throws<UniqueConstraintViolationException>(() => w.Context.SaveChanges());
    }

    [Fact]
    public async Task Xona_royxati_bosh_bolsa_ham_hammasi_ishlaydi()
    {
        // Arrange — xona moduli P1: xona umuman kiritilmagan holat.
        using var w = new V2World();
        var klass = w.AddClass("5-A");
        var matematika = w.AddSubject("Matematika", "MAT");
        var teacher = w.AddTeacher("Aliyev Vali");
        var lesson = w.AddLesson(matematika, teacher, klass, w.EntireClass(klass));
        w.AddCard(lesson, dayNo: 0, periodNo: 1);

        // Act
        var rows = await w.RebuildAsync();

        // Assert
        Assert.Equal(0, await w.Context.Classrooms.CountAsync());
        Assert.Equal(6, rows);
    }

    [Fact]
    public async Task Tranzaksiya_xato_bolganda_hammasini_qaytaradi()
    {
        // Arrange — generator xatosida butun jadval yo'qolmasligi kerak.
        using var db = new TestDbFactory();
        var uow = (ITransactionalUnitOfWork)db.Get<Application.Abstractions.IUnitOfWork>();

        var before = await uow.Subjects.GetAllAsync();
        Assert.Empty(before);

        // Act — ikkita fan qo'shiladi, keyin xato tashlanadi.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uow.ExecuteInTransactionAsync(async ct =>
            {
                await uow.Subjects.AddAsync(new Subject { Name = "Matematika", Code = "MAT" }, ct);
                await uow.Subjects.AddAsync(new Subject { Name = "Fizika", Code = "FIZ" }, ct);
                throw new InvalidOperationException("Generator xatosi.");
            }));

        // Assert — hech biri saqlanmagan.
        var after = await db.GetFromNewScope<Application.Abstractions.IUnitOfWork>()
            .Subjects.GetAllAsync();
        Assert.Empty(after);
    }

    [Fact]
    public async Task Tranzaksiya_muvaffaqiyatli_bolsa_hammasini_saqlaydi()
    {
        using var db = new TestDbFactory();
        var uow = (ITransactionalUnitOfWork)db.Get<Application.Abstractions.IUnitOfWork>();

        await uow.ExecuteInTransactionAsync(async ct =>
        {
            await uow.Subjects.AddAsync(new Subject { Name = "Matematika", Code = "MAT" }, ct);
            await uow.Subjects.AddAsync(new Subject { Name = "Fizika", Code = "FIZ" }, ct);
        });

        var after = await db.GetFromNewScope<Application.Abstractions.IUnitOfWork>()
            .Subjects.GetAllAsync();
        Assert.Equal(2, after.Count);
    }
}

/// <summary>Test o'qilishi uchun kichik yordamchi.</summary>
internal static class ClassStructureFactoryConstants
{
    public const int GroupsPerClass =
        Infrastructure.Persistence.Backfill.ClassStructureFactory.GroupsPerClass;
}
