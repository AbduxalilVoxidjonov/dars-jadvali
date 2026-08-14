using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Domain.Common;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Dastur haqida sahifasi.</summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string copyFeedback = string.Empty;

    public AboutViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    /// <summary>Dastur nomi.</summary>
    public string AppName => AppInfo.AppName;

    /// <summary>Versiya.</summary>
    public string Version => AppInfo.Version;

    /// <summary>Versiya matni.</summary>
    public string VersionText => "Versiya " + AppInfo.Version;

    /// <summary>Qisqacha tavsif.</summary>
    public string Description => AppInfo.Description;

    /// <summary>Muallif.</summary>
    public string Author => AppInfo.Author;

    /// <summary>Telegram manzili (@ bilan).</summary>
    public string TelegramHandle => AppInfo.TelegramHandle;

    /// <summary>Telegram havolasi.</summary>
    public string TelegramUrl => AppInfo.TelegramUrl;

    /// <summary>Karta turi (Humo).</summary>
    public string DonateCardType => AppInfo.DonateCardType;

    /// <summary>Karta raqami.</summary>
    public string DonateCardNumber => AppInfo.DonateCardNumber;

    /// <summary>Karta egasi.</summary>
    public string DonateCardHolder => AppInfo.DonateCardHolder;

    public override Task LoadAsync(CancellationToken ct = default)
    {
        StatusMessage = AppInfo.AppName + " — " + VersionText;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenTelegram()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.TelegramUrl)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _dialogs.Error(
                "Havolani ochib bo'lmadi.\n\nManzil: " + AppInfo.TelegramUrl + "\n\n" + ex.Message);
        }
    }

    [RelayCommand]
    private void CopyCardNumber()
    {
        try
        {
            Clipboard.SetText(AppInfo.DonateCardNumber);
            CopyFeedback = "Nusxalandi!";
            StatusMessage = "Karta raqami nusxalandi.";
        }
        catch (Exception ex)
        {
            CopyFeedback = string.Empty;
            _dialogs.Error("Nusxa olishda xatolik yuz berdi.\n\n" + ex.Message);
        }
    }
}
