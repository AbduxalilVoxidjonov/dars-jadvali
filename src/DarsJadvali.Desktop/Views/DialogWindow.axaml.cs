using Avalonia.Controls;
using Avalonia.Interactivity;
using DarsJadvali.Desktop.Models;

namespace DarsJadvali.Desktop.Views;

/// <summary>Universal muloqot oynasi — ma'lumot, xato, tasdiqlash va validatsiya uchun.</summary>
public partial class DialogWindow : Window
{
    public DialogWindow()
    {
        InitializeComponent();
    }

    public DialogWindow(DialogModel model)
        : this()
    {
        ArgumentNullException.ThrowIfNull(model);
        DataContext = model;
        Title = model.Title;
    }

    /// <summary>Asosiy tugma bosilgan bo'lsa true.</summary>
    public bool Result { get; private set; }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close(true);
    }

    private void OnSecondaryClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close(false);
    }
}
