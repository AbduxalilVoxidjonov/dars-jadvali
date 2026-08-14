using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Sinflar bo'limi.</summary>
public sealed partial class ClassGroupsViewModel : ViewModelBase
{
    private readonly IClassGroupService _classGroups;
    private readonly IDialogService _dialogs;

    private int _editingId;

    [ObservableProperty]
    private ClassGroup? selectedClassGroup;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string editorTitle = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editRoomNumber = string.Empty;

    [ObservableProperty]
    private string editStudentCount = "0";

    public ClassGroupsViewModel(IClassGroupService classGroups, IDialogService dialogs)
    {
        _classGroups = classGroups;
        _dialogs = dialogs;
    }

    /// <summary>Sinflar ro'yxati.</summary>
    public ObservableCollection<ClassGroup> ClassGroups { get; } = new();

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
            var items = await _classGroups.GetAllAsync(ct).ConfigureAwait(true);

            ClassGroups.Clear();
            foreach (var item in items.OrderBy(c => c.Name, StringComparer.CurrentCulture))
            {
                ClassGroups.Add(item);
            }

            StatusMessage = $"Jami {ClassGroups.Count} ta sinf.";
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error("Sinflarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
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
        EditorTitle = "Yangi sinf";
        EditName = string.Empty;
        EditRoomNumber = string.Empty;
        EditStudentCount = "0";
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit(ClassGroup? classGroup)
    {
        var target = classGroup ?? SelectedClassGroup;
        if (target is null)
        {
            _dialogs.Info("Avval ro'yxatdan sinfni tanlang.");
            return;
        }

        _editingId = target.Id;
        EditorTitle = "Sinfni tahrirlash";
        EditName = target.Name;
        EditRoomNumber = target.RoomNumber ?? string.Empty;
        EditStudentCount = target.StudentCount.ToString(CultureInfo.InvariantCulture);
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
        if (string.IsNullOrWhiteSpace(EditName))
        {
            _dialogs.Error("Sinf nomini kiriting (masalan: 5-A).");
            return;
        }

        if (!int.TryParse(EditStudentCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var studentCount)
            || studentCount < 0)
        {
            _dialogs.Error("O'quvchilar sonini butun musbat son ko'rinishida kiriting.");
            return;
        }

        try
        {
            IsBusy = true;

            if (_editingId == 0)
            {
                var created = new ClassGroup
                {
                    Name = EditName.Trim(),
                    RoomNumber = string.IsNullOrWhiteSpace(EditRoomNumber) ? null : EditRoomNumber.Trim(),
                    StudentCount = studentCount,
                };

                await _classGroups.CreateAsync(created, ct).ConfigureAwait(true);
                StatusMessage = "Yangi sinf qo'shildi.";
            }
            else
            {
                var existing = await _classGroups.GetByIdAsync(_editingId, ct).ConfigureAwait(true);
                if (existing is null)
                {
                    _dialogs.Error("Sinf topilmadi. Ro'yxat yangilanadi.");
                    await RefreshAsync(ct).ConfigureAwait(true);
                    return;
                }

                existing.Name = EditName.Trim();
                existing.RoomNumber = string.IsNullOrWhiteSpace(EditRoomNumber) ? null : EditRoomNumber.Trim();
                existing.StudentCount = studentCount;

                await _classGroups.UpdateAsync(existing, ct).ConfigureAwait(true);
                StatusMessage = "Sinf ma'lumotlari saqlandi.";
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
            _dialogs.Error("Saqlashda xatolik yuz berdi.\n\nSinf nomi takrorlanmasligi kerak.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(ClassGroup? classGroup)
    {
        var target = classGroup ?? SelectedClassGroup;
        if (target is null)
        {
            _dialogs.Info("Avval ro'yxatdan sinfni tanlang.");
            return;
        }

        if (!_dialogs.Confirm(
                $"\"{target.Name}\" sinfi o'chirilsinmi?\n\nShu sinfga tegishli biriktirmalar va jadvaldagi darslar ham o'chadi.",
                "Sinfni o'chirish"))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _classGroups.DeleteAsync(target.Id).ConfigureAwait(true);
            StatusMessage = "Sinf o'chirildi.";
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
