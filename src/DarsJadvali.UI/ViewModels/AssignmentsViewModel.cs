using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Biriktirmalar: qaysi o'qituvchi qaysi sinfda qaysi fanni necha soat o'qitadi.</summary>
public sealed partial class AssignmentsViewModel : ViewModelBase
{
    private readonly ITeacherService _teachers;
    private readonly ISubjectService _subjects;
    private readonly IClassGroupService _classGroups;
    private readonly IAssignmentService _assignments;
    private readonly IDialogService _dialogs;

    private int _editingId;

    [ObservableProperty]
    private Teacher? selectedTeacher;

    [ObservableProperty]
    private AssignmentRowViewModel? selectedRow;

    [ObservableProperty]
    private Subject? editSubject;

    [ObservableProperty]
    private ClassGroup? editClassGroup;

    [ObservableProperty]
    private string editWeeklyHours = "1";

    [ObservableProperty]
    private string editorTitle = "Yangi biriktirma";

    public AssignmentsViewModel(
        ITeacherService teachers,
        ISubjectService subjects,
        IClassGroupService classGroups,
        IAssignmentService assignments,
        IDialogService dialogs)
    {
        _teachers = teachers;
        _subjects = subjects;
        _classGroups = classGroups;
        _assignments = assignments;
        _dialogs = dialogs;
    }

    /// <summary>Chapdagi o'qituvchilar ro'yxati.</summary>
    public ObservableCollection<Teacher> Teachers { get; } = new();

    /// <summary>Tanlangan o'qituvchining biriktirmalari.</summary>
    public ObservableCollection<AssignmentRowViewModel> Rows { get; } = new();

    /// <summary>Fan tanlash ro'yxati.</summary>
    public ObservableCollection<Subject> Subjects { get; } = new();

    /// <summary>Sinf tanlash ro'yxati.</summary>
    public ObservableCollection<ClassGroup> ClassGroups { get; } = new();

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;

            var teachers = await _teachers.GetAllAsync(ct).ConfigureAwait(true);
            var subjects = await _subjects.GetAllAsync(ct).ConfigureAwait(true);
            var classGroups = await _classGroups.GetAllAsync(ct).ConfigureAwait(true);

            Teachers.Clear();
            foreach (var teacher in teachers.OrderBy(t => t.FullName, StringComparer.CurrentCulture))
            {
                Teachers.Add(teacher);
            }

            Subjects.Clear();
            foreach (var subject in subjects.OrderBy(s => s.Name, StringComparer.CurrentCulture))
            {
                Subjects.Add(subject);
            }

            ClassGroups.Clear();
            foreach (var classGroup in classGroups.OrderBy(c => c.Name, StringComparer.CurrentCulture))
            {
                ClassGroups.Add(classGroup);
            }

