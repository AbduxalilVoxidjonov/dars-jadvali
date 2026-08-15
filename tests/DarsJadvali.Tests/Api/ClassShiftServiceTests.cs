using DarsJadvali.Application.Services;
using DarsJadvali.Tests.Generation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// Sinf smenasini o'zgartirish: <c>ISchedulingStore.SetClassShiftAsync</c> ustidagi
/// Application servisi va uning xato xabarlari.
/// </summary>
/// <remarks>
/// Ilgari bu store metodini <b>hech kim chaqirmasdi</b> — backfill barcha sinfni
/// 1-smenaga qo'yib qo'yardi va foydalanuvchi buni o'zgartira olmasdi.
/// </remarks>
public sealed class ClassShiftServiceTests
{
    private static ClassShiftService Service(GenerationWorld world)
        => new(world.UnitOfWork(), world.Store());

    [Fact]
    public async Task Smenalar_royxatida_tayinlanmagan_varianti_ham_bor()
    {
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);

        var options = await Service(world).GetShiftsAsync(world.Schedule.Id);

        // Birinchi variant — "smenadan chiqarish".
        Assert.Null(options[0].ShiftId);
        Assert.Equal(2, options.Count(o => o.ShiftId is not null));
        Assert.Equal(new[] { 1, 2 }, options.Where(o => o.ShiftId is not null).Select(o => o.ShiftNo));
    }

    [Fact]
    public async Task Sinflar_royxati_joriy_smenasi_bilan_keladi()
    {
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var first = world.AddClass("5-A", world.Shifts[0]);
        var second = world.AddClass("5-B", world.Shifts[1]);

        var views = await Service(world).GetClassShiftsAsync(world.Schedule.Id);

        Assert.Equal(2, views.Count);
        Assert.Equal(world.Shifts[0].Id, views.Single(v => v.SchoolClassId == first.Id).ShiftId);
        Assert.Equal("2-smena", views.Single(v => v.SchoolClassId == second.Id).ShiftName);
    }

    [Fact]
    public async Task Smena_ozgartirilib_bazaga_yoziladi()
    {
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var schoolClass = world.AddClass("5-A", world.Shifts[0]);

        var result = await Service(world).SetShiftAsync(schoolClass.Id, world.Shifts[1].Id);

        Assert.True(result.Changed);
        Assert.Equal("Sinf smenasi yangilandi.", result.Message);

        var saved = await world.Context.SchoolClasses.AsNoTracking()
            .SingleAsync(c => c.Id == schoolClass.Id);

        Assert.Equal(world.Shifts[1].Id, saved.ShiftId);
    }

    [Fact]
    public async Task Smenadan_chiqarish_null_bilan_ishlaydi()
    {
        using var world = new GenerationWorld(shiftCount: 2, periodsPerShift: 6);
        var schoolClass = world.AddClass("5-A", world.Shifts[0]);

        var result = await Service(world).SetShiftAsync(schoolClass.Id, null);

        Assert.True(result.Changed);

        var saved = await world.Context.SchoolClasses.AsNoTracking()
            .SingleAsync(c => c.Id == schoolClass.Id);

        Assert.Null(saved.ShiftId);
    }

    [Fact]
    public async Task Begona_oquv_yili_smenasi_tushunarli_xabar_bilan_rad_etiladi()
    {
        using var world = new GenerationWorld(shiftCount: 1, periodsPerShift: 6);
        var schoolClass = world.AddClass("5-A", world.Shifts[0]);

        // Boshqa o'quv yilining smenasi.
        var otherYear = new DarsJadvali.Domain.Entities.AcademicYear
        {
            Name = "2026–2027",
            StartYear = 2026,
            DaysPerWeek = 6,
            WeeksInCycle = 1,
            TermsCount = 4,
        };

        world.Context.AcademicYears.Add(otherYear);
        world.Context.SaveChanges();

        var foreignShift = new DarsJadvali.Domain.Entities.Shift
        {
            AcademicYearId = otherYear.Id,
            ShiftNo = 1,
            Name = "1-smena",
            ShortName = "I",
        };

        world.Context.Shifts.Add(foreignShift);
        world.Context.SaveChanges();

        var result = await Service(world).SetShiftAsync(schoolClass.Id, foreignShift.Id);

        Assert.False(result.Changed);
        Assert.Contains("o'quv yiliga tegishli emas", result.Message, StringComparison.Ordinal);

        // Baza tegilmagan.
        var saved = await world.Context.SchoolClasses.AsNoTracking()
            .SingleAsync(c => c.Id == schoolClass.Id);

        Assert.Equal(world.Shifts[0].Id, saved.ShiftId);
    }

    [Fact]
    public async Task Yoq_sinf_uchun_xabar_qaytadi()
    {
        using var world = new GenerationWorld(shiftCount: 1, periodsPerShift: 6);

        var result = await Service(world).SetShiftAsync(9999, world.Shifts[0].Id);

        Assert.False(result.Changed);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }
}
