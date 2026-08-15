using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>O'qituvchining qaysi dars soatlarida ishlashi (kunlar × dars soatlari to'ri).</summary>
public sealed partial class AvailabilityViewModel : ViewModelBase
{
    /// <summary>Ekranda ko'rinadigan izoh — foydalanuvchilar buni tez-tez chalkashtiradi.</summary>
    public const string RuleExplanation =
        "«Cheklov bor» belgilanmagan kunda o'qituvchi barcha soatlarda dars o'ta oladi.\n" +
        "Belgilangan kunda esa FAQAT tanlangan soatlarda dars o'ta oladi — qolgan soatlarga " +
        "jadval tuzishda dars qo'yilmaydi.";

    private readonly ITeacherService _teachers;
    private readonly IAvailabilityService _availabilities;
    private readonly IWorkDayService _workDays;
    private readonly IDialogService _dialogs;

    /// <summary>Sahifa yuklanayotgan payt — tanlov o'zgarishi qayta yuklashni ishga tushirmaydi.</summary>
    private bool _isInitializing;

    [ObservableProperty]
    private Teacher? _selectedTeacher;

    [ObservableProperty]
    private bool _hasDays;

    public AvailabilityViewModel(
        ITeacherService teachers,
        IAvailabilityService availabilities,
        IWorkDayService workDays,
        IDialogService dialogs)
    {
        _teachers = teachers;
        _availabilities = availabilities;
        _workDays = workDays;
        _dialogs = dialogs;
    }

    /// <summary>Chapdagi o'qituvchilar ro'yxati.</summary>
    public ObservableCollection<Teacher> Teachers { get; } = new();

    /// <summary>Dars soati ustunlari (sarlavha uchun).</summary>
    public ObservableCollection<LessonColumnViewModel> Columns { get; } = new();

    /// <summary>Faol ish kunlari qatorlari.</summary>
    public ObservableCollection<TeacherDayRowViewModel> Days { get; } = new();

    /// <summary>Ekranda ko'rsatiladigan qoida izohi.</summary>
    public string RuleText => RuleExplanation;

    /// <summary>Sarlavha: tanlangan o'qituvchi nomi bilan.</summary>
    public string HeaderText => SelectedTeacher is null
        ? "Ish soatlari"
        : "Ish soatlari — " + SelectedTeacher.FullName;

    public override Task LoadAsync(CancellationToken ct = default)
        => RunExclusiveAsync(LoadCoreAsync, ct);

    private async Task LoadCoreAsync(CancellationToken ct)
    {
        try
        {
            IsBusy = true;
            _isInitializing = true;

            await BuildColumnsAsync(ct).ConfigureAwait(true);

            var teachers = await _teachers.GetAllAsync(ct).ConfigureAwait(true);

            Teachers.Clear();
            foreach (var teacher in teachers.OrderBy(t => t.FullName, StringComparer.CurrentCulture))
            {
                Teachers.Add(teacher);
            }

            SelectedTeacher = Teachers.FirstOrDefault();

            if (Teachers.Count == 0)
            {
                StatusMessage = "O'qituvchilar ro'yxati bo'sh. Avval «O'qituvchilar» bo'limiga o'qituvchi qo'shing.";
            }

            _isInitializing = false;

            // Qayta yuklash setter orqali emas, shu yerda — navbat ichida ketma-ket bajariladi.
            await ReloadRowsCoreAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("O'qituvchilarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            _isInitializing = false;
            IsBusy = false;
        }
    }

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        OnPropertyChanged(nameof(HeaderText));

        if (_isInitializing)
        {
            return;
        }

        // Setterdan `await` qilib bo'lmaydi — amal navbatga qo'yiladi (M-01).
        _ = RunExclusiveAsync(ReloadRowsCoreAsync);
    }

