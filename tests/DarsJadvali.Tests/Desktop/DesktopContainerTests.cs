using DarsJadvali.Desktop;
using DarsJadvali.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// Dasturning HAQIQIY DI konteyneri: har bir sahifa ViewModel'i yig'ila oladimi.
/// </summary>
/// <remarks>
/// Ilgari yangi bog'liqlik qo'shilganda (masalan <c>IClassShiftService</c>) uni
/// ro'yxatdan o'tkazish unutilsa, xato faqat foydalanuvchi o'sha sahifaga o'tganda
/// chiqardi. Endi buni sinov ushlaydi.
/// </remarks>
public sealed class DesktopContainerTests
{
    private static ServiceProvider Build(string dbPath)
    {
        // Baza fayli TEGILMAYDI: ViewModel yaratish ulanish ochmaydi.
        Environment.SetEnvironmentVariable("DARSJADVALI_DB", dbPath);

        var services = new ServiceCollection();
        App.ConfigureServices(services);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });
    }

    [Theory]
    [InlineData(typeof(DashboardViewModel))]
    [InlineData(typeof(ClassGroupsViewModel))]
    [InlineData(typeof(AssignmentsViewModel))]
    [InlineData(typeof(TeachersViewModel))]
    [InlineData(typeof(SubjectsViewModel))]
    [InlineData(typeof(WorkDaysViewModel))]
    [InlineData(typeof(AvailabilityViewModel))]
    [InlineData(typeof(TimetableViewModel))]
    [InlineData(typeof(TimetableBoardViewModel))]
    [InlineData(typeof(AcademicYearsViewModel))]
    [InlineData(typeof(AscImportViewModel))]
    [InlineData(typeof(AboutViewModel))]
    public void Har_bir_sahifa_ViewModeli_konteynerdan_yigiladi(Type viewModelType)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dj-di-{Guid.NewGuid():N}.db");
        var previous = Environment.GetEnvironmentVariable("DARSJADVALI_DB");

        try
        {
            using var provider = Build(dbPath);
            using var scope = provider.CreateScope();

            var viewModel = scope.ServiceProvider.GetRequiredService(viewModelType);

            Assert.IsAssignableFrom<ViewModelBase>(viewModel);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DARSJADVALI_DB", previous);
        }
    }

    /// <summary>
    /// Chap menyudagi HAR BIR band ochiladigan bo'lsin: bandi bor, lekin konteynerda
    /// ro'yxatdan o'tmagan sahifa foydalanuvchida "Sahifani ochib bo'lmadi" beradi.
    /// </summary>
    [Fact]
    public void Menyudagi_har_bir_band_ochiladi()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dj-di-{Guid.NewGuid():N}.db");
        var previous = Environment.GetEnvironmentVariable("DARSJADVALI_DB");

        try
        {
            using var provider = Build(dbPath);
            var main = provider.GetRequiredService<MainViewModel>();

            // aSc importi menyuda bor — aks holda foydalanuvchi importerga umuman yeta olmaydi.
            Assert.Contains(main.MenuItems, item => item.ViewModelType == typeof(AscImportViewModel));

            foreach (var item in main.MenuItems)
            {
                using var scope = provider.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService(item.ViewModelType);
                Assert.IsAssignableFrom<ViewModelBase>(viewModel);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DARSJADVALI_DB", previous);
        }
    }
}
