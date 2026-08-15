using System;
using System.Collections.Generic;
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
/// Sinflar bo'limi: nom, xona, o'quvchilar soni va <b>smena</b>.
/// </summary>
/// <remarks>
/// Smena yangi (v2) <c>SchoolClass</c> da yashaydi, ekran esa hamon eski
/// <see cref="ClassGroup"/> ustida ishlaydi. Ikkalasi <c>SchoolClass.LegacyClassGroupId</c>
/// orqali bog'lanadi — shuning uchun har qator <see cref="ClassRowViewModel"/> ga
/// o'raladi va u ikkala Id ni ham olib yuradi.
/// </remarks>
public sealed partial class ClassGroupsViewModel : ViewModelBase
{
    private readonly IClassGroupService _classGroups;
    private readonly IClassShiftService _shifts;
    private readonly IDialogService _dialogs;

    private int _editingId;

    /// <summary>Tahrirlanayotgan sinfning v2 (<c>SchoolClass</c>) Id si; 0 — bog'lanmagan.</summary>
    private int _editingSchoolClassId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ClassRowViewModel? _selectedClassGroup;

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

    /// <summary>Tahrirlash panelida tanlangan smena.</summary>
    [ObservableProperty]
    private ClassShiftOption? _editShift;

    /// <summary>Smena tanlagichi ko'rinadimi (smenalar ma'lumotnomasi to'ldirilgan bo'lsa).</summary>
    [ObservableProperty]
    private bool _hasShifts;

    /// <summary>Tanlangan sinf v2 modelga bog'langanmi (bog'lanmasa smena o'zgartirib bo'lmaydi).</summary>
    [ObservableProperty]
    private bool _canEditShift;

    public ClassGroupsViewModel(
        IClassGroupService classGroups,
        IClassShiftService shifts,
        IDialogService dialogs)
    {
        _classGroups = classGroups;
        _shifts = shifts;
        _dialogs = dialogs;
    }

    /// <summary>Sinflar ro'yxati.</summary>
    public ObservableCollection<ClassRowViewModel> ClassGroups { get; } = new();

    /// <summary>Smena variantlari ("Smena tayinlanmagan" ham shu yerda).</summary>
    public ObservableCollection<ClassShiftOption> Shifts { get; } = new();

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
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;
            var items = await _classGroups.GetAllAsync(ct).ConfigureAwait(true);

            // Smena ma'lumoti yangi modeldan keladi; u hali to'ldirilmagan bo'lsa
            // ekran baribir ishlaydi — shunchaki smena ustuni bo'sh qoladi.
            var shiftOptions = await LoadShiftsSafeAsync(ct).ConfigureAwait(true);
            var classShifts = await LoadClassShiftsSafeAsync(ct).ConfigureAwait(true);

            Shifts.Clear();
            foreach (var option in shiftOptions)
            {
                Shifts.Add(option);
            }

            HasShifts = Shifts.Count > 1;

            var byLegacyId = new Dictionary<int, ClassShiftView>();
            var byName = new Dictionary<string, ClassShiftView>(StringComparer.CurrentCultureIgnoreCase);

            foreach (var view in classShifts)
            {
                if (view.LegacyClassGroupId is int legacyId)
                {
                    byLegacyId[legacyId] = view;
                }

                byName.TryAdd(view.ClassName, view);
            }

            var selectedId = SelectedClassGroup?.Id ?? 0;

            ClassGroups.Clear();
            foreach (var item in items.OrderBy(c => c.Name, StringComparer.CurrentCulture))
            {
                // Avval ANIQ bog'lanish (backfill izi), keyingina nom bo'yicha moslash.
                if (!byLegacyId.TryGetValue(item.Id, out var shift))
                {
                    byName.TryGetValue(item.Name, out shift);
                }

                ClassGroups.Add(new ClassRowViewModel(item, shift));
            }

            SelectedClassGroup = ClassGroups.FirstOrDefault(c => c.Id == selectedId);

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