    /// <summary>Dars soati ustunlarini (raqam + vaqt) tayyorlaydi.</summary>
    private async Task BuildColumnsAsync(CancellationToken ct)
    {
        var maxLesson = await _workDays.GetMaxLessonNumberAsync(ct).ConfigureAwait(true);
        var slots = await _workDays.GetLessonSlotsAsync(ct).ConfigureAwait(true);

        if (maxLesson < 1)
        {
            maxLesson = slots.Count > 0 ? slots.Max(s => s.LessonNumber) : 0;
        }

        Columns.Clear();
        for (var number = 1; number <= maxLesson; number++)
        {
            var slot = slots.FirstOrDefault(s => s.LessonNumber == number);
            var timeText = slot is null
                ? string.Empty
                : TimeTextHelper.ToText(slot.StartTime) + "-" + TimeTextHelper.ToText(slot.EndTime);

            Columns.Add(new LessonColumnViewModel(number, timeText));
        }
    }

    /// <summary>Tanlangan o'qituvchi bo'yicha kunlar to'rini qayta yuklaydi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ReloadRowsAsync(CancellationToken ct = default)
        => RunExclusiveAsync(ReloadRowsCoreAsync, ct);

    private async Task ReloadRowsCoreAsync(CancellationToken ct)
    {
        Days.Clear();
        HasDays = false;

        if (SelectedTeacher is null)
        {
            return;
        }

        try
        {
            IsBusy = true;

            if (Columns.Count == 0)
            {
                await BuildColumnsAsync(ct).ConfigureAwait(true);
            }

            var lessonNumbers = Columns.Select(c => c.LessonNumber).ToList();
            var days = await _availabilities
                .GetLessonAvailabilityAsync(SelectedTeacher.Id, ct)
                .ConfigureAwait(true);

            foreach (var day in days)
            {
                Days.Add(new TeacherDayRowViewModel(day, lessonNumbers));
            }

            HasDays = Days.Count > 0;

            if (Columns.Count == 0)
            {
                StatusMessage = "Dars soatlari sozlanmagan. Avval «Hafta kunlari» bo'limida dars soatlarini kiriting.";
            }
            else if (Days.Count == 0)
            {
                StatusMessage = "Faol ish kuni yo'q. Avval «Hafta kunlari» bo'limida ish kunlarini yoqing.";
            }
            else
            {
                var restricted = Days.Count(d => d.HasRestriction);
                StatusMessage = restricted == 0
                    ? $"{SelectedTeacher.FullName}: cheklov yo'q — barcha soatlarda dars o'ta oladi."
                    : $"{SelectedTeacher.FullName}: {restricted} ta kunda cheklov bor.";
            }
        }
        catch (OperationCanceledException)
        {
            // Boshqa o'qituvchi tanlandi — bu natija kerak emas.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Ish soatlarini yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SaveAsync(CancellationToken ct = default)
    {
        if (SelectedTeacher is null)
        {
            await _dialogs.InfoAsync("Avval chap tomondan o'qituvchini tanlang.");
            return;
        }

        if (Days.Count == 0)
        {
            await _dialogs.InfoAsync("Saqlash uchun ma'lumot yo'q: faol ish kunlari topilmadi.");
            return;
        }

        // Cheklov belgilangan, lekin bironta soat tanlanmagan kunlar — o'sha kuni umuman dars bo'lmaydi.
        var emptyDays = Days
            .Where(d => d.HasRestriction && d.SelectedLessonNumbers.Count == 0)
            .Select(d => d.DayName)
            .ToList();

        if (emptyDays.Count > 0)
        {
            var confirmed = await _dialogs.ConfirmAsync(
                "Quyidagi kunlarda «Cheklov bor» belgilangan, lekin bironta ham dars soati tanlanmagan:\n\n" +
                "• " + string.Join("\n• ", emptyDays) + "\n\n" +
                "Bu — o'qituvchi o'sha kunlari umuman dars o'ta olmaydi degani. Davom etilsinmi?",
                "Diqqat");

            if (!confirmed)
            {
                return;
            }
        }

        try
        {
            IsBusy = true;

            await _availabilities
                .SaveLessonAvailabilityAsync(SelectedTeacher.Id, Days.Select(d => d.ToRecord()).ToList(), ct)
                .ConfigureAwait(true);

            StatusMessage = "Ish soatlari saqlandi.";
            await _dialogs.InfoAsync($"{SelectedTeacher.FullName} uchun ish soatlari saqlandi.");

            await ReloadRowsAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Saqlashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Saqlanmagan o'zgarishlarni bekor qiladi.</summary>
    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task CancelAsync()
    {
        await ReloadRowsAsync().ConfigureAwait(true);
        StatusMessage = "O'zgarishlar bekor qilindi.";
    }
}

/// <summary>Jadval sarlavhasidagi bitta dars soati ustuni.</summary>
public sealed class LessonColumnViewModel
{
    public LessonColumnViewModel(int lessonNumber, string timeText)
    {
        LessonNumber = lessonNumber;
        TimeText = timeText;
    }

    /// <summary>Dars raqami (1..N).</summary>
    public int LessonNumber { get; }

    /// <summary>Sarlavhadagi raqam matni.</summary>
    public string NumberText => LessonNumber.ToString();

    /// <summary>"08:30-09:15" ko'rinishidagi vaqt (bo'sh bo'lishi mumkin).</summary>
    public string TimeText { get; }
}

/// <summary>Bitta ish kuni qatori: cheklov bayrog'i + dars soati katakchalari.</summary>
public sealed partial class TeacherDayRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoRestrictionHint))]
    private bool _hasRestriction;

