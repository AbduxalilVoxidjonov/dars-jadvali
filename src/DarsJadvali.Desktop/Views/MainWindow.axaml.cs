using Avalonia.Controls;
using DarsJadvali.Desktop.ViewModels;
using DarsJadvali.Domain.Common;

namespace DarsJadvali.Desktop.Views;

/// <summary>Dastur asosiy oynasi.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = AppInfo.AppName;
    }

    public MainWindow(MainViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }
}
