using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>
/// "O'quv yillari" sahifasi: o'quv yillari va ularning ichidagi dars jadvallari (variantlari).
/// Eski o'quv yillari saqlanib qoladi, har bir yilda bir nechta jadval bo'lishi mumkin.
/// </summary>
public sealed partial class AcademicYearsViewModel : ViewModelBase
{
    private readonly IAcademicYearService _years;
    private readonly IScheduleSetService _sets;
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;

    /// <summary>Ro'yxat dastur tomonidan to'ldirilayotganda tanlov hodisalari o'tkazib yuboriladi.</summary>
    private bool _suppressSelection;

    private int _editingYearId;
    private int _editingScheduleId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedYear))]
    [NotifyPropertyChangedFor(nameof(SelectedYearTitle))]
    private AcademicYear? _selectedYear;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSchedule))]
    private ScheduleRowViewModel? _selectedSchedule;

    [ObservableProperty]
    private bool _isYearEditing;

    [ObservableProperty]
    private string _yearEditorTitle = string.Empty;

    [ObservableProperty]
    private string _editYearName = string.Empty;

    [ObservableProperty]
    private string _editYearStartYear = string.Empty;

    [ObservableProperty]
    private string _editYearNote = string.Empty;

    [ObservableProperty]
    private bool _isScheduleEditing;

    [ObservableProperty]
    private string _scheduleEditorTitle = string.Empty;

    [ObservableProperty]
    private string _editScheduleName = string.Empty;

    /// <summary>Yangi ViewModel yaratadi.</summary>
    public AcademicYearsViewModel(
        IAcademicYearService years,
        IScheduleSetService sets,
        IDialogService dialogs,
        MainViewModel main)
    {
        _years = years ?? throw new ArgumentNullException(nameof(years));
        _sets = sets ?? throw new ArgumentNullException(nameof(sets));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _main = main ?? throw new ArgumentNullException(nameof(main));
    }

    /// <summary>O'quv yillari ro'yxati (yangisidan eskisiga).</summary>
    public ObservableCollection<AcademicYear> Years { get; } = new();

    /// <summary>Tanlangan o'quv yilining dars jadvallari.</summary>
    public ObservableCollection<ScheduleRowViewModel> Schedules { get; } = new();


    /// <summary>O'quv yili tanlanganmi (va amal bajarilmayaptimi).</summary>
    public bool HasSelectedYear => !IsBusy && SelectedYear is not null;

    /// <summary>Jadval tanlanganmi (va amal bajarilmayaptimi).</summary>
    public bool HasSelectedSchedule => !IsBusy && SelectedSchedule is not null;

    /// <summary>O'ng ustun sarlavhasi: qaysi yilning jadvallari ko'rsatilyapti.</summary>
    public string SelectedYearTitle => SelectedYear is null
        ? "Dars jadvallari"
        : $"«{SelectedYear.Name}» jadvallari";

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken ct = default)
        => RunExclusiveAsync(RefreshCoreAsync, ct);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(HasSelectedYear));
            OnPropertyChanged(nameof(HasSelectedSchedule));
        }
    }

    partial void OnSelectedYearChanged(AcademicYear? value)
    {
        if (_suppressSelection)
        {
            return;
        }

        IsScheduleEditing = false;

        // Setterdan `await` qilib bo'lmaydi — amal navbatga qo'yiladi (M-01).
        var yearId = value?.Id;
        _ = RunExclusiveAsync(ct => LoadSchedulesAsync(yearId, ct));
    }

    /// <summary>O'quv yillari ro'yxatini bazadan qayta o'qiydi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task RefreshAsync(CancellationToken ct = default)
        => RunExclusiveAsync(RefreshCoreAsync, ct);

    private async Task RefreshCoreAsync(CancellationToken ct)
    {
        var keepYearId = SelectedYear?.Id;

        try
        {
            IsBusy = true;
            var items = await _years.GetAllAsync(ct).ConfigureAwait(true);
            var activeId = await _sets.GetActiveIdAsync(ct).ConfigureAwait(true);
            var active = await _sets.GetByIdAsync(activeId, ct).ConfigureAwait(true);

            _suppressSelection = true;
            try
            {
                Years.Clear();
                foreach (var item in items)
                {
                    Years.Add(item);
                }

                SelectedYear =
                    Years.FirstOrDefault(y => y.Id == keepYearId)
                    ?? Years.FirstOrDefault(y => y.Id == active?.AcademicYearId)
                    ?? Years.FirstOrDefault();
            }
            finally
            {
                _suppressSelection = false;
            }

            StatusMessage = $"Jami {Years.Count} ta o'quv yili.";
            await LoadSchedulesAsync(SelectedYear?.Id, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("O'quv yillarini yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Tanlangan yilning jadvallarini (dars soni bilan) yuklaydi.</summary>
    private async Task LoadSchedulesAsync(int? academicYearId, CancellationToken ct = default)
    {
        var keepScheduleId = SelectedSchedule?.Id;

        try
        {
            IsBusy = true;

            Schedules.Clear();
            SelectedSchedule = null;

            if (academicYearId is null)
            {
                return;
            }

            var items = await _sets.GetByAcademicYearAsync(academicYearId.Value, ct).ConfigureAwait(true);
            foreach (var item in items)
            {
                var count = await _sets.GetEntryCountAsync(item.Id, ct).ConfigureAwait(true);
                Schedules.Add(new ScheduleRowViewModel(item, count));
            }

            SelectedSchedule =
                Schedules.FirstOrDefault(s => s.Id == keepScheduleId)
                ?? Schedules.FirstOrDefault(s => s.IsActive)
                ?? Schedules.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Dars jadvallarini yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ================= O'quv yili amallari =================

    /// <summary>Yangi o'quv yili qo'shish shaklini ochadi.</summary>
    [RelayCommand]
    private void NewYear()
    {
        _editingYearId = 0;
        YearEditorTitle = "Yangi o'quv yili";

        var startYear = Years.Count > 0
            ? Years.Max(y => y.StartYear) + 1
            : (DateTime.Now.Month >= 9 ? DateTime.Now.Year : DateTime.Now.Year - 1);

        EditYearStartYear = startYear.ToString(CultureInfo.InvariantCulture);
        EditYearName = $"{startYear}–{startYear + 1}";
        EditYearNote = string.Empty;
        IsYearEditing = true;
    }

    /// <summary>Tanlangan o'quv yilini tahrirlash shaklini ochadi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task EditYearAsync()
    {
        var target = SelectedYear;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan o'quv yilini tanlang.").ConfigureAwait(true);
            return;
        }

        _editingYearId = target.Id;
        YearEditorTitle = "O'quv yilini tahrirlash";
        EditYearName = target.Name;
        EditYearStartYear = target.StartYear.ToString(CultureInfo.InvariantCulture);
        EditYearNote = target.Note ?? string.Empty;
        IsYearEditing = true;
    }

    /// <summary>O'quv yili shaklini yopadi.</summary>
    [RelayCommand]
    private void CancelYearEdit()
    {
        IsYearEditing = false;
        _editingYearId = 0;
    }

    /// <summary>O'quv yilini qo'shadi yoki nomini o'zgartiradi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SaveYearAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(EditYearName))
        {
            await _dialogs.ErrorAsync("O'quv yili nomini kiriting (masalan: 2025–2026).").ConfigureAwait(true);
            return;
        }

        if (!int.TryParse(EditYearStartYear?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var startYear)
            || startYear < 1900 || startYear > 2200)
        {
            await _dialogs.ErrorAsync(
                "Boshlanish yilini to'g'ri kiriting (masalan: 2025).").ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;
            var note = string.IsNullOrWhiteSpace(EditYearNote) ? null : EditYearNote.Trim();

            if (_editingYearId == 0)
            {
                var created = await _years.CreateAsync(EditYearName.Trim(), startYear, note, ct)
                    .ConfigureAwait(true);
                StatusMessage = "Yangi o'quv yili qo'shildi.";
                _editingYearId = created.Id;

                IsYearEditing = false;
                IsBusy = false;
                await RefreshAsync(ct).ConfigureAwait(true);

                _suppressSelection = true;
                try
                {
                    SelectedYear = Years.FirstOrDefault(y => y.Id == created.Id) ?? SelectedYear;
                }
                finally
                {
                    _suppressSelection = false;
                }

                await LoadSchedulesAsync(SelectedYear?.Id, ct).ConfigureAwait(true);
                await _main.RefreshSelectorsAsync(ct).ConfigureAwait(true);
                _editingYearId = 0;
                return;
            }

            await _years.RenameAsync(_editingYearId, EditYearName.Trim(), startYear, note ?? string.Empty, ct)
                .ConfigureAwait(true);
            StatusMessage = "O'quv yili saqlandi.";

            IsYearEditing = false;
            _editingYearId = 0;
            IsBusy = false;

            await RefreshAsync(ct).ConfigureAwait(true);
            await _main.RefreshSelectorsAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (InvalidOperationException ex)
        {
            await _dialogs.ErrorAsync(ex.Message).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Saqlashda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>O'quv yilini (ichidagi hamma narsa bilan) o'chiradi — avval tasdiq so'raladi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DeleteYearAsync()
    {
        var target = SelectedYear;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan o'quv yilini tanlang.").ConfigureAwait(true);
            return;
        }

        var scheduleCount = Schedules.Count;
        var lessonCount = Schedules.Sum(s => s.EntryCount);

        var confirmed = await _dialogs.ConfirmAsync(
                $"«{target.Name}» o'quv yili butunlay o'chirilsinmi?\n\n" +
                $"Uning ichidagi {scheduleCount} ta dars jadvali va ulardagi {lessonCount} ta dars yozuvi " +
                "ham o'chib ketadi. Buni ortga qaytarib bo'lmaydi.",
                "O'quv yilini o'chirish")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _years.DeleteAsync(target.Id).ConfigureAwait(true);
            StatusMessage = "O'quv yili o'chirildi.";
            IsYearEditing = false;
            IsScheduleEditing = false;

            _suppressSelection = true;
            try
            {
                SelectedYear = null;
            }
            finally
            {
                _suppressSelection = false;
            }

            IsBusy = false;
            await RefreshAsync().ConfigureAwait(true);
            await _main.RefreshSelectorsAsync().ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            // Masalan: oxirgi o'quv yilini o'chirib bo'lmaydi.
            await _dialogs.ErrorAsync(ex.Message, "O'chirib bo'lmadi").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("O'chirishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ================= Dars jadvali amallari =================

    /// <summary>Tanlangan yilda yangi jadval qo'shish shaklini ochadi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task NewScheduleAsync()
    {
        if (SelectedYear is null)
        {
            await _dialogs.InfoAsync("Avval o'quv yilini tanlang.").ConfigureAwait(true);
            return;
        }

        _editingScheduleId = 0;
        ScheduleEditorTitle = "Yangi dars jadvali";
        EditScheduleName = Schedules.Count == 0 ? "Asosiy jadval" : $"{Schedules.Count + 1}-variant";
        IsScheduleEditing = true;
    }

    /// <summary>Tanlangan jadval nomini tahrirlash shaklini ochadi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task EditScheduleAsync()
    {
        var target = SelectedSchedule;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan dars jadvalini tanlang.").ConfigureAwait(true);
            return;
        }

        _editingScheduleId = target.Id;
        ScheduleEditorTitle = "Jadval nomini o'zgartirish";
        EditScheduleName = target.Name;
        IsScheduleEditing = true;
    }

    /// <summary>Jadval shaklini yopadi.</summary>
    [RelayCommand]
    private void CancelScheduleEdit()
    {
        IsScheduleEditing = false;
        _editingScheduleId = 0;
    }

    /// <summary>Jadvalni qo'shadi yoki nomini o'zgartiradi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SaveScheduleAsync(CancellationToken ct = default)
    {
        if (SelectedYear is null)
        {
            await _dialogs.InfoAsync("Avval o'quv yilini tanlang.").ConfigureAwait(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(EditScheduleName))
        {
            await _dialogs.ErrorAsync("Jadval nomini kiriting.").ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;

            if (_editingScheduleId == 0)
            {
                await _sets.CreateAsync(SelectedYear.Id, EditScheduleName.Trim(), ct).ConfigureAwait(true);
                StatusMessage = "Yangi dars jadvali qo'shildi.";
            }
            else
            {
                await _sets.RenameAsync(_editingScheduleId, EditScheduleName.Trim(), ct).ConfigureAwait(true);
                StatusMessage = "Jadval nomi saqlandi.";
            }

            IsScheduleEditing = false;
            _editingScheduleId = 0;
            IsBusy = false;

            await LoadSchedulesAsync(SelectedYear.Id, ct).ConfigureAwait(true);
            await _main.RefreshSelectorsAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (InvalidOperationException ex)
        {
            await _dialogs.ErrorAsync(ex.Message).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Saqlashda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Jadvalning barcha darslari bilan nusxasini yaratadi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DuplicateScheduleAsync(CancellationToken ct = default)
    {
        var target = SelectedSchedule;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval nusxalanadigan jadvalni tanlang.").ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;
            var copy = await _sets.DuplicateAsync(target.Id, null, ct).ConfigureAwait(true);
            StatusMessage = $"«{copy.Name}» yaratildi ({target.EntryCount} ta dars nusxalandi).";
            IsBusy = false;

            await LoadSchedulesAsync(SelectedYear?.Id, ct).ConfigureAwait(true);
            await _main.RefreshSelectorsAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (InvalidOperationException ex)
        {
            await _dialogs.ErrorAsync(ex.Message).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Nusxalashda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Tanlangan jadvalni faol qiladi (butun dasturda shu jadval ko'rsatiladi).</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task ActivateScheduleAsync(CancellationToken ct = default)
    {
        var target = SelectedSchedule;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval faol qilinadigan jadvalni tanlang.").ConfigureAwait(true);
            return;
        }

        if (target.IsActive)
        {
            await _dialogs.InfoAsync($"«{target.Name}» allaqachon faol jadval.").ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;
            await _sets.SetActiveAsync(target.Id, ct).ConfigureAwait(true);
            StatusMessage = $"Faol jadval: {target.Name}.";
            IsBusy = false;

            await LoadSchedulesAsync(SelectedYear?.Id, ct).ConfigureAwait(true);
            await _main.RefreshSelectorsAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Jadvalni faollashtirishda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Jadvalni barcha darslari bilan o'chiradi — avval tasdiq so'raladi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DeleteScheduleAsync()
    {
        var target = SelectedSchedule;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan dars jadvalini tanlang.").ConfigureAwait(true);
            return;
        }

        var activeNote = target.IsActive
            ? "\n\nBu jadval hozir faol — o'chirilgach boshqa jadval avtomatik faollashadi."
            : string.Empty;

        var confirmed = await _dialogs.ConfirmAsync(
                $"«{target.Name}» dars jadvali o'chirilsinmi?\n\n" +
                $"Undagi {target.EntryCount} ta dars yozuvi ham o'chib ketadi. " +
                "Buni ortga qaytarib bo'lmaydi." + activeNote,
                "Jadvalni o'chirish")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _sets.DeleteAsync(target.Id).ConfigureAwait(true);
            StatusMessage = "Dars jadvali o'chirildi.";
            IsScheduleEditing = false;
            SelectedSchedule = null;
            IsBusy = false;

            await LoadSchedulesAsync(SelectedYear?.Id).ConfigureAwait(true);
            await _main.RefreshSelectorsAsync().ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            // Masalan: bazadagi oxirgi jadvalni o'chirib bo'lmaydi.
            await _dialogs.ErrorAsync(ex.Message, "O'chirib bo'lmadi").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("O'chirishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>"O'quv yillari" sahifasidagi bitta dars jadvali qatori.</summary>
public sealed class ScheduleRowViewModel
{
    /// <summary>Jadval va undagi darslar soni asosida qator yaratadi.</summary>
    public ScheduleRowViewModel(Schedule schedule, int entryCount)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        Id = schedule.Id;
        Name = schedule.Name;
        IsActive = schedule.IsActive;
        EntryCount = entryCount;
        CreatedAt = schedule.CreatedAt;
    }

    /// <summary>Jadval Id si.</summary>
    public int Id { get; }

    /// <summary>Jadval nomi.</summary>
    public string Name { get; }

    /// <summary>Shu jadval faolmi.</summary>
    public bool IsActive { get; }

    /// <summary>Jadvaldagi dars yozuvlari soni.</summary>
    public int EntryCount { get; }

    /// <summary>Yaratilgan vaqti (UTC).</summary>
    public DateTime CreatedAt { get; }

    /// <summary>Ro'yxatda ko'rsatiladigan darslar soni.</summary>
    public string EntryCountText => $"{EntryCount} ta dars";

    /// <summary>Faol jadval belgisi ("Faol" yoki bo'sh).</summary>
    public string ActiveText => IsActive ? "Faol" : string.Empty;

    /// <summary>Yaratilgan sanasi (mahalliy vaqt).</summary>
    public string CreatedAtText =>
        CreatedAt.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
}
