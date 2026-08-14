using System.Windows;
using System.Windows.Threading;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.DependencyInjection;
using DarsJadvali.Infrastructure.DependencyInjection;
using DarsJadvali.UI.Services;
using DarsJadvali.UI.ViewModels;
using DarsJadvali.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DarsJadvali.UI;

/// <summary>Dastur kirish nuqtasi — DI konteyner va boshlang'ich sozlamalar.</summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Biznes qatlamlari
        services.AddApplication();
        services.AddInfrastructureSqlite(InfrastructureServiceRegistration.DefaultDbPath);

        // UI infratuzilmasi
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModel'lar — har navigatsiyada yangi qamrov (scope) ichida yaratiladi
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TeachersViewModel>();
        services.AddTransient<SubjectsViewModel>();
        services.AddTransient<ClassGroupsViewModel>();
        services.AddTransient<AssignmentsViewModel>();
        services.AddTransient<WorkDaysViewModel>();
        services.AddTransient<AvailabilityViewModel>();
        services.AddTransient<TimetableViewModel>();
        services.AddTransient<AboutViewModel>();

        // Asosiy oyna
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            await _host.StartAsync().ConfigureAwait(true);

            using var scope = _host.Services.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Ma'lumotlar bazasini tayyorlashda xatolik yuz berdi.\n\n" +
                ex.Message +
                "\n\nDastur yopiladi.",
                "Xatolik",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        try
        {
            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Asosiy oynani ochishda xatolik yuz berdi.\n\n" + ex.Message,
                "Xatolik",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
            _host.Dispose();
        }
        catch
        {
            // Yopilish paytidagi xatolar e'tiborsiz qoldiriladi.
        }

        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Kutilmagan xatolik yuz berdi:\n\n" + e.Exception.Message,
            "Xatolik",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
