using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Api;

/// <summary>
/// 2-API: <see cref="IAvailabilityService.GetLessonAvailabilityForAllAsync"/>.
/// </summary>
/// <remarks>
/// Ilgari UI har bir o'qituvchi uchun alohida so'rov yuborardi (40 o'qituvchi = 40 so'rov).
/// Ommaviy variant natijasi yakka variant bilan AYNAN bir xil bo'lishi shart — aks holda
/// tezlik uchun to'g'rilik qurbon bo'lardi.
/// </remarks>
public class BulkAvailabilityTests
{
    /// <summary>Ommaviy natija yakka so'rov natijasi bilan bir xil.</summary>
    [Fact]
    public async Task Ommaviy_natija_yakka_soro_bilan_bir_xil()
    {
        // Arrange — uch o'qituvchi, uch xil holat.
        using var db = new TestDbFactory();
        db.SeedDefaults();

        var free = db.AddTeacher("Cheklovsiz");
        var limited = db.AddTeacher("Faqat ertalab");
        var busy = db.AddTeacher("Dushanba band");

        db.AddAvailability(limited, WeekDay.Dushanba,
            new TimeSpan(8, 30, 0), new TimeSpan(10, 10, 0), isAvailable: true);
        db.AddAvailability(busy, WeekDay.Dushanba,
            new TimeSpan(8, 0, 0), new TimeSpan(15, 0, 0), isAvailable: false);

        var service = db.Get<IAvailabilityService>();

        // Act
        var all = await service.GetLessonAvailabilityForAllAsync();

        // Assert — barcha o'qituvchilar natijada bor.
        Assert.Equal(3, all.Count);

        foreach (var teacher in new[] { free, limited, busy })
        {
            var single = await service.GetLessonAvailabilityAsync(teacher.Id);
            var bulk = all[teacher.Id];

            Assert.Equal(single.Count, bulk.Count);
            for (var i = 0; i < single.Count; i++)
            {
                Assert.Equal(single[i].Day, bulk[i].Day);
                Assert.Equal(single[i].HasRestriction, bulk[i].HasRestriction);
                Assert.Equal(single[i].AllowedLessonNumbers, bulk[i].AllowedLessonNumbers);
            }
        }
    }

    /// <summary>Cheklovi yo'q o'qituvchi ham natijada bo'ladi (bo'sh kalit qolmaydi).</summary>
    [Fact]
    public async Task Cheklovsiz_oqituvchi_ham_royxatda_boladi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher("Cheklovsiz");

        // Act
        var all = await db.Get<IAvailabilityService>().GetLessonAvailabilityForAllAsync();

        // Assert
        Assert.True(all.ContainsKey(teacher.Id));
        Assert.All(all[teacher.Id], d => Assert.False(d.HasRestriction));

        // Faqat FAOL ish kunlari (Yakshanba nofaol).
        Assert.Equal(6, all[teacher.Id].Count);
        Assert.DoesNotContain(all[teacher.Id], d => d.Day == WeekDay.Yakshanba);
    }

    /// <summary>Kun butunlay yopiq bo'lsa ruxsat etilgan soatlar ro'yxati bo'sh bo'ladi.</summary>
    [Fact]
    public async Task Yopiq_kun_bosh_royxat_beradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher("Dushanba band");
        db.AddAvailability(teacher, WeekDay.Dushanba,
            new TimeSpan(0, 0, 0), new TimeSpan(23, 59, 0), isAvailable: false);

        // Act
        var all = await db.Get<IAvailabilityService>().GetLessonAvailabilityForAllAsync();
        var monday = all[teacher.Id].Single(d => d.Day == WeekDay.Dushanba);

        // Assert
        Assert.True(monday.HasRestriction);
        Assert.Empty(monday.AllowedLessonNumbers);
    }
}
