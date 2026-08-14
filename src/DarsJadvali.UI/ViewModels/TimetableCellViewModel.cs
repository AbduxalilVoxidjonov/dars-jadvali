using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Jadval to'rining bitta katagi (sarlavha yoki dars katagi).</summary>
public sealed partial class TimetableCellViewModel : ObservableObject
{
    private readonly TimetableViewModel _owner;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntry))]
    private int? entryId;

    [ObservableProperty]
    private string subjectName = string.Empty;

    [ObservableProperty]
    private string personName = string.Empty;

    [ObservableProperty]
    private string roomText = string.Empty;

    [ObservableProperty]
    private string colorCode = "#FFFFFF";

    [ObservableProperty]
    private bool isSelected;

    public TimetableCellViewModel(TimetableViewModel owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>Sarlavha katagi (kun nomi, dars raqami yoki burchak).</summary>
    public bool IsHeader { get; init; }

    /// <summary>Sarlavha matni.</summary>
    public string HeaderText { get; init; } = string.Empty;

    /// <summary>Sarlavha ostidagi qo'shimcha matn (dars vaqti).</summary>
    public string HeaderSubText { get; init; } = string.Empty;

    /// <summary>Katakka tegishli kun.</summary>
    public WeekDay Day { get; init; }

    /// <summary>Katakka tegishli dars raqami.</summary>
    public int LessonNumber { get; init; }

    /// <summary>Katakda dars bormi.</summary>
    public bool HasEntry => EntryId.HasValue;

    /// <summary>Katakni tanlash.</summary>
    [RelayCommand]
    private void Select() => _owner.SelectCell(this);

    /// <summary>Katakdagi darsni o'chirish.</summary>
    [RelayCommand]
    private Task DeleteAsync() => _owner.DeleteEntryAsync(this);
}
