using System.Windows;
using DarsJadvali.UI.ViewModels;

namespace DarsJadvali.UI;

/// <summary>Dastur asosiy oynasi.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();

        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            await _viewModel.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Dastlabki yuklashda xatolik yuz berdi.\n\n" + ex.Message,
                "Xatolik",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