    private async Task<IReadOnlyList<ClassShiftOption>> LoadShiftsSafeAsync(CancellationToken ct)
    {
        try
        {
            return await _shifts.GetShiftsAsync(null, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Smenalar ma'lumotnomasi hali yo'q — ekranning qolgan qismi ishlashda davom etadi.
            return new[] { ClassShiftOption.None };
        }
    }

    private async Task<IReadOnlyList<ClassShiftView>> LoadClassShiftsSafeAsync(CancellationToken ct)
    {
        try
        {
            return await _shifts.GetClassShiftsAsync(null, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Array.Empty<ClassShiftView>();
        }
    }

    [RelayCommand]
    private void New()
    {
        _editingId = 0;
        _editingSchoolClassId = 0;
        EditorTitle = "Yangi sinf";
        EditName = string.Empty;
        EditRoomNumber = string.Empty;
        EditStudentCount = "0";
        EditShift = Shifts.FirstOrDefault();

        // Yangi sinf hali v2 modelga ko'chirilmagan — smena keyin tayinlanadi.
        CanEditShift = false;
        IsEditing = true;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task EditAsync(ClassRowViewModel? row)
    {
        var target = row ?? SelectedClassGroup;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval ro'yxatdan sinfni tanlang.").ConfigureAwait(true);
            return;
        }

        _editingId = target.Id;
        _editingSchoolClassId = target.SchoolClassId;
        EditorTitle = "Sinfni tahrirlash";
        EditName = target.Name;
        EditRoomNumber = target.RoomNumber ?? string.Empty;
        EditStudentCount = target.StudentCount.ToString(CultureInfo.InvariantCulture);
        EditShift = Shifts.FirstOrDefault(s => s.ShiftId == target.ShiftId) ?? Shifts.FirstOrDefault();
        CanEditShift = target.SchoolClassId > 0;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        _editingId = 0;
        _editingSchoolClassId = 0;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
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

                // Smena alohida yoziladi: u yangi (v2) modelga tegishli.
                if (!await ApplyShiftAsync(ct).ConfigureAwait(true))
                {
                    return;
                }
            }

            IsEditing = false;
            _editingId = 0;
            _editingSchoolClassId = 0;
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

    /// <summary>
    /// Tanlangan smenani bazaga yozadi. Rad etilsa foydalanuvchiga sabab ko'rsatiladi
    /// va tahrirlash paneli ochiq qoladi (kiritilgan ma'lumot yo'qolmasligi uchun).
    /// </summary>
    /// <returns>Davom etish mumkinmi.</returns>
    private async Task<bool> ApplyShiftAsync(CancellationToken ct)
    {
        if (_editingSchoolClassId <= 0)
        {
            return true;
        }

        var row = ClassGroups.FirstOrDefault(c => c.Id == _editingId);
        var wanted = EditShift?.ShiftId;

        if (row is not null && row.ShiftId == wanted)
        {
            return true;
        }

        var result = await _shifts
            .SetShiftAsync(_editingSchoolClassId, wanted, ct)
            .ConfigureAwait(true);

        if (!result.Changed)
        {
            StatusMessage = "Smena o'zgartirilmadi.";
            await _dialogs.ErrorAsync(result.Message, "Smenani o'zgartirib bo'lmadi").ConfigureAwait(true);
            return false;
        }

        StatusMessage = result.Message;
        return true;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DeleteAsync(ClassRowViewModel? row)
    {
        var target = row ?? SelectedClassGroup;
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

/// <summary>Sinflar ro'yxatidagi bitta qator (eski sinf + yangi modeldagi smenasi).</summary>
public sealed class ClassRowViewModel
{
    /// <summary>Qatorni yaratadi.</summary>
    /// <param name="source">Eski model sinfi.</param>
    /// <param name="shift">Yangi modeldagi mos sinf (topilmasa <c>null</c>).</param>
    public ClassRowViewModel(ClassGroup source, ClassShiftView? shift)
    {
        ArgumentNullException.ThrowIfNull(source);

        Id = source.Id;
        Name = source.Name;
        RoomNumber = source.RoomNumber;
        StudentCount = source.StudentCount;
        SchoolClassId = shift?.SchoolClassId ?? 0;
        ShiftId = shift?.ShiftId;
        ShiftName = string.IsNullOrWhiteSpace(shift?.ShiftName) ? "—" : shift!.ShiftName;
    }

    /// <summary>Eski <c>ClassGroup.Id</c>.</summary>
    public int Id { get; }

    /// <summary>Yangi <c>SchoolClass.Id</c> (0 — bog'lanmagan).</summary>
    public int SchoolClassId { get; }

    /// <summary>Sinf nomi.</summary>
    public string Name { get; }

    /// <summary>Asosiy xona.</summary>
    public string? RoomNumber { get; }

    /// <summary>O'quvchilar soni.</summary>
    public int StudentCount { get; }

    /// <summary>Joriy smena Id.</summary>
    public int? ShiftId { get; }

    /// <summary>Joriy smena nomi ("—" — tayinlanmagan).</summary>
    public string ShiftName { get; }
}
