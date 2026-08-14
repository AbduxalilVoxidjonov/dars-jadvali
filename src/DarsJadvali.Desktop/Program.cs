using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace DarsJadvali.Desktop;

/// <summary>Dastur boshlanish nuqtasi.</summary>
internal static class Program
{
    /// <summary>Avtomatik sinov uchun: necha soniyadan keyin dastur o'zini yopishi.</summary>
    private const string AutoCloseVariable = "DARSJADVALI_AUTOCLOSE";

    [STAThread]
    public static int Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia previewer ham shu metodni chaqiradi — nomini o'zgartirmang.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        var seconds = ReadAutoCloseSeconds();
        if (seconds is > 0)
        {
            builder = builder.AfterSetup(_ => ScheduleAutoClose(seconds.Value));
        }

        return builder;
    }

    private static double? ReadAutoCloseSeconds()
    {
        var raw = Environment.GetEnvironmentVariable(AutoCloseVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static void ScheduleAutoClose(double seconds)
    {
        DispatcherTimer.RunOnce(
            () =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime
                    is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            },
            TimeSpan.FromSeconds(seconds));
    }
}
