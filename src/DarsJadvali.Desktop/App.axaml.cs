using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.DependencyInjection;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Desktop.ViewModels;
using DarsJadvali.Desktop.Views;
using DarsJadvali.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DarsJadvali.Desktop;

/// <summary>Dastur kirish nuqtasi — DI konteyner va boshlang'ich sozlamalar.</summary>
public partial class App : Avalonia.Application
{
    /// <summary>Baza faylini almashtirish uchun muhit o'zgaruvchisi (sinov uchun qulay).</summary>
    private const string DbPathVariable = "DARSJADVALI_DB";

    private IHost? _host;

    /// <summary>Ishga tushgan DI konteyner (kerak bo'lganda murojaat uchun).</summary>
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += OnExit;

            // Tutilmagan istisno dasturni yopib yubormasin — saqlanmagan ma'lumot yo'qolmasligi uchun.
            Dispatcher.UIThread.UnhandledException += OnUnhandledException;

            // Bazani tayyorlash asinxron — oyna faqat muvaffaqiyatli tayyorlangandan keyin ochiladi.
            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// DI konteynerni to'ldiradi. <c>public</c> — sinovlar barcha sahifa ViewModel'i
    /// haqiqatan yig'ilishini (bog'liqliklari ro'yxatdan o'tganini) tekshira olishi uchun.
    /// </summary>
    /// <param name="services">Servislar to'plami.</param>
    public static void ConfigureServices(IServiceCollection services)
    {
        // Biznes qatlamlari (Scoped)
        services.AddApplication();
        services.AddInfrastructureSqlite(ResolveDbPath());

        // Butun jadvalni qayta yozish / tozalash (ommaviy amal).
        services.AddScoped<IBoardCardRewriter, BoardCardRewriter>();

        // Sinf smenasi — ISchedulingStore.SetClassShiftAsync ustidagi yupqa servis.
        services.AddScoped<IClassShiftService, ClassShiftService>();

        // Reja sig'imi (aSc "Verify") — generatsiyadan oldingi ogohlantirish.
        services.AddScoped<IPlanCapacityService, PlanCapacityService>();

        // UI infratuzilmasi
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // Sahifa ViewModel'lari — har navigatsiyada yangi qamrov (scope) ichida yaratiladi
        // Jadval tahrirlash yadrosi — bosh sahifa ichida yashaydi (bir qamrov, bir DbContext).
        services.AddTransient<TimetableBoardViewModel>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TeachersViewModel>();
        services.AddTransient<SubjectsViewModel>();
        services.AddTransient<ClassGroupsViewModel>();
        services.AddTransient<AssignmentsViewModel>();
        services.AddTransient<WorkDaysViewModel>();
        services.AddTransient<AvailabilityViewModel>();
        services.AddTransient<TimetableViewModel>();
        services.AddTransient<AcademicYearsViewModel>();
        services.AddTransient<AscImportViewModel>();
        services.AddTransient<AboutViewModel>();

        // Asosiy oyna — boshqa sahifalar MainViewModel'ga tayanadi, shuning uchun singleton
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    /// <summary>Baza fayli yo'li: DARSJADVALI_DB berilgan bo'lsa o'sha, aks holda standart yo'l.</summary>
    private static string ResolveDbPath()
    {
        var custom = Environment.GetEnvironmentVariable(DbPathVariable);
        return string.IsNullOrWhiteSpace(custom)
            ? InfrastructureServiceRegistration.DefaultDbPath
            : custom;
    }

    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var dialogs = new DialogService();

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) => ConfigureServices(services))
                .Build();

            Services = _host.Services;

            await _host.StartAsync().ConfigureAwait(true);

            using var scope = _host.Services.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await dialogs.ErrorAsync(
                "Ma'lumotlar bazasini tayyorlashda xatolik yuz berdi.\n\n" +
                ex.Message +
                "\n\nDastur yopiladi.",
                "Xatolik").ConfigureAwait(true);

            desktop.Shutdown(-1);
            return;
        }

        try
        {
            var window = _host.Services.GetRequiredService<MainWindow>();
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            window.DataContext = mainViewModel;

            desktop.MainWindow = window;
            window.Show();

            await mainViewModel.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await dialogs.ErrorAsync(
                "Asosiy oynani ochishda xatolik yuz berdi.\n\n" + ex.Message,
                "Xatolik").ConfigureAwait(true);

            desktop.Shutdown(-1);
        }
    }

    /// <summary>
    /// UI oqimida tutilmagan istisno yuz berganda xato oynasini ko'rsatadi va dasturni tirik
    /// qoldiradi. Aks holda Avalonia jarayonni butunlay yopadi va foydalanuvchi saqlamagan
    /// ma'lumotini yo'qotadi.
    /// </summary>
    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        try
        {
            _ = new DialogService().ErrorAsync(
                "Kutilmagan xatolik yuz berdi:\n\n" + e.Exception.Message,
                "Xatolik");
        }
        catch
        {
            // Xato oynasining o'zi ochilmasa ham dastur yopilmasligi kerak.
        }
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            _host?.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host?.Dispose();
        }
        catch
        {
            // Yopilish paytidagi xatolar e'tiborsiz qoldiriladi.
        }
        finally
        {
            _host = null;
            Services = null;
        }
    }
}
