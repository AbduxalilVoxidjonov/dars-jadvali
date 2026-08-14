using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Fanlar bo'limi.</summary>
public sealed partial class SubjectsViewModel : ViewModelBase
{
    private readonly ISubjectService _subjects;
    private readonly IDialogService _dialogs;

    private int _editingId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Subject? _selectedSubject;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editCode = string.Empty;

    [ObservableProperty]
    private ColorOption? _editColor;

    public SubjectsViewModel(ISubjectService subjects, IDialogService dialogs)
    {
        _subjects = subjects;
        _dialogs = dialogs;
    }

    /// <summary>Fanlar ro'yxati.</summary>
    public ObservableCollection<Subject> Subjects { get; } = new();

    /// <summary>Rang tanlash uchun tayyor ranglar.</summary>
    public IReadOnlyList<ColorOption> Colors => ColorPalette.All;

    /// <summary>Amal bajarilmayotgan payt — tugmalar yoqiladi.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Ro'yxatdan biror fan tanlanganmi (va band emasmi).</summary>
    public bool HasSelection => !IsBusy && SelectedSubject is not null;

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
            await _dialogs.ErrorAsync("Fanlarni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
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
        EditorTitle = "Yangi fan";
        EditName = string.Empty;
        EditCode = string.Empty;
        EditColor = NextFreeColor();
        IsEditing = true;
    }

    /// <summary>Mavjud fanlar ranglariga qarab yangi fan uchun bo'sh rang tanlaydi.</summary>
    private ColorOption NextFreeColor()
        => ColorPalette.NextFree(Subjects.Select(s => s.ColorCode));

    [RelayCommand]
    private async Task EditAsync(Subject? subject)
    {
        var target = subject ?? SelectedSubject;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan fanni tanlang.").ConfigureAwait(true);
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
            await _dialogs.ErrorAsync("Fan nomini kiriting.").ConfigureAwait(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(EditCode))
        {
            await _dialogs.ErrorAsync("Fan kodini kiriting (masalan: MAT).").ConfigureAwait(true);
            return;
        }

        var code = EditCode.Trim();

        // Kod takrorlanmasligi kerak — bazaga bormasdan oldin ro'yxatdan tekshiramiz.
        if (Subjects.Any(s => s.Id != _editingId && string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogs.ErrorAsync(
                    $"\"{code}\" kodi allaqachon band. Har bir fanning kodi takrorlanmas bo'lishi kerak.",
                    "Kod takrorlandi")
                .ConfigureAwait(true);
            return;
        }

        try
        {
            IsBusy = true;
            // Rang tanlanmagan bo'lsa: yangi fanga bo'sh rang, tahrirlashda esa birinchi rang.
            var colorCode = EditColor?.Code
                ?? (_editingId == 0 ? NextFreeColor().Code : ColorPalette.All[0].Code);

            if (_editingId == 0)
            {
                var created = new Subject
                {
                    Name = EditName.Trim(),
                    Code = code,
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
                    await _dialogs.ErrorAsync("Fan topilmadi. Ro'yxat yangilanadi.").ConfigureAwait(true);
                    await RefreshAsync(ct).ConfigureAwait(true);
                    return;
                }

                existing.Name = EditName.Trim();
                existing.Code = code;
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
        catch (Exception ex) when (UniqueViolation.Is(ex))
        {
            // Baza darajasidagi unikal indeks buzildi — foydalanuvchiga tushunarli xabar.
            StatusMessage = "Fan kodi takrorlandi.";
            await _dialogs.ErrorAsync(
                    $"\"{code}\" kodi allaqachon band. Har bir fanning kodi takrorlanmas bo'lishi kerak.",
                    "Kod takrorlandi")
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
    private async Task DeleteAsync(Subject? subject)
    {
        var target = subject ?? SelectedSubject;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan fanni tanlang.").ConfigureAwait(true);
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
                $"\"{target.Name}\" fani o'chirilsinmi?\n\nShu fanga tegishli biriktirmalar va jadvaldagi darslar ham o'chadi.",
                "Fanni o'chirish")
            .ConfigureAwait(true);

        if (!confirmed)
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
            await _dialogs.ErrorAsync("O'chirishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>Baza unikal indeksi buzilganini aniqlaydi (SQLite / EF Core xabarlari bo'yicha).</summary>
internal static class UniqueViolation
{
    public static bool Is(Exception? ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("SQLITE_CONSTRAINT", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
