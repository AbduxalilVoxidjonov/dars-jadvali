using CommunityToolkit.Mvvm.ComponentModel;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Hafta kunlari jadvalidagi bitta kun.</summary>
public sealed partial class WorkDayRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private int maxLessonsPerDay;

    public WorkDayRowViewModel(WorkDay workDay)
    {
        Entity = workDay ?? throw new ArgumentNullException(nameof(workDay));
        isActive = workDay.IsActive;
        maxLessonsPerDay = workDay.MaxLessonsPerDay;
    }

    /// <summary>Bazadagi yozuv (Id = 0 bo'lsa yangi).</summary>
    public WorkDay Entity { get; }

    /// <summary>Kunning o'zbekcha nomi.</summary>
    public string DayName => Entity.DayOfWeek.ToUzbek();

    /// <summary>Kun qiymati.</summary>
    public WeekDay DayOfWeek => Entity.DayOfWeek;

    /// <summary>Tahrirlangan qiymatlarni entity ga ko'chiradi.</summary>
    public WorkDay ToEntity()
    {
        Entity.IsActive = IsActive;
        Entity.MaxLessonsPerDay = MaxLessonsPerDay;
        return Entity;
    }
}
