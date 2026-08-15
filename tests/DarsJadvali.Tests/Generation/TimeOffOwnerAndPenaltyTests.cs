using DarsJadvali.Application.Scheduling;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Scheduling.Model;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// <c>V2_06</c> — <c>TimeOff</c> ning yadroga o'girilishi: BARCHA egalar
/// (o'qituvchi / guruh / sinf / xona / fan / parallel / butun maktab) va
/// <c>Penalty</c> ning roli.
/// </summary>
public class TimeOffOwnerAndPenaltyTests
{
    // =====================================================================
    // 1. Har xil egalar
    // =====================================================================

    [Fact]
    public async Task Oqituvchi_egasi_ozining_matritsasiga_tushadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);

        world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, 1, AvailabilityLevel.Forbidden);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Teachers[mapped.Map.Teachers.IndexOf(teacher.Id)], grid, mapped, 0, 1));

        // Boshqa resurslar tegilmagan.
        Assert.Equal(
            AvailabilityState.Allowed,
            StateOf(mapped.Problem.Classes[mapped.Map.Classes.IndexOf(cls.Id)], grid, mapped, 0, 1));
    }

    [Fact]
    public async Task Sinf_egasi_ozining_matritsasiga_tushadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);

        world.AddTimeOff(ResourceOwnerKind.SchoolClass, cls.Id, 1, 2, AvailabilityLevel.Forbidden);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Classes[mapped.Map.Classes.IndexOf(cls.Id)], grid, mapped, 1, 2));
    }

    [Fact]
    public async Task Guruh_egasi_ozining_matritsasiga_tushadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        var group = world.Group(cls, "1-guruh");
        world.AddLesson(subject, teacher, cls, group);

        world.AddTimeOff(ResourceOwnerKind.StudentGroup, group.Id, 2, 3, AvailabilityLevel.Forbidden);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Groups[mapped.Map.Groups.IndexOf(group.Id)], grid, mapped, 2, 3));

        // Qo'shni guruh erkin qoladi — cheklov guruh aniqligida.
        var other = world.Group(cls, "2-guruh");
        Assert.Equal(
            AvailabilityState.Allowed,
            StateOf(mapped.Problem.Groups[mapped.Map.Groups.IndexOf(other.Id)], grid, mapped, 2, 3));
    }

    [Fact]
    public async Task Xona_egasi_ozining_matritsasiga_tushadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);
        var room = world.AddClassroom("101-xona");

        world.AddTimeOff(ResourceOwnerKind.Classroom, room.Id, 3, 4, AvailabilityLevel.Forbidden);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Rooms[mapped.Map.Rooms.IndexOf(room.Id)], grid, mapped, 3, 4));
    }

    [Fact]
    public async Task Fan_egasi_ozining_matritsasiga_tushadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);

        world.AddTimeOff(ResourceOwnerKind.Subject, subject.Id, 0, 5, AvailabilityLevel.Forbidden);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Subjects[mapped.Map.Subjects.IndexOf(subject.Id)], grid, mapped, 0, 5));
    }

    /// <summary>Parallel cheklovi shu paralleldagi BARCHA sinflarga tarqaladi.</summary>
    [Fact]
    public async Task Parallel_egasi_shu_paralleldagi_hamma_sinfga_tushadi()
    {
        using var world = new GenerationWorld();
        var grade = world.AddGrade(5);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");

        var a = world.AddClass("5-A");
        var b = world.AddClass("5-B");
        var c = world.AddClass("6-A");

        a.GradeId = grade.Id;
        b.GradeId = grade.Id;
        world.Context.SaveChanges();

        world.AddLesson(subject, teacher, a);

        world.AddTimeOff(ResourceOwnerKind.Grade, grade.Id, 1, 1, AvailabilityLevel.Forbidden);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(AvailabilityState.Forbidden, StateOf(Class(mapped, a.Id), grid, mapped, 1, 1));
        Assert.Equal(AvailabilityState.Forbidden, StateOf(Class(mapped, b.Id), grid, mapped, 1, 1));
        Assert.Equal(AvailabilityState.Allowed, StateOf(Class(mapped, c.Id), grid, mapped, 1, 1));
    }

    /// <summary>Butun maktab cheklovi barcha o'qituvchi va sinflarga tushadi.</summary>
    [Fact]
    public async Task Global_egasi_hamma_oqituvchi_va_sinfga_tushadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var other = world.AddTeacher("Karimova Nodira");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);

        world.AddTimeOff(ResourceOwnerKind.Global, 0, 4, 2, AvailabilityLevel.Forbidden);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Teachers[mapped.Map.Teachers.IndexOf(teacher.Id)], grid, mapped, 4, 2));
        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Teachers[mapped.Map.Teachers.IndexOf(other.Id)], grid, mapped, 4, 2));
        Assert.Equal(AvailabilityState.Forbidden, StateOf(Class(mapped, cls.Id), grid, mapped, 4, 2));
    }

    // =====================================================================
    // 2. Penalty
    // =====================================================================

    /// <summary>
    /// Oddiy jarima "?" holatini o'zgartirmaydi — yadro uni bitta qat'iy og'irlik bilan
    /// hisoblaydi, shuning uchun mapper faqat izoh qoldiradi.
    /// </summary>
    [Fact]
    public async Task Oddiy_jarima_soft_holat_bolib_qoladi_va_izoh_beriladi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);

        world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, 1, AvailabilityLevel.NotRecommended, penalty: 250);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Questioned,
            StateOf(mapped.Problem.Teachers[mapped.Map.Teachers.IndexOf(teacher.Id)], grid, mapped, 0, 1));

        Assert.Contains(mapped.Notes, n => n.Contains("yagona darajaga tushirildi", StringComparison.Ordinal));
    }

    /// <summary>Eng yuqori jarima amalda TAQIQ sifatida qo'llanadi.</summary>
    [Fact]
    public async Task Eng_yuqori_jarima_taqiqqa_aylanadi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);

        world.AddTimeOff(
            ResourceOwnerKind.Teacher, teacher.Id, 0, 1,
            AvailabilityLevel.NotRecommended, penalty: TimeOff.HardThreshold);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Forbidden,
            StateOf(mapped.Problem.Teachers[mapped.Map.Teachers.IndexOf(teacher.Id)], grid, mapped, 0, 1));

        Assert.Contains(mapped.Notes, n => n.Contains("TAQIQ sifatida", StringComparison.Ordinal));
    }

    /// <summary>Jarima 0 bo'lsa hech qanday izoh chiqmaydi (odatiy holat).</summary>
    [Fact]
    public async Task Jarimasiz_tavsiya_etilmaydi_izohsiz_qoladi()
    {
        using var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls);

        world.AddTimeOff(ResourceOwnerKind.Teacher, teacher.Id, 0, 1, AvailabilityLevel.NotRecommended);

        var (mapped, grid) = await BuildAsync(world);

        Assert.Equal(
            AvailabilityState.Questioned,
            StateOf(mapped.Problem.Teachers[mapped.Map.Teachers.IndexOf(teacher.Id)], grid, mapped, 0, 1));

        Assert.DoesNotContain(mapped.Notes, n => n.Contains("yagona darajaga", StringComparison.Ordinal));
    }

    // =====================================================================
    // 3. Xona ro'yxati BO'SH bo'lganda ham hamma narsa ishlaydi
    // =====================================================================

    /// <summary>
    /// Foydalanuvchi maktabida xona ishlatilmaydi: <c>Classrooms</c> butunlay bo'sh
    /// bo'lsa ham generatsiya to'liq ishlashi shart.
    /// </summary>
    [Fact]
    public async Task Xona_royxati_bosh_bolsa_ham_generatsiya_ishlaydi()
    {
        using var world = new GenerationWorld(activeDays: 5, periodsPerShift: 6);
        var teacher = world.AddTeacher("Aliyev Vali");
        var cls = world.AddClass("5-A");
        var subject = world.AddSubject("Matematika", "MAT");
        world.AddLesson(subject, teacher, cls, periodsPerWeek: 5);

        Assert.Empty(world.Context.Classrooms.ToList());

        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 7 });

        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        Assert.Equal(5, world.Context.Cards.Count(c => c.ScheduleId == world.Schedule.Id));

        // Xona qatori umuman yozilmaydi.
        Assert.Empty(world.Context.CardClassrooms.ToList());
        Assert.DoesNotContain(
            world.Context.CardOccurrences.ToList(),
            o => o.ResourceKind == Domain.Enums.ResourceKind.Classroom);
    }

    // =====================================================================

    private static async Task<(MappedProblem Mapped, TimeGrid Grid)> BuildAsync(GenerationWorld world)
    {
        var input = await world.LoadAsync();
        var mapped = world.Mapper().BuildProblem(input);
        return (mapped, mapped.Problem.Grid);
    }

    private static ClassDef Class(MappedProblem mapped, int classId) =>
        mapped.Problem.Classes[mapped.Map.Classes.IndexOf(classId)];

    private static AvailabilityState StateOf(
        ResourceDef def, TimeGrid grid, MappedProblem mapped, int dayNo, int periodNo) =>
        def.Availability.Get(grid.SlotOf(dayNo, mapped.Map.IndexOfPeriodNo[periodNo]));
}
