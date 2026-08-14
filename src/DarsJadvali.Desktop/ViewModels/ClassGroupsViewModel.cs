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

/// <summary>Sinflar bo'limi.</summary>
public sealed partial class ClassGroupsViewModel : ViewModelBase
{
    private readonly IClassGroupService _classGroups;
    private readonly IDialogService _dialogs;

    private int _editingId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ClassGroup? _selectedClassGroup;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editRoomNumber = string.Empty;

    [ObservableProperty]
    private string _editStudentCount = "0";

    public ClassGroupsViewModel(IClassGroupService classGroups, IDialogService dialogs)
    {
        _classGroups = classGroups;
        _dialogs = dialogs;
    }

    /// <summary>Sinflar ro'yxati.</summary>
    public ObservableCollection<ClassGroup> ClassGroups { get; } = new();

    /// <summary>Amal bajarilmayotgan payt — tugmalar yoqiladi.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Ro'yxatdan biror sinf tanlanganmi (va band emasmi).</summary>
    public bool HasSelection => !IsBusy && SelectedClassGroup is not null;

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct).ConfigureAwait(true);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(IsNotBusy));
            OnPropertyChanged(nameof(HasSelection));
        }
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
            await _dialogs.ErrorAsync("Sinflarni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
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
        EditorTitle = "Yangi sinf";
        EditName = string.Empty;
        EditRoomNumber = string.Empty;
        EditStudentCount = "0";
        IsEditing = true;
    }

    [RelayCommand]
    private async Task EditAsync(ClassGroup? classGroup)
    {
        var target = classGroup ?? SelectedClassGroup;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan sinfni tanlang.").ConfigureAwait(true);
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
            await _dialogs.ErrorAsync("Sinf nomini kiriting (masalan: 5-A).").ConfigureAwait(true);
            return;
        }

        if (!int.TryParse(EditStudentCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var studentCount)
            || studentCount < 0)
        {
            await _dialogs.ErrorAsync("O'quvchilar sonini butun musbat son ko'rinishida kiriting.")
                .ConfigureAwait(true);
            return;
        }

        var name = EditName.Trim();

        // Sinf nomi takrorlanmasligi kerak — bazaga bormasdan oldin tekshiramiz.
        if (ClassGroups.Any(c => c.Id != _editingId && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogs.ErrorAsync(
                    $"\"{name}\" nomli sinf allaqachon mavjud. Sinf nomi takrorlanmas bo'lishi kerak.",
                    "Nom takrorlandi")
                .ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;

            if (_editingId == 0)
            {
                var created = new ClassGroup
                {
                    Name = name,
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
                    await _dialogs.ErrorAsync("Sinf topilmadi. Ro'yxat yangilanadi.").ConfigureAwait(true);
                    await RefreshAsync(ct).ConfigureAwait(true);
                    return;
                }

                existing.Name = name;
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
        catch (Exception ex) when (UniqueViolation.Is(ex))
        {
            StatusMessage = "Sinf nomi takrorlandi.";
            await _dialogs.ErrorAsync(
                    $"\"{name}\" nomli sinf allaqachon mavjud. Sinf nomi takrorlanmas bo'lishi kerak.",
                    "Nom takrorlandi")
                .ConfigureAwait(true);
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

    [RelayCommand]
    private async Task DeleteAsync(ClassGroup? classGroup)
    {
        var target = classGroup ?? SelectedClassGroup;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan sinfni tanlang.").ConfigureAwait(true);
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
                $"\"{target.Name}\" sinfi o'chirilsinmi?\n\nShu sinfga tegishli biriktirmalar va jadvaldagi darslar ham o'chadi.",
                "Sinfni o'chirish")
            .ConfigureAwait(true);

        if (!confirmed)
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
            await _dialogs.ErrorAsync("O'chirishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
