using CommunityToolkit.Mvvm.ComponentModel;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Biriktirmalar jadvalidagi bitta qator.</summary>
public sealed partial class AssignmentRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string hoursSummary = "Hisoblanmoqda...";

    [ObservableProperty]
    private double placedHours;

    [ObservableProperty]
    private double weeklyHours;

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
}
