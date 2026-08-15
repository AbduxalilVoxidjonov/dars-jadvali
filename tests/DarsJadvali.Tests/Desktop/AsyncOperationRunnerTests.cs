using DarsJadvali.Desktop.Services;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// M-01: bitta <c>DbContext</c> ustida ikkita amal kesishmasligi kerak.
/// Shu xatti-harakat <see cref="AsyncOperationRunner"/> darajasida tekshiriladi.
/// </summary>
public sealed class AsyncOperationRunnerTests
{
    [Fact]
    public async Task RunAsync_amallarni_ketma_ket_bajaradi()
    {
        using var runner = new AsyncOperationRunner();

        var active = 0;
        var maxActive = 0;
        var completed = 0;
        var sync = new object();

        async Task OperationAsync(CancellationToken ct)
        {
            lock (sync)
            {
                active++;
                maxActive = Math.Max(maxActive, active);
            }

            await Task.Delay(20, CancellationToken.None);

            lock (sync)
            {
                active--;
                completed++;
            }
        }

        // Amallar ketma-ket kutiladi — kesishmasligi shart.
        for (var i = 0; i < 5; i++)
        {
            await runner.RunAsync(OperationAsync);
        }

        Assert.Equal(1, maxActive);
        Assert.Equal(5, completed);
    }

    [Fact]
    public async Task Bir_vaqtda_yuborilgan_amallar_ham_kesishmaydi()
    {
        using var runner = new AsyncOperationRunner();

        var active = 0;
        var maxActive = 0;
        var sync = new object();

        async Task OperationAsync(CancellationToken ct)
        {
            lock (sync)
            {
                active++;
                maxActive = Math.Max(maxActive, active);
            }

            try
            {
                await Task.Delay(50, ct);
            }
            catch (OperationCanceledException)
            {
                // Bekor qilindi — bu kutilgan holat.
            }
            finally
            {
                lock (sync)
                {
                    active--;
                }
            }
        }

        // Foydalanuvchi tanlagichni tez-tez almashtirgandagi holat.
        var tasks = Enumerable.Range(0, 6).Select(_ => runner.RunAsync(OperationAsync)).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxActive);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task Yangi_amal_oldingisini_bekor_qiladi_va_oxirgisi_golib_boladi()
    {
        using var runner = new AsyncOperationRunner();

        var firstCancelled = false;
        var secondFinished = false;
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = runner.RunAsync(async ct =>
        {
            firstStarted.TrySetResult();

            try
            {
                await Task.Delay(5_000, ct);
            }
            catch (OperationCanceledException)
            {
                firstCancelled = true;
                throw;
            }
        });

        await firstStarted.Task;

        var second = runner.RunAsync(ct =>
        {
            secondFinished = true;
            return Task.CompletedTask;
        });

        await Task.WhenAll(first, second);

        Assert.True(firstCancelled, "Oldingi amal bekor qilinishi kerak edi.");
        Assert.True(secondFinished, "Oxirgi amal bajarilishi kerak edi.");
    }

    [Fact]
    public async Task Amal_ichidan_chaqirilsa_ozini_ozi_bloklamaydi()
    {
        using var runner = new AsyncOperationRunner();

        var innerRan = false;

        // "Qo'yish" amali oxirida to'rni yangilash — ichkarida navbat kutilmasligi kerak.
        var outer = runner.RunAsync(async ct =>
        {
            await runner.RunAsync(inner =>
            {
                innerRan = true;
                return Task.CompletedTask;
            });
        });

        var completed = await Task.WhenAny(outer, Task.Delay(3_000));

        Assert.Same(outer, completed);
        Assert.True(innerRan);
    }

    [Fact]
    public async Task CancelAndWaitAsync_amal_tugashini_kutadi()
    {
        using var runner = new AsyncOperationRunner();

        var finished = false;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = runner.RunAsync(async ct =>
        {
            started.TrySetResult();

            try
            {
                await Task.Delay(5_000, ct);
            }
            catch (OperationCanceledException)
            {
                // Tozalash ishlari tugaguncha kutilishi kerak.
                await Task.Delay(30, CancellationToken.None);
                finished = true;
                throw;
            }
        });

        await started.Task;

        // M-03: sahifadan chiqishdan oldin — eski ish tugashi SHART.
        await runner.CancelAndWaitAsync();

        Assert.True(finished, "Amal tugamasdan turib kutish tugamasligi kerak edi.");
        await running;
    }

    [Fact]
    public async Task Tashqi_token_bekor_qilinsa_amal_ishga_tushmaydi()
    {
        using var runner = new AsyncOperationRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ran = false;

        await runner.RunAsync(ct =>
        {
            ran = true;
            return Task.CompletedTask;
        }, cts.Token);

        Assert.False(ran);
    }
}
