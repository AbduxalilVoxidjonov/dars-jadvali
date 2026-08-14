using CommunityToolkit.Mvvm.ComponentModel;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.UI.Models;

namespace DarsJadvali.UI.ViewModels;

/// <summary>O'qituvchining bitta vaqt oralig'i.</summary>
public sealed partial class AvailabilityRowViewModel : ObservableObject
{
    [ObservableProperty]
    private WeekDay dayOfWeek;

    [ObservableProperty]
    private string startText = "08:30";

    [ObservableProperty]
    private string endText = "13:00";

    [ObservableProperty]
    private bool isAvailable = true;

    public AvailabilityRowViewModel(TeacherAvailability availability)
    {
        Entity = availability ?? throw new ArgumentNullException(nameof(availability));
        dayOfWeek = availability.DayOfWeek;
        startText = TimeTextHelper.ToText(availability.StartTime);
        endText = TimeTextHelper.ToText(availability.EndTime);
        isAvailable = availability.IsAvailable;
    }

    /// <summary>Bazadagi yozuv (Id = 0 bo'lsa yangi).</summary>
    public TeacherAvailability Entity { get; }
}
