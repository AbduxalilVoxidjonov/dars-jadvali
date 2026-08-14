using DarsJadvali.UI.ViewModels;

namespace DarsJadvali.UI.Services;

/// <summary>Sahifalar orasida o'tish xizmati.</summary>
public interface INavigationService
{
    /// <summary>Hozirgi ko'rsatilayotgan ViewModel.</summary>
    ViewModelBase? Current { get; }

    /// <summary>Yangi sahifaga o'tilganda ishlaydi.</summary>
    event EventHandler<ViewModelBase>? Navigated;

    /// <summary>Turi bo'yicha sahifaga o'tadi va yangi ViewModel qaytaradi.</summary>
    ViewModelBase NavigateTo(Type viewModelType);

    /// <summary>Umumlashtirilgan ko'rinish.</summary>
    ViewModelBase NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
}
