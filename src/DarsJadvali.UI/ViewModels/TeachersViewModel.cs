using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.UI.Models;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>O'qituvchilar bo'limi.</summary>
public sealed partial class TeachersViewModel : ViewModelBase
{
    private readonly ITeacherService _teachers;
    private readonly IDialogService _dialogs;

    private int _editingId;

    [ObservableProperty]
    private Teacher? selectedTeacher;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string editorTitle = string.Empty;

    [ObservableProperty]
    private string editFullName = string.Empty;

    [ObservableProperty]
    private string editPhone = string.Empty;

    [ObservableProperty]
    private ColorOption? editColor;

    [ObservableProperty]
    private bool editIsActive = true;

    public TeachersViewModel(ITeacherService teachers, IDialogService dialogs)
    {
        _teachers = teachers;
        _dialogs = dialogs;
    }

    /// <summary>Ro'yxatdagi o'qituvchilar.</summary>
    public ObservableCollection<Teacher> Teachers { get; } = new();

    /// <summary>Rang tanlash uchun tayyor ranglar.</summary>
    public IReadOnlyList<ColorOption> Colors => ColorPalette.All;

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
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
            _dialogs.Error("O'qituvchilarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
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
        EditColor = ColorPalette.All[0];
        EditIsActive = true;
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit(Teacher? teacher)
    {
        var target = teacher ?? SelectedTeacher;
        if (target is null)
        {
            _dialogs.Info("Avval ro'yxatdan o'qituvchini tanlang.");
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

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(EditFullName))
        {
            _dialogs.Error("O'qituvchining familiya-ismini kiriting.");
            return;
        }

        try
        {
            IsBusy = true;
            var colorCode = EditColor?.Code ?? ColorPalette.All[0].Code;

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
                    _dialogs.Error("O'qituvchi topilmadi. Ro'yxat yangilanadi.");
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
            _dialogs.Error("Saqlashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Teacher? teacher)
    {
        var target = teacher ?? SelectedTeacher;
        if (target is null)
        {
            _dialogs.Info("Avval ro'yxatdan o'qituvchini tanlang.");
            return;
        }

        if (!_dialogs.Confirm(
                $"\"{target.FullName}\" o'chirilsinmi?\n\nUning barcha biriktirmalari, vaqt oraliqlari va jadvaldagi darslari ham o'chadi.",
                "O'qituvchini o'chirish"))
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
            _dialogs.Error("O'chirishda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
