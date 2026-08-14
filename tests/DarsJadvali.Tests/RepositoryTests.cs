using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// <c>IRepository&lt;T&gt;</c> / <c>IUnitOfWork</c> CRUD testlari va
/// <c>GetAllAsync</c> navigatsiyalar bilan qaytishini (AutoInclude) tasdiqlash.
/// </summary>
public class RepositoryTests
{
    [Fact]
    public async Task AddAsync_yangi_yozuv_qoshadi_va_Id_beradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var uow = db.Get<IUnitOfWork>();

        // Act
        var teacher = await uow.Teachers.AddAsync(new Teacher { FullName = "Aliyev Vali" });

        // Assert
        Assert.True(teacher.Id > 0);
        Assert.True(await uow.Teachers.ExistsAsync(teacher.Id));
    }

    [Fact]
    public async Task GetByIdAsync_mavjud_yozuvni_qaytaradi_yoq_bolsa_null()
    {
        // Arrange
        using var db = new TestDbFactory();
        var uow = db.Get<IUnitOfWork>();
        var created = await uow.Teachers.AddAsync(new Teacher { FullName = "Karimova Nodira" });

        // Act
        var found = await uow.Teachers.GetByIdAsync(created.Id);
        var missing = await uow.Teachers.GetByIdAsync(created.Id + 1000);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("Karimova Nodira", found!.FullName);
        Assert.Null(missing);
    }

    [Fact]
    public async Task UpdateAsync_yozuvni_yangilaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var uow = db.Get<IUnitOfWork>();
        var teacher = await uow.Teachers.AddAsync(new Teacher { FullName = "Eski Ism" });

        // Act
        teacher.FullName = "Yangi Ism";
        teacher.IsActive = false;
        await uow.Teachers.UpdateAsync(teacher);

        // Assert — yangi skopdan (yangi DbContext) o'qiymiz.
        var reloaded = await db.GetFromNewScope<IUnitOfWork>().Teachers.GetByIdAsync(teacher.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Yangi Ism", reloaded!.FullName);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_yozuvni_ochiradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var uow = db.Get<IUnitOfWork>();
        var subject = await uow.Subjects.AddAsync(new Subject { Name = "Kimyo", Code = "KIM" });

        // Act
        await uow.Subjects.DeleteAsync(subject.Id);

        // Assert
        Assert.False(await uow.Subjects.ExistsAsync(subject.Id));
        Assert.Empty(await uow.Subjects.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_mavjud_bolmagan_Id_uchun_xato_bermaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var uow = db.Get<IUnitOfWork>();

        // Act + Assert — istisno tashlanmasligi kerak.
        await uow.ClassGroups.DeleteAsync(9999);
    }

    [Fact]
    public async Task GetAllAsync_barcha_yozuvlarni_qaytaradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var uow = db.Get<IUnitOfWork>();
        await uow.ClassGroups.AddAsync(new ClassGroup { Name = "5-A", StudentCount = 25 });
        await uow.ClassGroups.AddAsync(new ClassGroup { Name = "5-B", StudentCount = 27 });

        // Act
        var all = await uow.ClassGroups.GetAllAsync();

        // Assert
        Assert.Equal(2, all.Count);
    }

    /// <summary>
    /// AutoInclude ishlayotganini tasdiqlaydi: yangi DbContext'da ham
    /// ScheduleEntry navigatsiyalari (Teacher/Subject/ClassGroup) to'ldirilgan bo'lishi kerak.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ScheduleEntry_navigatsiyalari_bilan_qaytadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher("Aliyev Vali");
        var subject = db.AddSubject("Matematika", "MAT");
        var group = db.AddClassGroup("5-A");
        db.AddAssignment(teacher, subject, group);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        // Act — tracking keshiga tayanmaslik uchun yangi skop.
        var entries = await db.GetFromNewScope<IUnitOfWork>().ScheduleEntries.GetAllAsync();

        // Assert
        var entry = Assert.Single(entries);
        Assert.NotNull(entry.Teacher);
        Assert.NotNull(entry.Subject);
        Assert.NotNull(entry.ClassGroup);
        Assert.Equal("Aliyev Vali", entry.Teacher!.FullName);
        Assert.Equal("Matematika", entry.Subject!.Name);
        Assert.Equal("5-A", entry.ClassGroup!.Name);
    }

    /// <summary>AutoInclude TeacherAssignment uchun ham yoqilgan bo'lishi kerak.</summary>
    [Fact]
    public async Task GetAllAsync_TeacherAssignment_navigatsiyalari_bilan_qaytadi()
    {
        // Arrange
        using var db = new TestDbFactory();
        var teacher = db.AddTeacher("Karimova Nodira");
        var subject = db.AddSubject("Fizika", "FIZ");
        var group = db.AddClassGroup("7-B");
        db.AddAssignment(teacher, subject, group, weeklyHours: 4);

        // Act
        var assignments = await db.GetFromNewScope<IUnitOfWork>().Assignments.GetAllAsync();

        // Assert
        var assignment = Assert.Single(assignments);
        Assert.NotNull(assignment.Teacher);
        Assert.NotNull(assignment.Subject);
        Assert.NotNull(assignment.ClassGroup);
        Assert.Equal(4, assignment.WeeklyHoursCount);
    }

    [Fact]
    public async Task Oqituvchi_ochirilsa_bogliq_yozuvlar_ham_ochadi()
    {
        // Arrange — Cascade delete tekshiruvi (CONTRACT §3).
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        var uow = db.Get<IUnitOfWork>();

        // Act
        await uow.Teachers.DeleteAsync(teacher.Id);

        // Assert
        Assert.Empty(await db.GetFromNewScope<IUnitOfWork>().ScheduleEntries.GetAllAsync());
        Assert.Empty(await db.GetFromNewScope<IUnitOfWork>().Assignments.GetAllAsync());
    }

    [Fact]
    public async Task LessonSlot_va_WorkDay_repozitoriylari_ishlaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var uow = db.Get<IUnitOfWork>();

        // Act
        var days = await uow.WorkDays.GetAllAsync();
        var slots = await uow.LessonSlots.GetAllAsync();

        // Assert
        Assert.Equal(7, days.Count);
        Assert.Equal(7, slots.Count);
        Assert.Contains(days, d => d.DayOfWeek == WeekDay.Yakshanba && !d.IsActive);
        Assert.Contains(slots, s => s.LessonNumber == 1 && s.StartTime == new TimeSpan(8, 30, 0));
    }
}
