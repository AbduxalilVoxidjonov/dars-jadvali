using System.Collections.Concurrent;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Desktop.Services;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Barcha sahifa ViewModel'lari uchun asos.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Har bir ViewModel turi uchun buyruq xossalari bir marta topiladi.</summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> CommandPropertyCache = new();

    /// <summary>Uzoq davom etadigan amal bajarilayotganini bildiradi.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Foydalanuvchiga ko'rsatiladigan holat matni.</summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Amal bajarilmayotgan payt — tugmalar va tanlagichlar yoqiladi (M-02).</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Shu sahifaning barcha bazaga murojaat qiladigan amallarini ketma-ketlashtiradi (M-01).
    /// Sahifa o'z DI qamrovidagi bitta <c>DbContext</c> ni ishlatgani uchun bu majburiy.
    /// </summary>
    protected AsyncOperationRunner Operations { get; private set; } = new();

    /// <summary>
    /// Ichki (bolalik) ViewModel'ni <b>shu sahifaning navbatiga</b> ulaydi.
    /// </summary>
    /// <remarks>
    /// Bosh sahifa ichida jadval yadrosi kabi ikkinchi ViewModel bo'lsa, ikkalasi bitta DI
    /// qamrovidagi <b>bitta</b> <c>DbContext</c> ni ishlatadi. Alohida navbatlar kesishib
    /// M-01 ni qaytadan keltirib chiqarardi — shuning uchun navbat bo'lishiladi.
    /// </remarks>
    /// <param name="child">Ichki ViewModel.</param>
    public void ShareOperationQueueWith(ViewModelBase child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!ReferenceEquals(child, this))
        {
            child.Operations = Operations;
        }
    }

    /// <summary>Sahifa ochilganda chaqiriladigan yuklash amali.</summary>
    public virtual Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Sahifadan chiqishdan oldin chaqiriladi: davom etayotgan amal bekor qilinadi va
    /// tugashi kutiladi. Shundan keyingina DI qamrovini yopish xavfsiz (M-03).
    /// </summary>
    public Task CancelPendingWorkAsync(CancellationToken ct = default)
        => Operations.CancelAndWaitAsync(ct);

    /// <summary>
    /// Amalni navbat bilan bajaradi: oldingi amal bekor qilinadi, tugashi kutiladi,
    /// so'ng yangisi ishga tushadi.
    /// </summary>
    /// <param name="operation">Bajariladigan amal.</param>
    /// <param name="ct">Tashqi bekor qilish tokeni.</param>
    protected Task RunExclusiveAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
        => Operations.RunAsync(operation, ct);

    /// <summary>
    /// Barcha buyruqlardan <c>CanExecute</c> ni qayta so'raydi.
    /// Buyruqlar <c>[RelayCommand(CanExecute = nameof(IsNotBusy))]</c> deb belgilangani uchun
    /// amal boshlanganda tugmalar o'chadi, tugagach yana yonadi.
    /// </summary>
    protected void NotifyCommandsCanExecuteChanged()
    {
        foreach (var property in GetCommandProperties(GetType()))
        {
            if (property.GetValue(this) is IRelayCommand command)
            {
                command.NotifyCanExecuteChanged();
            }
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        NotifyCommandsCanExecuteChanged();
    }

    /// <summary>Turdagi barcha buyruq xossalarini qaytaradi (natija keshlanadi).</summary>
    private static PropertyInfo[] GetCommandProperties(Type type)
        => CommandPropertyCache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead &&
                        p.GetIndexParameters().Length == 0 &&
                        typeof(IRelayCommand).IsAssignableFrom(p.PropertyType))
            .ToArray());
}
