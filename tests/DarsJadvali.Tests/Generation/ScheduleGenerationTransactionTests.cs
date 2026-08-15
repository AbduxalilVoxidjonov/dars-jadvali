using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Scheduling;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DarsJadvali.Tests.Generation;

/// <summary>
/// 05-audit K-04: eski generator eski jadvalni <b>commit bilan</b> o'chirib, keyin yangisini
/// yozardi — o'rtada xato bo'lsa foydalanuvchi butun jadvalini yo'qotardi.
/// Bu yerda yozish o'rtasida sun'iy xato hosil qilinadi va eski jadval joyida qolishi
/// isbotlanadi.
/// </summary>
public class ScheduleGenerationTransactionTests
{
    /// <summary>Kartochka yozish bosqichida ataylab yiqiladigan store.</summary>
    private sealed class FailingStore : ISchedulingStore
    {
        private readonly ISchedulingStore _inner;

        public FailingStore(ISchedulingStore inner) => _inner = inner;

        public bool DeleteCalled { get; private set; }

        public Task<SchedulingInput> LoadAsync(int scheduleId, CancellationToken ct = default)
            => _inner.LoadAsync(scheduleId, ct);

        public async Task<int> DeleteCardsAsync(int scheduleId, bool keepLocked, CancellationToken ct = default)
        {
            DeleteCalled = true;
            return await _inner.DeleteCardsAsync(scheduleId, keepLocked, ct);
        }

        public Task<IReadOnlyList<int>> InsertCardsAsync(
            IReadOnlyList<CardWrite> cards, CancellationToken ct = default)
            => throw new InvalidOperationException("Sun'iy xato: kartochkalarni yozib bo'lmadi.");

        public Task<IReadOnlyList<PlacedCardView>> LoadPlacedCardsAsync(
            int scheduleId, CancellationToken ct = default)
            => _inner.LoadPlacedCardsAsync(scheduleId, ct);

        // --- jadval to'ri (UI) uchun o'qish/yozish: shu testda ishlatilmaydi ---

        public Task<IReadOnlyList<CardView>> LoadCardViewsAsync(
            int scheduleId, CancellationToken ct = default)
            => _inner.LoadCardViewsAsync(scheduleId, ct);

        public Task<IReadOnlyList<UnplacedLessonView>> LoadUnplacedLessonsAsync(
            int scheduleId, CancellationToken ct = default)
            => _inner.LoadUnplacedLessonsAsync(scheduleId, ct);

        public Task<IReadOnlyList<CardOccupancy>> LoadOccupancyAsync(
            int scheduleId, CancellationToken ct = default)
            => _inner.LoadOccupancyAsync(scheduleId, ct);

        public Task<bool> MoveCardAsync(
            int cardId, int dayNo, int periodId, int? weeksMask, CancellationToken ct = default)
            => _inner.MoveCardAsync(cardId, dayNo, periodId, weeksMask, ct);

        public Task<int> MoveCardsAsync(
            IReadOnlyList<CardPlacement> placements, CancellationToken ct = default)
            => _inner.MoveCardsAsync(placements, ct);

        public Task<bool> SetCardLockAsync(int cardId, bool isLocked, CancellationToken ct = default)
            => _inner.SetCardLockAsync(cardId, isLocked, ct);

        public Task<bool> DeleteCardAsync(int cardId, CancellationToken ct = default)
            => _inner.DeleteCardAsync(cardId, ct);

        public Task<bool> SetClassShiftAsync(
            int schoolClassId, int? shiftId, CancellationToken ct = default)
            => _inner.SetClassShiftAsync(schoolClassId, shiftId, ct);
    }

    private static async Task<(GenerationWorld World, int Cards, int Occurrences)> SeedExistingScheduleAsync()
    {
        var world = new GenerationWorld();
        var teacher = world.AddTeacher("Aliyev Vali");
        var subject = world.AddSubject("Matematika", "MAT");
        var cls = world.AddClass("5-A");
        var lesson = world.AddLesson(subject, teacher, cls, periodsPerWeek: 3);

        // Qo'lda tuzilgan "eski" jadval.
        world.AddCard(lesson, dayNo: 0, periodNo: 1);
        world.AddCard(lesson, dayNo: 1, periodNo: 1);
        world.AddCard(lesson, dayNo: 2, periodNo: 1);
        await world.Projector().RebuildForScheduleAsync(world.Schedule.Id);

        var cards = await world.Context.Cards.CountAsync();
        var occurrences = await world.Context.CardOccurrences.CountAsync();
        return (world, cards, occurrences);
    }

