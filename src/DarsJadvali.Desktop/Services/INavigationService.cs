using DarsJadvali.Desktop.ViewModels;

namespace DarsJadvali.Desktop.Services;

/// <summary>Sahifalar orasida o'tish xizmati.</summary>
public interface INavigationService
{
    /// <summary>Hozirgi ko'rsatilayotgan ViewModel.</summary>
    ViewModelBase? Current { get; }

    /// <summary>Turi bo'yicha sahifaga o'tadi.</summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>Yangi sahifaga o'tilganda ishlaydi.</summary>
    event EventHandler<ViewModelBase>? Navigated;

    /// <summary>
    /// Tur ob'ekti bo'yicha sahifaga o'tadi va yaratilgan ViewModel'ni qaytaradi
    /// (menyu bandlari <c>Type</c> saqlagani uchun kerak).
    /// </summary>
    ViewModelBase NavigateToType(Type viewModelType);
}
