using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Common;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Dastur haqida sahifasi.</summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IUpdateChecker _updateChecker;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCopyFeedback))]
    private string _copyFeedback = string.Empty;

    /// <summary>Tekshiruv davom etyaptimi.</summary>
    [ObservableProperty]
    private bool _isCheckingUpdate;

    /// <summary>Yangilanish haqidagi xabar.</summary>
    [ObservableProperty]
    private string _updateMessage = string.Empty;

    /// <summary>Yangi versiya bormi — diqqatni tortadigan blok shu bilan ko'rinadi.</summary>
    [ObservableProperty]
    private bool _hasUpdate;

    /// <summary>O'rnatilgan versiya eng so'nggisimi.</summary>
    [ObservableProperty]
    private bool _isUpToDate;

    /// <summary>Reliz yo'q yoki tekshirib bo'lmadi — kichik, bezovta qilmaydigan izoh.</summary>
    [ObservableProperty]
    private bool _hasUpdateNote;

    /// <summary>Reliz izohi (qisqartirilgan).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReleaseNotes))]
    private string _releaseNotes = string.Empty;

    private string? _releaseUrl;

    public AboutViewModel(IDialogService dialogs, IUpdateChecker updateChecker)
    {
        _dialogs = dialogs;
        _updateChecker = updateChecker;
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

    /// <summary>Nusxa olindi degan xabar ko'rinadimi.</summary>
    public bool HasCopyFeedback => !string.IsNullOrEmpty(CopyFeedback);

    /// <summary>Reliz izohi bormi.</summary>
    public bool HasReleaseNotes => !string.IsNullOrEmpty(ReleaseNotes);

    public override Task LoadAsync(CancellationToken ct = default)
    {
        CopyFeedback = string.Empty;
        StatusMessage = AppInfo.AppName + " — " + VersionText;

        // Tarmoq so'rovi sahifa ochilishini kechiktirmasligi kerak — fon rejimida ketadi.
        _ = CheckUpdateCommand.ExecuteAsync(null);

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenTelegramAsync()
        => await OpenUrlAsync(AppInfo.TelegramUrl);

    /// <summary>Yangilanishni tekshiradi. Xato bo'lsa ham dialog ochilmaydi.</summary>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        IsCheckingUpdate = true;
        HasUpdate = false;
        IsUpToDate = false;
        HasUpdateNote = false;
        ReleaseNotes = string.Empty;
        _releaseUrl = null;
        UpdateMessage = "Yangilanish tekshirilmoqda...";

        try
        {
            var result = await _updateChecker.CheckAsync();

            UpdateMessage = result.Message;
            _releaseUrl = result.ReleaseUrl;
            ReleaseNotes = result.ReleaseNotes ?? string.Empty;

            switch (result.Status)
            {
                case UpdateStatus.UpdateAvailable:
                    HasUpdate = true;
                    StatusMessage = result.Message;
                    break;

                case UpdateStatus.UpToDate:
                    IsUpToDate = true;
                    break;

                default:
                    // NoRelease va Failed — ikkalasi ham kichik izoh sifatida ko'rsatiladi.
                    HasUpdateNote = true;
                    ReleaseNotes = string.Empty;
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            UpdateMessage = string.Empty;
        }
        catch (Exception)
        {
            // Yangilanish tekshiruvi hech qachon dasturni bezovta qilmasligi kerak.
            HasUpdateNote = true;
            UpdateMessage = "Yangilanishni tekshirib bo'lmadi.";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    /// <summary>Reliz sahifasini brauzerda ochadi.</summary>
    [RelayCommand]
    private async Task DownloadUpdateAsync()
        => await OpenUrlAsync(string.IsNullOrWhiteSpace(_releaseUrl) ? AppInfo.ReleasesUrl : _releaseUrl);

    /// <summary>Havolani tizim brauzerida ochadi (macOS/Windows/Linux).</summary>
    private async Task OpenUrlAsync(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync(
                "Havolani ochib bo'lmadi.\n\nManzil: " + url + "\n\n" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task CopyCardNumberAsync()
    {
        try
        {
            await _dialogs.CopyToClipboardAsync(AppInfo.DonateCardNumber);
            CopyFeedback = "Nusxalandi!";
            StatusMessage = "Karta raqami nusxalandi.";
        }
        catch (Exception ex)
        {
            CopyFeedback = string.Empty;
            await _dialogs.ErrorAsync("Nusxa olishda xatolik yuz berdi.\n\n" + ex.Message);
        }
    }

}
