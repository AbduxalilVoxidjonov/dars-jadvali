using System.Windows;
using DarsJadvali.Application.Validation;

namespace DarsJadvali.UI.Services;

/// <summary>MessageBox asosidagi sodda muloqot xizmati.</summary>
public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title = "Tasdiqlang")
    {
        return Invoke(() => MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes);
    }

    public void Info(string message, string title = "Ma'lumot")
    {
        Invoke(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
    }

    public void Error(string message, string title = "Xatolik")
    {
        Invoke(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
    }

    public void ShowValidation(ValidationResult result, string title = "Tekshiruv natijasi")
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Conflicts.Count == 0)
        {
            Info("Hech qanday muammo topilmadi.", title);
            return;
        }

        var image = result.IsValid ? MessageBoxImage.Warning : MessageBoxImage.Error;
        var header = result.IsValid
            ? "Ogohlantirishlar mavjud:"
            : "Quyidagi to'siqlar aniqlandi:";

        Invoke(() => MessageBox.Show(
            header + Environment.NewLine + Environment.NewLine + result.ToDisplayText(),
            title,
            MessageBoxButton.OK,
            image));
    }

    private static T Invoke<T>(Func<T> action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.Invoke(action);
    }

    private static void Invoke(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