    public TeacherDayRowViewModel(TeacherDayAvailability source, IReadOnlyList<int> lessonNumbers)
    {
        Day = source.Day;
        _hasRestriction = source.HasRestriction;

        var allowed = new HashSet<int>(source.AllowedLessonNumbers);

        foreach (var number in lessonNumbers)
        {
            Cells.Add(new LessonCellViewModel(this, number, allowed.Contains(number)));
        }
    }

    /// <summary>Kun qiymati.</summary>
    public WeekDay Day { get; }

    /// <summary>Kunning o'zbekcha nomi.</summary>
    public string DayName => Day.ToUzbek();

    /// <summary>Dars soati katakchalari.</summary>
    public ObservableCollection<LessonCellViewModel> Cells { get; } = new();

    /// <summary>Cheklov yo'q bo'lganda ko'rsatiladigan kulrang izoh.</summary>
    public bool NoRestrictionHint => !HasRestriction;

    /// <summary>Tanlangan dars soatlari raqamlari.</summary>
    public IReadOnlyList<int> SelectedLessonNumbers
        => Cells.Where(c => c.IsSelected).Select(c => c.LessonNumber).ToList();

    /// <summary>Barcha katakchalarni belgilaydi.</summary>
    [RelayCommand]
    private void SelectAll()
    {
        HasRestriction = true;
        foreach (var cell in Cells)
        {
            cell.IsSelected = true;
        }
    }

    /// <summary>Barcha katakchalarni bo'shatadi.</summary>
    [RelayCommand]
    private void ClearAll()
    {
        foreach (var cell in Cells)
        {
            cell.IsSelected = false;
        }
    }

    /// <summary>Application qatlamiga uzatiladigan yozuvni tayyorlaydi.</summary>
    public TeacherDayAvailability ToRecord()
        => new(Day, HasRestriction, HasRestriction ? SelectedLessonNumbers : Array.Empty<int>());

    partial void OnHasRestrictionChanged(bool value)
    {
        foreach (var cell in Cells)
        {
            cell.RaiseEnabledChanged();
        }
    }
}

/// <summary>To'rdagi bitta katakcha (kun × dars soati).</summary>
public sealed partial class LessonCellViewModel : ObservableObject
{
    private readonly TeacherDayRowViewModel _row;

    [ObservableProperty]
    private bool _isSelected;

    public LessonCellViewModel(TeacherDayRowViewModel row, int lessonNumber, bool isSelected)
    {
        _row = row;
        LessonNumber = lessonNumber;
        _isSelected = isSelected;
    }

    /// <summary>Dars raqami.</summary>
    public int LessonNumber { get; }

    /// <summary>Katakcha faolmi (kunda cheklov bo'lsagina).</summary>
    public bool IsEnabled => _row.HasRestriction;

    /// <summary>Kun cheklovi o'zgarganda chaqiriladi.</summary>
    public void RaiseEnabledChanged() => OnPropertyChanged(nameof(IsEnabled));
}