            SelectedTeacher = Teachers.FirstOrDefault();
            StatusMessage = "Biriktirmalar bo'limi.";
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error("Ma'lumotlarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        ResetEditor();
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
            var items = await _assignments.GetByTeacherAsync(SelectedTeacher.Id).ConfigureAwait(true);

            foreach (var item in items
                         .OrderBy(a => a.ClassGroup?.Name, StringComparer.CurrentCulture)
                         .ThenBy(a => a.Subject?.Name, StringComparer.CurrentCulture))
            {
                var row = new AssignmentRowViewModel(item);
                Rows.Add(row);
            }

            foreach (var row in Rows)
            {
                await LoadSummaryAsync(row).ConfigureAwait(true);
            }

            StatusMessage = $"{SelectedTeacher.FullName}: {Rows.Count} ta biriktirma.";
        }
        catch (Exception ex)
        {
            _dialogs.Error("Biriktirmalarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSummaryAsync(AssignmentRowViewModel row)
    {
        try
        {
            var summary = await _assignments.GetHoursSummaryAsync(row.Id).ConfigureAwait(true);
            row.WeeklyHours = summary.Weekly;
            row.PlacedHours = summary.Placed;
            row.HoursSummary = $"{summary.Weekly} dan {summary.Placed} tasi qo'yilgan" +
                               (summary.Remaining > 0 ? $" ({summary.Remaining} ta qoldi)" : " (to'liq)");
        }
        catch (Exception)
        {
            row.HoursSummary = "Hisoblab bo'lmadi.";
        }
    }

    [RelayCommand]
    private void New()
    {
        if (SelectedTeacher is null)
        {
            _dialogs.Info("Avval chap tomondan o'qituvchini tanlang.");
            return;
        }

        ResetEditor();
    }

    [RelayCommand]
    private void Edit(AssignmentRowViewModel? row)
    {
        var target = row ?? SelectedRow;
        if (target is null)
        {
            _dialogs.Info("Avval biriktirmani tanlang.");
            return;
        }

        _editingId = target.Id;
        EditorTitle = "Biriktirmani tahrirlash";
        EditSubject = Subjects.FirstOrDefault(s => s.Id == target.Assignment.SubjectId);
        EditClassGroup = ClassGroups.FirstOrDefault(c => c.Id == target.Assignment.ClassGroupId);
        EditWeeklyHours = target.Assignment.WeeklyHoursCount.ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetEditor();
    }

    private void ResetEditor()
    {
        _editingId = 0;
        EditorTitle = "Yangi biriktirma";
        EditSubject = null;
        EditClassGroup = null;
        EditWeeklyHours = "1";
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct = default)
    {
        if (SelectedTeacher is null)
        {
            _dialogs.Info("Avval o'qituvchini tanlang.");
            return;
        }

        if (EditSubject is null)
        {
            _dialogs.Error("Fanni tanlang.");
            return;
        }

        if (EditClassGroup is null)
        {
            _dialogs.Error("Sinfni tanlang.");
            return;
        }

        if (!int.TryParse(EditWeeklyHours, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)
            || hours < 1)
        {
            _dialogs.Error("Haftalik soatni 1 dan katta butun son ko'rinishida kiriting.");
            return;
        }

        try
        {
            IsBusy = true;

            if (_editingId == 0)
            {
                var created = new TeacherAssignment
                {
                    TeacherId = SelectedTeacher.Id,
                    SubjectId = EditSubject.Id,
                    ClassGroupId = EditClassGroup.Id,
                    WeeklyHoursCount = hours,
                };

                await _assignments.CreateAsync(created, ct).ConfigureAwait(true);
                StatusMessage = "Yangi biriktirma qo'shildi.";
            }
            else
            {
                var row = Rows.FirstOrDefault(r => r.Id == _editingId);
                if (row is null)
                {
                    _dialogs.Error("Biriktirma topilmadi.");
                    await ReloadRowsAsync().ConfigureAwait(true);
                    return;
                }

                var entity = row.Assignment;
                entity.SubjectId = EditSubject.Id;
                entity.ClassGroupId = EditClassGroup.Id;
                entity.WeeklyHoursCount = hours;

                await _assignments.UpdateAsync(entity, ct).ConfigureAwait(true);
                StatusMessage = "Biriktirma saqlandi.";
            }

            ResetEditor();
            await ReloadRowsAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error(
                "Saqlashda xatolik yuz berdi.\n\nBir o'qituvchiga bir sinf va bir fan bo'yicha faqat bitta biriktirma bo'lishi mumkin.\n\n" +
                ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(AssignmentRowViewModel? row)
    {
        var target = row ?? SelectedRow;
        if (target is null)
        {
            _dialogs.Info("Avval biriktirmani tanlang.");
            return;
        }

        if (!_dialogs.Confirm(
                $"{target.ClassGroupName} — {target.SubjectName} biriktirmasi o'chirilsinmi?",
                "Biriktirmani o'chirish"))
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _assignments.DeleteAsync(target.Id).ConfigureAwait(true);
            StatusMessage = "Biriktirma o'chirildi.";
            ResetEditor();
            await ReloadRowsAsync().ConfigureAwait(true);
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
