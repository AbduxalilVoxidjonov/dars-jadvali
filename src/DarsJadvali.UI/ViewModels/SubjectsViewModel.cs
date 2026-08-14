using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.UI.Models;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Fanlar bo'limi.</summary>
public sealed partial class SubjectsViewModel : ViewModelBase
{
    private readonly ISubjectService _subjects;
    private readonly IDialogService _dialogs;

    private int _editingId;

    [ObservableProperty]
    private Subject? selectedSubject;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string editorTitle = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editCode = string.Empty;

    [ObservableProperty]
    private ColorOption? editColor;

    public SubjectsViewModel(ISubjectService subjects, IDialogService dialogs)
    {
        _subjects = subjects;
        _dialogs = dialogs;
    }

    /// <summary>Fanlar ro'yxati.</summary>
    public ObservableCollection<Subject> Subjects { get; } = new();

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
            var items = await _subjects.GetAllAsync(ct).ConfigureAwait(true);

            Subjects.Clear();
            foreach (var item in items.OrderBy(s => s.Name, StringComparer.CurrentCulture))
            {
                Subjects.Add(item);
            }

            StatusMessage = $"Jami {Subjects.Count} ta fan.";
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error("Fanlarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
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
        EditorTitle = "Yangi fan";
        EditName = string.Empty;
        EditCode = string.Empty;
        EditColor = ColorPalette.All[0];
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit(Subject? subject)
    {
        var target = subject ?? SelectedSubject;
        if (target is null)
        {
            _dialogs.Info("Avval ro'yxatdan fanni tanlang.");
            return;
        }

        _editingId = target.Id;
        EditorTitle = "Fanni tahrirlash";
        EditName = target.Name;
        EditCode = target.Code;
        EditColor = ColorPalette.Find(target.ColorCode);
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
            _dialogs.Error("Fan nomini kiriting.");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditCode))
        {
            _dialogs.Error("Fan kodini kiriting (masalan: MAT).");
            return;
        }

        try
        {
            IsBusy = true;
            var colorCode = EditColor?.Code ?? ColorPalette.All[0].Code;

            if (_editingId == 0)
            {
                var created = new Subject
                {
                    Name = EditName.Trim(),
                    Code = EditCode.Trim(),
                    ColorCode = colorCode,
                };

                await _subjects.CreateAsync(created, ct).ConfigureAwait(true);
                StatusMessage = "Yangi fan qo'shildi.";
            }
            else
            {
                var existing = await _subjects.GetByIdAsync(_editingId, ct).ConfigureAwait(true);
                if (existing is null)
                {
                    _dialogs.Error("Fan topilmadi. Ro'yxat yangilanadi.");
                    await RefreshAsync(ct).ConfigureAwait(true);
                    return;
                }

                existing.Name = EditName.Trim();
                existing.Code = EditCode.Trim();
                existing.ColorCode = colorCode;

                await _subjects.UpdateAsync(existing, ct).ConfigureAwait(true);
                StatusMessage = "Fan ma'lumotlari saqlandi.";
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
            _dialogs.Error("Saqlashda xatolik yuz berdi.\n\nFan kodi takrorlanmasligi kerak.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Subject? subject)
    {
        var target = subject ?? SelectedSubject;
        if (target is null)
        {
            _dialogs.Info("Avval ro'yxatdan fanni tanlang.");
            return;
        }

        if (!_dialogs.Confirm(
                $"\"{target.Name}\" fani o'chirilsinmi?\n\nShu fanga tegishli biriktirmalar va jadvaldagi darslar ham o'chadi.",
                "Fanni o'chirish"))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _subjects.DeleteAsync(target.Id).ConfigureAwait(true);
            StatusMessage = "Fan o'chirildi.";
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
