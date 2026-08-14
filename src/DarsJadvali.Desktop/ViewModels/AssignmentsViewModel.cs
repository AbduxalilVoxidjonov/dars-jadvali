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
    [NotifyPropertyChangedFor(nameof(SelectedTeacherLabel))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTeacher))]
    private Teacher? _selectedTeacher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRow))]
    private AssignmentRowViewModel? _selectedRow;

    [ObservableProperty]
    private Subject? _editSubject;

    [ObservableProperty]
    private ClassGroup? _editClassGroup;

    [ObservableProperty]
    private string _editWeeklyHours = "1";

    [ObservableProperty]
    private string _editorTitle = "Yangi biriktirma";

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

    /// <summary>Amal bajarilmayotgan payt — tugmalar yoqiladi.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>O'qituvchi tanlanganmi (va band emasmi).</summary>
    public bool HasSelectedTeacher => !IsBusy && SelectedTeacher is not null;

    /// <summary>Jadvaldan biriktirma tanlanganmi (va band emasmi).</summary>
    public bool HasSelectedRow => !IsBusy && SelectedRow is not null;

    /// <summary>"Tanlangan: Familiya Ism" matni.</summary>
    public string SelectedTeacherLabel =>
        SelectedTeacher is null ? "O'qituvchi tanlanmagan" : $"Tanlangan: {SelectedTeacher.FullName}";

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
            await _dialogs.ErrorAsync("Ma'lumotlarni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(IsNotBusy));
            OnPropertyChanged(nameof(HasSelectedTeacher));
            OnPropertyChanged(nameof(HasSelectedRow));
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
        SelectedRow = null;

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
                Rows.Add(new AssignmentRowViewModel(item));
            }

            foreach (var row in Rows)
            {
                await LoadSummaryAsync(row).ConfigureAwait(true);
            }

            StatusMessage = $"{SelectedTeacher.FullName}: {Rows.Count} ta biriktirma.";
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Biriktirmalarni yuklashda xatolik yuz berdi.\n\n" + ex.Message)
                .ConfigureAwait(true);
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
    private async Task NewAsync()
    {
        if (SelectedTeacher is null)
        {
            await _dialogs.InfoAsync("Avval chap tomondan o'qituvchini tanlang.").ConfigureAwait(true);
            return;
        }

        ResetEditor();
    }

    [RelayCommand]
    private async Task EditAsync(AssignmentRowViewModel? row)
    {
        var target = row ?? SelectedRow;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval biriktirmani tanlang.").ConfigureAwait(true);
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
            await _dialogs.InfoAsync("Avval o'qituvchini tanlang.").ConfigureAwait(true);
            return;
        }

        if (EditSubject is null)
        {
            await _dialogs.ErrorAsync("Fanni tanlang.").ConfigureAwait(true);
            return;
        }

        if (EditClassGroup is null)
        {
            await _dialogs.ErrorAsync("Sinfni tanlang.").ConfigureAwait(true);
            return;
        }

        if (!int.TryParse(EditWeeklyHours, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)
            || hours < 1)
        {
            await _dialogs.ErrorAsync("Haftalik soatni 1 dan katta butun son ko'rinishida kiriting.")
                .ConfigureAwait(true);
            return;
        }

        // (O'qituvchi, Fan, Sinf) uchligi takrorlanmasligi kerak.
        if (Rows.Any(r => r.Id != _editingId
                          && r.Assignment.SubjectId == EditSubject.Id
                          && r.Assignment.ClassGroupId == EditClassGroup.Id))
        {
            await _dialogs.ErrorAsync(
                    $"{EditClassGroup.Name} sinfida {EditSubject.Name} fani bo'yicha biriktirma allaqachon bor.\n\n" +
                    "Bir o'qituvchiga bir sinf va bir fan bo'yicha faqat bitta biriktirma bo'lishi mumkin.",
                    "Biriktirma takrorlandi")
                .ConfigureAwait(true);
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
                    await _dialogs.ErrorAsync("Biriktirma topilmadi.").ConfigureAwait(true);
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
        catch (Exception ex) when (UniqueViolation.Is(ex))
        {
            StatusMessage = "Biriktirma takrorlandi.";
            await _dialogs.ErrorAsync(
                    "Bir o'qituvchiga bir sinf va bir fan bo'yicha faqat bitta biriktirma bo'lishi mumkin.",
                    "Biriktirma takrorlandi")
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
    private async Task DeleteAsync(AssignmentRowViewModel? row)
    {
        var target = row ?? SelectedRow;
        if (target is null)
        {
            await _dialogs.InfoAsync("Avval biriktirmani tanlang.").ConfigureAwait(true);
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
                $"{target.ClassGroupName} — {target.SubjectName} biriktirmasi o'chirilsinmi?",
                "Biriktirmani o'chirish")
            .ConfigureAwait(true);

        if (!confirmed)
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
            await _dialogs.ErrorAsync("O'chirishda xatolik yuz berdi.\n\n" + ex.Message).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>Biriktirmalar jadvalidagi bitta qator.</summary>
public sealed partial class AssignmentRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _hoursSummary = "Hisoblanmoqda...";

    [ObservableProperty]
    private double _placedHours;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressMaximum))]
    private double _weeklyHours;

    public AssignmentRowViewModel(TeacherAssignment assignment)
    {
        Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
        WeeklyHours = assignment.WeeklyHoursCount;
    }

    /// <summary>Asosiy biriktirma yozuvi.</summary>
    public TeacherAssignment Assignment { get; }

    public int Id => Assignment.Id;

    public string SubjectName => Assignment.Subject?.Name ?? "(fan topilmadi)";

    public string ClassGroupName => Assignment.ClassGroup?.Name ?? "(sinf topilmadi)";

    public string TeacherName => Assignment.Teacher?.FullName ?? "(o'qituvchi topilmadi)";

    public int WeeklyHoursCount => Assignment.WeeklyHoursCount;

    /// <summary>ProgressBar uchun yuqori chegara (nolga bo'linishning oldini oladi).</summary>
    public double ProgressMaximum => WeeklyHours <= 0 ? 1d : WeeklyHours;
}
