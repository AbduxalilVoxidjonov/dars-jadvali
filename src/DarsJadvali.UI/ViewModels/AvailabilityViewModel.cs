using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.UI.Models;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>O'qituvchining ish vaqti oraliqlari.</summary>
public sealed partial class AvailabilityViewModel : ViewModelBase
{
    private readonly ITeacherService _teachers;
    private readonly IAvailabilityService _availabilities;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private Teacher? selectedTeacher;

    public AvailabilityViewModel(
        ITeacherService teachers,
        IAvailabilityService availabilities,
        IDialogService dialogs)
    {
        _teachers = teachers;
        _availabilities = availabilities;
        _dialogs = dialogs;
    }

    /// <summary>Chapdagi o'qituvchilar ro'yxati.</summary>
    public ObservableCollection<Teacher> Teachers { get; } = new();

    /// <summary>Tanlangan o'qituvchining vaqt oraliqlari.</summary>
    public ObservableCollection<AvailabilityRowViewModel> Rows { get; } = new();

    /// <summary>Kun tanlash ro'yxati.</summary>
    public IReadOnlyList<WeekDay> Days => WeekDayExtensions.All;

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;

            var teachers = await _teachers.GetAllAsync(ct).ConfigureAwait(true);

            Teachers.Clear();
            foreach (var teacher in teachers.OrderBy(t => t.FullName, StringComparer.CurrentCulture))
            {
                Teachers.Add(teacher);
            }

            SelectedTeacher = Teachers.FirstOrDefault();
            StatusMessage = "O'qituvchi vaqti bo'limi.";
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error("O'qituvchilarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        _ = ReloadRowsAsync();
    }

    [RelayCommand]
    private async Task ReloadRowsAsync()
    {
        Rows.Clear();

        if (SelectedTeacher is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var items = await _availabilities.GetByTeacherAsync(SelectedTeacher.Id).ConfigureAwait(true);

            foreach (var item in items.OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime))
            {
                Rows.Add(new AvailabilityRowViewModel(item));
            }

            StatusMessage = Rows.Count == 0
                ? $"{SelectedTeacher.FullName}: cheklov yo'q (istalgan vaqtda dars bera oladi)."
                : $"{SelectedTeacher.FullName}: {Rows.Count} ta vaqt oralig'i.";
        }
        catch (Exception ex)
        {
            _dialogs.Error("Vaqt oraliqlarini yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddRow()
    {
        if (SelectedTeacher is null)
        {
            _dialogs.Info("Avval chap tomondan o'qituvchini tanlang.");
            return;
        }

        var entity = new TeacherAvailability
        {
            TeacherId = SelectedTeacher.Id,
            DayOfWeek = WeekDay.Dushanba,
            StartTime = TimeSpan.FromHours(8.5),
            EndTime = TimeSpan.FromHours(13),
            IsAvailable = true,
        };

        Rows.Add(new AvailabilityRowViewModel(entity));
        StatusMessage = "Yangi oraliq qo'shildi. Saqlashni unutmang.";
    }

    [RelayCommand]
    private void RemoveRow(AvailabilityRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        Rows.Remove(row);
        StatusMessage = "Oraliq olib tashlandi. Saqlashni unutmang.";
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct = default)
    {
        if (SelectedTeacher is null)
        {
            _dialogs.Info("Avval o'qituvchini tanlang.");
            return;
        }

        var items = new List<TeacherAvailability>();

        foreach (var row in Rows)
        {
            if (!TimeTextHelper.TryParse(row.StartText, out var start))
            {
                _dialogs.Error(
                    $"{row.DayOfWeek.ToUzbek()} kuni uchun boshlanish vaqti noto'g'ri kiritilgan.\n\nFormat: HH:mm (masalan 08:30).");
                return;
            }

            if (!TimeTextHelper.TryParse(row.EndText, out var end))
            {
                _dialogs.Error(
                    $"{row.DayOfWeek.ToUzbek()} kuni uchun tugash vaqti noto'g'ri kiritilgan.\n\nFormat: HH:mm (masalan 13:00).");
                return;
            }

            if (end <= start)
            {
                _dialogs.Error(
                    $"{row.DayOfWeek.ToUzbek()} kuni uchun tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak.");
                return;
            }

            row.Entity.TeacherId = SelectedTeacher.Id;
            row.Entity.DayOfWeek = row.DayOfWeek;
            row.Entity.StartTime = start;
            row.Entity.EndTime = end;
            row.Entity.IsAvailable = row.IsAvailable;

            items.Add(row.Entity);
        }

        try
        {
            IsBusy = true;
            await _availabilities.ReplaceForTeacherAsync(SelectedTeacher.Id, items, ct).ConfigureAwait(true);

            StatusMessage = "Vaqt oraliqlari saqlandi.";
            _dialogs.Info($"{SelectedTeacher.FullName} uchun vaqt oraliqlari saqlandi.");

            await ReloadRowsAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error("Saqlashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
