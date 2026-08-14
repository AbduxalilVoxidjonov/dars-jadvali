using CommunityToolkit.Mvvm.ComponentModel;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Barcha sahifa ViewModel'lari uchun asos.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Uzoq davom etadigan amal bajarilayotganini bildiradi.</summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>Foydalanuvchiga ko'rsatiladigan holat matni.</summary>
    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>Sahifa ochilganda chaqiriladigan yuklash amali.</summary>
    public virtual Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
}
