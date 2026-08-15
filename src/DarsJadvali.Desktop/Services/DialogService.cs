using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Views;

namespace DarsJadvali.Desktop.Services;

/// <summary>
/// <see cref="DialogWindow"/> asosidagi muloqot xizmati.
/// Tashqi paketlar ishlatilmaydi — Avalonia'ning o'z modal oynasi.
/// </summary>
public sealed class DialogService : IDialogService
{
    private const string TextColor = "#212121";
    private const string ErrorColor = "#C62828";
    private const string WarningColor = "#EF6C00";
    private const string SuccessColor = "#2E7D32";

    public Task InfoAsync(string message, string title = "Ma'lumot")
        => ShowAsync(new DialogModel
        {
            Title = title,
            Message = message,
            MessageColorCode = TextColor,
            PrimaryText = "Yopish",
        });

    public Task ErrorAsync(string message, string title = "Xato")
        => ShowAsync(new DialogModel
        {
            Title = title,
            Message = message,
            MessageColorCode = ErrorColor,
            PrimaryText = "Yopish",
        });

    public Task<bool> ConfirmAsync(string message, string title = "Tasdiqlang")
        => ShowAsync(new DialogModel
        {
            Title = title,
            Message = message,
            MessageColorCode = TextColor,
            PrimaryText = "Ha",
            SecondaryText = "Yo'q",
        });

    public Task ShowValidationAsync(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Conflicts.Count == 0)
        {
            return ShowAsync(new DialogModel
            {
                Title = "Tekshiruv natijasi",
                Message = "Hech qanday muammo topilmadi.",
                MessageColorCode = SuccessColor,
                PrimaryText = "Yopish",
            });
        }

        return ShowAsync(new DialogModel
        {
            Title = "Tekshiruv natijasi",
            Message = result.IsValid
                ? "Ogohlantirishlar mavjud:"
                : "Quyidagi to'siqlar aniqlandi:",
            MessageColorCode = TextColor,
            Lines = BuildLines(result),
            PrimaryText = "Yopish",
        });
    }

    public Task<bool> ConfirmWarningsAsync(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Conflicts.Count == 0)
        {
            return Task.FromResult(true);
        }

        return ShowAsync(new DialogModel
        {
            Title = "Ogohlantirish",
            Message = "Quyidagi ogohlantirishlar bor:",
            MessageColorCode = TextColor,
            Lines = BuildLines(result),
            PrimaryText = "Baribir qo'yilsin",
            SecondaryText = "Bekor qilish",
        });
    }

    public async Task CopyToClipboardAsync(string text)
    {
        if (text is null)
        {
            return;
        }

        await RunOnUiAsync(async () =>
        {
            var owner = GetOwner();
            var clipboard = owner is null ? null : TopLevel.GetTopLevel(owner)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(text).ConfigureAwait(true);
            }
        }).ConfigureAwait(false);
    }

    public Task<string?> SaveFileAsync(
        string suggestedFileName,
        string filterName = "PDF hujjat",
        string extension = "pdf")
    {
        var ext = string.IsNullOrWhiteSpace(extension) ? "pdf" : extension.TrimStart('.');

        return RunOnUiAsync(async () =>
        {
            var owner = GetOwner();
            var top = owner is null ? null : TopLevel.GetTopLevel(owner);
            if (top?.StorageProvider is null)
            {
                return null;
            }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Faylni saqlash",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = ext,
                ShowOverwritePrompt = true,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(filterName)
                    {
                        Patterns = new[] { "*." + ext },
                    },
                },
            }).ConfigureAwait(true);

            if (file is null)
            {
                return null;
            }

            return file.TryGetLocalPath() ?? file.Path.LocalPath;
        });
    }

    public Task<string?> OpenFileAsync(
        string title = "Faylni tanlang",
        string filterName = "XML fayl",
        string extension = "xml")
    {
        var ext = string.IsNullOrWhiteSpace(extension) ? "xml" : extension.TrimStart('.');

        return RunOnUiAsync(async () =>
        {
            var owner = GetOwner();
            var top = owner is null ? null : TopLevel.GetTopLevel(owner);
            if (top?.StorageProvider is null)
            {
                return null;
            }

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(filterName)
                    {
                        Patterns = new[] { "*." + ext },
                    },
                },
            }).ConfigureAwait(true);

            if (files.Count == 0)
            {
                return null;
            }

            var picked = files[0];
            return picked.TryGetLocalPath() ?? picked.Path.LocalPath;
        });
    }

    private static IReadOnlyList<DialogLine> BuildLines(ValidationResult result)
    {
        var lines = new List<DialogLine>(result.Conflicts.Count);
        foreach (var conflict in result.Conflicts)
        {
            lines.Add(new DialogLine
            {
                Text = "• " + conflict.Message,
                ColorCode = conflict.Severity == ConflictSeverity.Error ? ErrorColor : WarningColor,
            });
        }

        return lines;
    }

    private static Task<bool> ShowAsync(DialogModel model)
        => RunOnUiAsync(() => ShowCoreAsync(model));

    private static async Task<bool> ShowCoreAsync(DialogModel model)
    {
        var window = new DialogWindow(model);
        var owner = GetOwner();

        if (owner is null)
        {
            // Asosiy oyna hali ochilmagan (masalan, ishga tushishdagi xato) — egasiz ko'rsatamiz.
            var tcs = new TaskCompletionSource<bool>();
            window.Closed += (_, _) => tcs.TrySetResult(window.Result);
            window.Show();
            return await tcs.Task.ConfigureAwait(true);
        }

        return await window.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private static Window? GetOwner()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is { } main && main.IsVisible)
            {
                return main;
            }

            for (var i = desktop.Windows.Count - 1; i >= 0; i--)
            {
                var candidate = desktop.Windows[i];
                if (candidate.IsVisible)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Amalni UI oqimida bajaradi (ViewModel'lar fon oqimidan chaqirishi mumkin).</summary>
    private static Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        var tcs = new TaskCompletionSource<T>();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                tcs.TrySetResult(await action().ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private static Task RunOnUiAsync(Func<Task> action)
        => RunOnUiAsync(async () =>
        {
            await action().ConfigureAwait(true);
            return true;
        });
}
