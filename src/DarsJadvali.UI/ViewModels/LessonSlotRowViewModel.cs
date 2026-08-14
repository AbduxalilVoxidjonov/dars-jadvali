using CommunityToolkit.Mvvm.ComponentModel;
using DarsJadvali.Domain.Entities;
using DarsJadvali.UI.Models;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Dars soatlari jadvalidagi bitta qator.</summary>
public sealed partial class LessonSlotRowViewModel : ObservableObject
{
    [ObservableProperty]
    private int lessonNumber;

    [ObservableProperty]
    private string startText = "08:30";

    [ObservableProperty]
    private string endText = "09:15";

    public LessonSlotRowViewModel(LessonSlot slot)
    {
        Entity = slot ?? throw new ArgumentNullException(nameof(slot));
        lessonNumber = slot.LessonNumber;
        startText = TimeTextHelper.ToText(slot.StartTime);
        endText = TimeTextHelper.ToText(slot.EndTime);
    }

    /// <summary>Bazadagi yozuv (Id = 0 bo'lsa yangi).</summary>
    public LessonSlot Entity { get; }
}
