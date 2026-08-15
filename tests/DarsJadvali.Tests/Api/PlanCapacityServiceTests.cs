using DarsJadvali.Application.Services;
using DarsJadvali.Tests.Generation;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// Reja sig'imi tekshiruvi: "rejalashtirilgan soat" vs "mavjud slot".
/// </summary>
/// <remarks>
/// Foydalanuvchining haqiqiy bazasida 1-A sinfida 47 soat rejalashtirilgan edi, lekin
/// jami 35 ta slot bor. Generator to'g'ri ishlagan, ammo darslarning bir qismi
/// <b>jimgina</b> joylashmay qolgan. Endi bu farq generatsiyadan OLDIN aytiladi.
/// </remarks>
public sealed class PlanCapacityServiceTests
{
    private static PlanCapacityService Service(GenerationWorld world)
        => new(world.UnitOfWork(), world.Store(), world.Mapper());

    [Fact]
    public async Task Sigimga_mos_rejada_ogohlantirish_bolmaydi()
    {
        // 5 kun × 6 soat = 30 slot; rejada 10 soat.
        using var world = new GenerationWorld(periodsPerShift: 6, activeDays: 5);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var schoolClass = world.AddClass("5-A");

        world.AddLesson(subject, teacher, schoolClass, periodsPerWeek: 10);

        var report = await Service(world).CheckAsync(world.Schedule.Id);

        Assert.False(report.HasWarnings);
        Assert.Equal(0, report.TotalOverflow);
        Assert.Contains("sig'adi", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sinf_sigimdan_oshsa_aniq_son_bilan_ogohlantiriladi()
    {
        // 5 kun × 7 soat = 35 slot; rejada 47 soat → 12 soat sig'maydi.
        using var world = new GenerationWorld(periodsPerShift: 7, activeDays: 5);
        var subject = world.AddSubject("Matematika", "MAT");
        var schoolClass = world.AddClass("1-A");

        // Bitta o'qituvchi 47 soatni ko'tarolmaydi, shuning uchun yuk bo'linadi —
        // sinfning yuki baribir 47 soat bo'lib qoladi.
        for (var i = 0; i < 5; i++)
        {
            var teacher = world.AddTeacher($"O'qituvchi {i + 1}");
            world.AddLesson(subject, teacher, schoolClass, periodsPerWeek: i == 4 ? 7 : 10);
        }

        var report = await Service(world).CheckAsync(world.Schedule.Id);

        Assert.True(report.HasWarnings);

        var warning = Assert.Single(report.Warnings, w => w.Scope == CapacityScope.Class);

        Assert.Equal("1-A", warning.Name);
        Assert.Equal(47, warning.PlannedPeriods);
        Assert.Equal(35, warning.AvailableSlots);
        Assert.Equal(12, warning.Overflow);
        Assert.Equal("1-A: 47 soat rejalashtirilgan, 35 ta slot bor — 12 soat sig'maydi.", warning.Message);
    }

    [Fact]
    public async Task Yadroning_Verify_fazasi_xatolari_ham_qaytadi()
    {
        using var world = new GenerationWorld(periodsPerShift: 7, activeDays: 5);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var schoolClass = world.AddClass("1-A");

        world.AddLesson(subject, teacher, schoolClass, periodsPerWeek: 47);

        var report = await Service(world).CheckAsync(world.Schedule.Id);

        // Xatolar ro'yxati AYNI manbadan — generatsiya hisobotidagi VerificationFaults bilan bir xil.
        Assert.NotEmpty(report.VerificationFaults);
        Assert.Contains(report.VerificationFaults, f => f.Contains("CLASS_OVERLOADED", StringComparison.Ordinal));
        Assert.Contains(report.VerificationFaults, f => f.Contains("1-A", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Oqituvchi_yuki_oshsa_ham_ogohlantiriladi()
    {
        // 5 kun × 6 soat = 30 slot; bitta o'qituvchida 40 soat.
        using var world = new GenerationWorld(periodsPerShift: 6, activeDays: 5);
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");

        for (var i = 0; i < 4; i++)
        {
            var schoolClass = world.AddClass($"5-{(char)('A' + i)}");
            world.AddLesson(subject, teacher, schoolClass, periodsPerWeek: 10);
        }

        var report = await Service(world).CheckAsync(world.Schedule.Id);

        var warning = Assert.Single(report.Warnings, w => w.Scope == CapacityScope.Teacher);

        Assert.Equal("Aliyev Vali", warning.Name);
        Assert.Equal(40, warning.PlannedPeriods);
        Assert.Equal(30, warning.AvailableSlots);
        Assert.Equal(10, warning.Overflow);
    }

    [Fact]
    public async Task Reja_bosh_bolsa_tekshiruv_ham_bosh()
    {
        using var world = new GenerationWorld(periodsPerShift: 6, activeDays: 5);
        world.AddClass("5-A");

        var report = await Service(world).CheckAsync(world.Schedule.Id);

        Assert.False(report.HasWarnings);
        Assert.Empty(report.VerificationFaults);
    }
}
