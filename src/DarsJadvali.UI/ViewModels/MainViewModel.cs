using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Domain.Common;
using DarsJadvali.UI.Models;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Asosiy oyna: chap menyu va o'ngdagi sahifa.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ViewModelBase? currentViewModel;

    [ObservableProperty]
    private MenuItemModel? selectedMenuItem;

    public MainViewModel(INavigationService navigation, IDialogService dialogs)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        MenuItems = new ObservableCollection<MenuItemModel>
        {
            new("Bosh sahifa", "ViewDashboard", typeof(DashboardViewModel)),
            new("O'qituvchilar", "AccountTie", typeof(TeachersViewModel)),
            new("Fanlar", "BookOpenPageVariant", typeof(SubjectsViewModel)),
            new("Sinflar", "GoogleClassroom", typeof(ClassGroupsViewModel)),
            new("Biriktirmalar", "LinkVariant", typeof(AssignmentsViewModel)),
            new("Hafta kunlari", "CalendarWeek", typeof(WorkDaysViewModel)),
            new("O'qituvchi vaqti", "ClockOutline", typeof(AvailabilityViewModel)),
            new("Dars jadvali", "TableLarge", typeof(TimetableViewModel)),
            new("Dastur haqida", "InformationOutline", typeof(AboutViewModel)),
        };

        _navigation.Navigated += OnNavigated;
    }

    /// <summary>Chap menyu bandlari.</summary>
    public ObservableCollection<MenuItemModel> MenuItems { get; }

    /// <summary>Sarlavhada ko'rsatiladigan dastur nomi.</summary>
    public string AppTitle => AppInfo.AppName;

    /// <summary>Sarlavhadagi versiya.</summary>
    public string AppVersion => "v" + AppInfo.Version;

    public override Task LoadAsync(CancellationToken ct = default)
    {
        SelectedMenuItem ??= MenuItems.FirstOrDefault();
        return Task.CompletedTask;
    }

    partial void OnSelectedMenuItemChanged(MenuItemModel? value)
    {
        if (value is null)
        {
            return;
        }

        _ = NavigateAsync(value);
    }

    [RelayCommand]
    private async Task NavigateAsync(MenuItemModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = item.Title + " yuklanmoqda...";

            var viewModel = _navigation.NavigateTo(item.ViewModelType);
            await viewModel.LoadAsync().ConfigureAwait(true);

            StatusMessage = item.Title;
        }
        catch (Exception ex)
        {
            StatusMessage = "Sahifani ochib bo'lmadi.";
            _dialogs.Error("Sahifani ochishda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnNavigated(object? sender, ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
    }
}