    [Fact]
    public async Task Yozish_ortasida_xato_bolsa_eski_jadval_joyida_qoladi()
    {
        // Arrange
        var (world, cardsBefore, occurrencesBefore) = await SeedExistingScheduleAsync();
        using var _ = world;

        Assert.Equal(3, cardsBefore);
        Assert.True(occurrencesBefore > 0);

        var faulty = new FailingStore(world.Store());
        var service = world.Service(faulty);

        // Act — yozish bosqichida sun'iy xato.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(new ScheduleGenerationOptions { Seed = 42 }));

        // Assert — o'chirish BAJARILGAN edi, lekin tranzaksiya uni ham qaytardi.
        Assert.True(faulty.DeleteCalled, "O'chirish bosqichi umuman bajarilmadi — test ma'nosini yo'qotadi.");

        world.Context.ChangeTracker.Clear();
        Assert.Equal(cardsBefore, await world.Context.Cards.CountAsync());
        Assert.Equal(occurrencesBefore, await world.Context.CardOccurrences.CountAsync());

        var days = await world.Context.Cards.AsNoTracking().Select(c => c.DayNo).OrderBy(d => d).ToListAsync();
        Assert.Equal(new[] { 0, 1, 2 }, days);
    }

    [Fact]
    public async Task Muvaffaqiyatli_generatsiya_eski_jadvalni_almashtiradi()
    {
        // Arrange
        var (world, cardsBefore, _) = await SeedExistingScheduleAsync();
        using var _2 = world;

        // Act
        var report = await world.Service().GenerateAsync(new ScheduleGenerationOptions { Seed = 42 });

        // Assert
        Assert.True(report.Applied, string.Join(" | ", report.Messages));
        Assert.True(report.Success, string.Join(" | ", report.HardViolations));
        Assert.Equal(3, report.PlacedCards);

        world.Context.ChangeTracker.Clear();
        Assert.Equal(cardsBefore, await world.Context.Cards.CountAsync());
        Assert.True(await world.Context.CardOccurrences.CountAsync() > 0);
        Assert.True(report.OccurrenceRows > 0);
    }

    [Fact]
    public async Task Bekor_qilinganda_hech_narsa_yozilmaydi()
    {
        // Arrange
        var (world, cardsBefore, occurrencesBefore) = await SeedExistingScheduleAsync();
        using var _ = world;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var report = await world.Service()
            .GenerateAsync(new ScheduleGenerationOptions { Seed = 7 }, progress: null, ct: cts.Token);

        // Assert — bekor qilish xato emas, lekin baza tegilmaydi.
        Assert.True(report.Cancelled);
        Assert.False(report.Applied);

        world.Context.ChangeTracker.Clear();
        Assert.Equal(cardsBefore, await world.Context.Cards.CountAsync());
        Assert.Equal(occurrencesBefore, await world.Context.CardOccurrences.CountAsync());
    }

    [Fact]
    public async Task Jarayon_ortasida_bekor_qilish_ham_bazani_tegmaydi()
    {
        // Arrange — birinchi progress xabarida bekor qilinadi.
        var (world, cardsBefore, _) = await SeedExistingScheduleAsync();
        using var _2 = world;

        using var cts = new CancellationTokenSource();
        var progress = new Progress<ScheduleGenerationProgress>(_ => cts.Cancel());

        // Act
        var report = await world.Service()
            .GenerateAsync(new ScheduleGenerationOptions { Seed = 7 }, progress, cts.Token);

        // Assert
        Assert.False(report.Applied);
        world.Context.ChangeTracker.Clear();
        Assert.Equal(cardsBefore, await world.Context.Cards.CountAsync());
    }
}
