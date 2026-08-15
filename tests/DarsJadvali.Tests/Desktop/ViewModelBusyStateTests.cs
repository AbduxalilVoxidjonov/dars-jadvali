using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Desktop.ViewModels;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// M-02: yozuv amali bajarilayotganda buyruq qayta ishga tushmasligi kerak
/// ("Qo'yish" ni ikki marta bosish = ikkita parallel yozuv).
/// </summary>
public sealed class ViewModelBusyStateTests
{
    [Fact]
    public void IsBusy_yoqilganda_buyruq_bloklanadi()
    {
        var vm = new TestPageViewModel();

        Assert.True(vm.IsNotBusy);
        Assert.True(vm.SaveCommand.CanExecute(null));

        vm.IsBusy = true;

        Assert.False(vm.IsNotBusy);
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.IsBusy = false;

        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void IsBusy_ozgarganda_CanExecuteChanged_hodisasi_chiqadi()
    {
        var vm = new TestPageViewModel();
        var raised = 0;

        vm.SaveCommand.CanExecuteChanged += (_, _) => raised++;

        vm.IsBusy = true;
        vm.IsBusy = false;

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task Amal_davomida_buyruq_ikkinchi_marta_ishga_tushmaydi()
    {
        var vm = new TestPageViewModel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Gate = gate.Task;

        // Birinchi bosish.
        var first = vm.SaveCommand.ExecuteAsync(null);

        // Amal davom etyapti — tugma o'chgan bo'lishi kerak.
        Assert.False(vm.SaveCommand.CanExecute(null));

        gate.SetResult();
        await first;

        Assert.Equal(1, vm.SaveCount);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Amallar_navbat_orqali_ketma_ket_bajariladi()
    {
        var vm = new TestPageViewModel();

        await vm.RunManyAsync();

        Assert.Equal(1, vm.MaxParallel);
    }

    /// <summary>Sinov uchun soddalashtirilgan sahifa ViewModel'i (Avalonia'siz).</summary>
    private sealed class TestPageViewModel : ViewModelBase
    {
        private readonly object _sync = new();
        private int _active;

        public TestPageViewModel()
        {
            // Manba generatori o'rniga qo'lda: [RelayCommand(CanExecute = nameof(IsNotBusy))] bilan bir xil.
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsNotBusy);
        }

        public IAsyncRelayCommand SaveCommand { get; }

        public Task? Gate { get; set; }

        public int SaveCount { get; private set; }

        public int MaxParallel { get; private set; }

        /// <summary>Bir necha amalni birdaniga yuboradi — ular kesishmasligi kerak.</summary>
        public async Task RunManyAsync()
        {
            var tasks = Enumerable
                .Range(0, 5)
                .Select(_ => RunExclusiveAsync(TrackAsync))
                .ToArray();

            await Task.WhenAll(tasks);
        }

        private async Task SaveAsync()
        {
            try
            {
                IsBusy = true;

                if (Gate is not null)
                {
                    await Gate;
                }

                SaveCount++;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TrackAsync(CancellationToken ct)
        {
            lock (_sync)
            {
                _active++;
                MaxParallel = Math.Max(MaxParallel, _active);
            }

            try
            {
                await Task.Delay(30, ct);
            }
            catch (OperationCanceledException)
            {
                // Yangi amal keldi — bu kutilgan holat.
            }
            finally
            {
                lock (_sync)
                {
                    _active--;
                }
            }
        }
    }
}
