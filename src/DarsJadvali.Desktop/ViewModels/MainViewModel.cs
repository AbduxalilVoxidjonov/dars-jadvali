using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>
/// Asosiy oyna: chapda menyu, yuqorida o'quv yili va dars jadvali tanlagichi, o'ngda sahifa.
/// DI da <b>singleton</b> — boshqa sahifalar undan foydalanib jadvalga o'tishi mumkin
/// (<see cref="GoToTimetable"/>).
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// Tanlagichlar dastur tomonidan to'ldirilayotganda <c>true</c> bo'ladi —
    /// shunda tanlov o'zgarishi jadvalni faollashtirmaydi (aks holda tsikl paydo bo'ladi).
    /// </summary>
    private bool _suppressSelectorEvents;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private MenuItemModel? _selectedMenuItem;

    /// <summary>Yuqoridagi tanlagichda tanlangan o'quv yili.</summary>
    [ObservableProperty]
    private AcademicYear? _selectedAcademicYear;

    /// <summary>Yuqoridagi tanlagichda tanlangan (ya'ni faol) dars jadvali.</summary>
    [ObservableProperty]
    private Schedule? _selectedSchedule;

    public MainViewModel(INavigationService navigation, IDialogService dialogs, IServiceScopeFactory scopes)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));

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
            new("O'quv yillari", "CalendarStar", typeof(AcademicYearsViewModel)),
            new("Dastur haqida", "InformationOutline", typeof(AboutViewModel)),
        };

        _navigation.Navigated += OnNavigated;
    }

    /// <summary>Chap menyu bandlari.</summary>
    public ObservableCollection<MenuItemModel> MenuItems { get; }

    /// <summary>Yuqoridagi tanlagich uchun barcha o'quv yillari.</summary>
    public ObservableCollection<AcademicYear> AcademicYears { get; } = new();

    /// <summary>Tanlangan o'quv yili ichidagi dars jadvallari.</summary>
    public ObservableCollection<Schedule> Schedules { get; } = new();

    /// <summary>Amal bajarilmayotgan payt — tanlagichlar yoqiladi.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Sarlavhada ko'rsatiladigan dastur nomi.</summary>
    public string AppTitle => AppInfo.AppName;

    /// <summary>Sarlavhadagi versiya.</summary>
    public string AppVersion => "v" + AppInfo.Version;

    /// <summary>
    /// "Dars jadvali" sahifasi ochilganda avtomatik tanlanishi kerak bo'lgan sinf.
    /// <c>TimetableViewModel.LoadAsync</c> uni o'qiydi va <c>null</c> qilib tozalaydi.
    /// </summary>
    public int? PendingClassGroupId { get; set; }

    /// <summary>Dars jadvali sahifasiga o'tadi; sinf berilsa, o'sha sinf tanlanadi.</summary>
    public void GoToTimetable(int? classGroupId = null)
    {
        PendingClassGroupId = classGroupId;

        var item = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(TimetableViewModel));
        if (item is null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedMenuItem, item))
        {
            // Tanlov o'zgarishi navigatsiyani o'zi ishga tushiradi.
            SelectedMenuItem = item;
            return;
        }

        _ = NavigateAsync(item);
    }

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        // Avval faol jadval aniqlanadi — sahifalar o'sha jadval bo'yicha ma'lumot ko'rsatadi.
        await RefreshSelectorsAsync(ct).ConfigureAwait(true);
        SelectedMenuItem ??= MenuItems.FirstOrDefault();
    }

    /// <summary>
    /// Yuqoridagi tanlagichlarni bazadan qayta o'qiydi (faol jadval bo'yicha).
    /// "O'quv yillari" sahifasi biror narsani o'zgartirgach shuni chaqiradi.
    /// Bu yerda jadval faollashtirilmaydi va sahifa qayta yuklanmaydi.
    /// </summary>
    public async Task RefreshSelectorsAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var yearService = scope.ServiceProvider.GetRequiredService<IAcademicYearService>();
            var setService = scope.ServiceProvider.GetRequiredService<IScheduleSetService>();

            // Bazada hech narsa bo'lmasa — o'quv yili va "Asosiy jadval" avtomatik yaratiladi.
            var active = await setService.GetActiveAsync(ct).ConfigureAwait(true);
            var years = await yearService.GetAllAsync(ct).ConfigureAwait(true);
            var schedules = await setService
                .GetByAcademicYearAsync(active.AcademicYearId, ct).ConfigureAwait(true);

            _suppressSelectorEvents = true;
            try
            {
                AcademicYears.Clear();
                foreach (var year in years)
                {
                    AcademicYears.Add(year);
                }

                Schedules.Clear();
                foreach (var schedule in schedules)
                {
                    Schedules.Add(schedule);
                }

                SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.Id == active.AcademicYearId);
                SelectedSchedule = Schedules.FirstOrDefault(s => s.Id == active.Id);
            }
            finally
            {
                _suppressSelectorEvents = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            StatusMessage = "Jadval tanlagichini yuklab bo'lmadi.";
            await _dialogs.ErrorAsync(
                "O'quv yili va dars jadvallarini yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    partial void OnSelectedMenuItemChanged(MenuItemModel? value)
    {
        if (value is null)
        {
            return;
        }

        _ = NavigateAsync(value);
    }

    partial void OnSelectedAcademicYearChanged(AcademicYear? value)
    {
        if (_suppressSelectorEvents || value is null)
        {
            return;
        }

        _ = SwitchAcademicYearAsync(value.Id);
    }

    partial void OnSelectedScheduleChanged(Schedule? value)
    {
        if (_suppressSelectorEvents || value is null)
        {
            return;
        }

        _ = ActivateScheduleAsync(value);
    }

    /// <summary>O'quv yili almashdi: jadvallar ro'yxati yangilanadi va birinchisi tanlanadi.</summary>
    private async Task SwitchAcademicYearAsync(int academicYearId)
    {
        IReadOnlyList<Schedule> schedules;

        try
        {
            IsBusy = true;
            using var scope = _scopes.CreateScope();
            var setService = scope.ServiceProvider.GetRequiredService<IScheduleSetService>();
            schedules = await setService.GetByAcademicYearAsync(academicYearId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync(
                "Bu o'quv yilining jadvallarini yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        _suppressSelectorEvents = true;
        try
        {
            SelectedSchedule = null;
            Schedules.Clear();
            foreach (var schedule in schedules)
            {
                Schedules.Add(schedule);
            }
        }
        finally
        {
            _suppressSelectorEvents = false;
        }

        var first = Schedules.FirstOrDefault();
        if (first is null)
        {
            StatusMessage = "Bu o'quv yilida hali dars jadvali yo'q — «O'quv yillari» sahifasidan qo'shing.";
            return;
        }

        // Tanlash o'zi ActivateScheduleAsync ni ishga tushiradi.
        SelectedSchedule = first;
    }

    /// <summary>Tanlangan jadvalni faol qiladi va ochiq turgan sahifani qayta yuklaydi.</summary>
    private async Task ActivateScheduleAsync(Schedule schedule)
    {
        try
        {
            IsBusy = true;
            using var scope = _scopes.CreateScope();
            var setService = scope.ServiceProvider.GetRequiredService<IScheduleSetService>();
            await setService.SetActiveAsync(schedule.Id).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync(
                "Jadvalni almashtirishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        // Ochiq sahifa endi boshqa jadval ma'lumotini ko'rsatishi kerak.
        await NavigateAsync(SelectedMenuItem).ConfigureAwait(true);
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

            var viewModel = _navigation.NavigateToType(item.ViewModelType);
            await viewModel.LoadAsync().ConfigureAwait(true);

            StatusMessage = item.Title;
        }
        catch (Exception ex)
        {
            StatusMessage = "Sahifani ochib bo'lmadi.";
            await _dialogs.ErrorAsync(
                "Sahifani ochishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
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
