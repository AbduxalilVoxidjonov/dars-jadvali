using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>O'qituvchilar bo'limi.</summary>
public sealed partial class TeachersViewModel : ViewModelBase
{
    private readonly ITeacherService _teachers;
    private readonly IDialogService _dialogs;

    private int _editingId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Teacher? _selectedTeacher;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editFullName = string.Empty;

    [ObservableProperty]
    private string _editPhone = string.Empty;

    [ObservableProperty]
    private ColorOption? _editColor;

    [ObservableProperty]
    private bool _editIsActive = true;

    public TeachersViewModel(ITeacherService teachers, IDialogService dialogs)
    {
        _teachers = teachers;
        _dialogs = dialogs;
    }

    /// <summary>Ro'yxatdagi o'qituvchilar.</summary>
    public ObservableCollection<Teacher> Teachers { get; } = new();

    /// <summary>Rang tanlash uchun tayyor ranglar.</summary>
    public IReadOnlyList<ColorOption> Colors => ColorPalette.All;


    /// <summary>Ro'yxatdan biror o'qituvchi tanlanganmi (va band emasmi).</summary>
    public bool HasSelection => !IsBusy && SelectedTeacher is not null;

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct).ConfigureAwait(true);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            var items = await _teachers.GetAllAsync(ct).ConfigureAwait(true);

            Teachers.Clear();
            foreach (var item in items.OrderBy(t => t.FullName, StringComparer.CurrentCulture))
            {
                Teachers.Add(item);
            }

            StatusMessage = $"Jami {Teachers.Count} ta o'qituvchi.";
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("O'qituvchilarni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void New()
    {
        _editingId = 0;
        EditorTitle = "Yangi o'qituvchi";
        EditFullName = string.Empty;
        EditPhone = string.Empty;
        EditColor = NextFreeColor();
        EditIsActive = true;
        IsEditing = true;
    }

    /// <summary>Mavjud o'qituvchilar ranglariga qarab yangi o'qituvchi uchun bo'sh rang tanlaydi.</summary>
    private ColorOption NextFreeColor()
        => ColorPalette.NextFree(Teachers.Select(t => t.ColorCode));

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task EditAsync(Teacher? teacher)
    {
        var target = teacher ?? SelectedTeacher;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan o'qituvchini tanlang.").ConfigureAwait(true);
            return;
        }

        _editingId = target.Id;
        EditorTitle = "O'qituvchini tahrirlash";
        EditFullName = target.FullName;
        EditPhone = target.Phone ?? string.Empty;
        EditColor = ColorPalette.Find(target.ColorCode);
        EditIsActive = target.IsActive;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        _editingId = 0;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SaveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(EditFullName))
        {
            await _dialogs.ErrorAsync("O'qituvchining familiya-ismini kiriting.").ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;
            // Rang tanlanmagan bo'lsa: yangi o'qituvchiga bo'sh rang, tahrirlashda esa birinchi rang.
            var colorCode = EditColor?.Code
                ?? (_editingId == 0 ? NextFreeColor().Code : ColorPalette.All[0].Code);

            if (_editingId == 0)
            {
                var created = new Teacher
                {
                    FullName = EditFullName.Trim(),
                    Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim(),
                    ColorCode = colorCode,
                    IsActive = EditIsActive,
                };

                await _teachers.CreateAsync(created, ct).ConfigureAwait(true);
                StatusMessage = "Yangi o'qituvchi qo'shildi.";
            }
            else
            {
                var existing = await _teachers.GetByIdAsync(_editingId, ct).ConfigureAwait(true);
                if (existing is null)
                {
                    await _dialogs.ErrorAsync("O'qituvchi topilmadi. Ro'yxat yangilanadi.").ConfigureAwait(true);
                    await RefreshAsync(ct).ConfigureAwait(true);
                    return;
                }

                existing.FullName = EditFullName.Trim();
                existing.Phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim();
                existing.ColorCode = colorCode;
                existing.IsActive = EditIsActive;

                await _teachers.UpdateAsync(existing, ct).ConfigureAwait(true);
                StatusMessage = "O'qituvchi ma'lumotlari saqlandi.";
            }

            IsEditing = false;
            _editingId = 0;
            await RefreshAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
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

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DeleteAsync(Teacher? teacher)
    {
        var target = teacher ?? SelectedTeacher;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan o'qituvchini tanlang.").ConfigureAwait(true);
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
                $"\"{target.FullName}\" o'chirilsinmi?\n\nUning barcha biriktirmalari, vaqt oraliqlari va jadvaldagi darslari ham o'chadi.",
                "O'qituvchini o'chirish")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _teachers.DeleteAsync(target.Id).ConfigureAwait(true);
            StatusMessage = "O'qituvchi o'chirildi.";
            IsEditing = false;
            await RefreshAsync().ConfigureAwait(true);
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
