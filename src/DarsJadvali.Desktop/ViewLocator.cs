using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DarsJadvali.Desktop.ViewModels;

namespace DarsJadvali.Desktop;

/// <summary>
/// ViewModel'ni nom bo'yicha View bilan bog'laydi:
/// <c>DarsJadvali.Desktop.ViewModels.XxxViewModel</c> → <c>DarsJadvali.Desktop.Views.XxxView</c>.
/// <c>Application.DataTemplates</c> ga qo'shilgani uchun
/// <c>ContentControl Content="{Binding CurrentViewModel}"</c> avtomatik ishlaydi.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var fullName = param.GetType().FullName;
        if (string.IsNullOrEmpty(fullName))
        {
            return NotFound("Nomsiz ViewModel");
        }

        // "…ViewModels.XxxViewModel" → "…Views.XxxView" (ikkala o'rin ham almashadi)
        var viewName = fullName.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(viewName);

        if (type is null)
        {
            return NotFound(viewName + " topilmadi");
        }

        if (Activator.CreateInstance(type) is Control control)
        {
            return control;
        }

        return NotFound(viewName + " Control emas");
    }

    public bool Match(object? data) => data is ViewModelBase;

    private static TextBlock NotFound(string message) => new()
    {
        Text = "Sahifa ko'rinishi topilmadi: " + message,
        Margin = new Avalonia.Thickness(24),
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
    };
}
