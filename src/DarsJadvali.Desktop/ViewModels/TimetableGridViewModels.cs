using CommunityToolkit.Mvvm.ComponentModel;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Kun sarlavhasi ustuni (aylantirilganda joyida qoladi).</summary>
public sealed partial class TimetableDayHeaderViewModel : ObservableObject
{
    /// <summary>Drag paytida sarlavha ham baholanadi (aSc: kun sarlavhalari rangga bo'yaladi).</summary>
    [ObservableProperty]
    private PlacementRating? _rating;

    /// <summary>Yangi sarlavha yaratadi.</summary>
    public TimetableDayHeaderViewModel(WeekDay day, TimetableMetrics metrics)
    {
        Day = day;
        Metrics = metrics;
        Title = day.ToUzbek();
    }

    /// <summary>Kun.</summary>
    public WeekDay Day { get; }

    /// <summary>Ko'rinadigan matn.</summary>
    public string Title { get; }

    /// <summary>To'r o'lchamlari (yagona manba).</summary>
    public TimetableMetrics Metrics { get; }
}

/// <summary>
/// To'rdagi bitta katak. Bu — <b>ko'rinish</b> obyekti; karta modelining o'zi
/// <see cref="TimetableCard"/> da.
/// </summary>
public sealed partial class TimetableSlotViewModel : ObservableObject
{
    /// <summary>Katakdagi karta (bo'sh bo'lsa <c>null</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCard))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private TimetableCard? _card;

    /// <summary>Bu katak juft darsning davomi (boshi yuqoridagi qatorda).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCardText))]
    private bool _isContinuation;

    /// <summary>Kursor ostidagi jonli baho (drag paytida).</summary>
    [ObservableProperty]
    private PlacementRating? _rating;

    /// <summary>SHIFT bilan yoritilgan mumkin pozitsiya.</summary>
    [ObservableProperty]
    private bool _isHighlighted;

    /// <summary>Kursor aynan shu katak ustida.</summary>
    [ObservableProperty]
    private bool _isHoverTarget;

    /// <summary>Yangi katak yaratadi.</summary>
    public TimetableSlotViewModel(
        TimetableBoardViewModel owner, int scopeId, WeekDay day, int period, TimetableMetrics metrics)
    {
        Owner = owner;
        ScopeId = scopeId;
        Day = day;
        Period = period;
        Metrics = metrics;
    }

    /// <summary>Egasi — barcha amallar shu orqali ketadi.</summary>
    public TimetableBoardViewModel Owner { get; }

    /// <summary>Katak qaysi qatorga (sinf/o'qituvchi/xona) tegishli.</summary>
    public int ScopeId { get; }

    /// <summary>Kun.</summary>
    public WeekDay Day { get; }

    /// <summary>Dars raqami (1-based).</summary>
    public int Period { get; }

    /// <summary>To'r o'lchamlari.</summary>
    public TimetableMetrics Metrics { get; }

    /// <summary>Katakda karta bormi.</summary>
    public bool HasCard => Card is not null;

    /// <summary>Katak bo'shmi.</summary>
    public bool IsEmpty => Card is null;

    /// <summary>Kartaning matni shu katakda ko'rsatiladimi (juft darsning faqat birinchi soatida).</summary>
    public bool ShowCardText => Card is not null && !IsContinuation;
}

/// <summary>
/// To'rdagi bitta qator: (sinf yoki o'qituvchi) × (dars raqami).
/// </summary>
/// <remarks>
/// Qatorlar <c>VirtualizingStackPanel</c> ichida — ekranda ko'rinmagan qator umuman
/// yaratilmaydi. M-04 dagi "2000+ Border bir vaqtda vizual daraxtda" muammosi shu bilan yopiladi.
/// </remarks>
public sealed class TimetableRowViewModel
{
    /// <summary>Yangi qator yaratadi.</summary>
    public TimetableRowViewModel(
        int scopeId,
        string scopeName,
        bool isFirstOfScope,
        bool isAlternate,
        int period,
        string timeText,
        TimetableMetrics metrics)
    {
        ScopeId = scopeId;
        ScopeName = scopeName;
        IsFirstOfScope = isFirstOfScope;
        IsAlternate = isAlternate;
        Period = period;
        TimeText = timeText;
        Metrics = metrics;
    }

    /// <summary>Qator egasi (sinf/o'qituvchi/xona identifikatori).</summary>
    public int ScopeId { get; }

    /// <summary>Egasining nomi ("5-A").</summary>
    public string ScopeName { get; }

    /// <summary>Nom faqat blokning birinchi qatorida ko'rsatiladi (RowSpan o'rniga).</summary>
    public bool IsFirstOfScope { get; }

    /// <summary>Bloklar navbatma-navbat ozgina boshqa fonda.</summary>
    public bool IsAlternate { get; }

    /// <summary>Dars raqami.</summary>
    public int Period { get; }

    /// <summary>Dars raqami matni ("3-soat").</summary>
    public string PeriodText => Period + "-soat";

    /// <summary>Dars vaqti ("10:20-11:05"), topilmasa bo'sh.</summary>
    public string TimeText { get; }

    /// <summary>Vaqt matni bormi.</summary>
    public bool HasTimeText => !string.IsNullOrWhiteSpace(TimeText);

    /// <summary>To'r o'lchamlari.</summary>
    public TimetableMetrics Metrics { get; }

    /// <summary>Qatordagi kun kataklari.</summary>
    public List<TimetableSlotViewModel> Slots { get; } = new();
}

/// <summary>Ko'rinish almashtirgichdagi bitta band ("5-A sinfi", "Aliyev A.").</summary>
/// <param name="Id">Resurs identifikatori (0 — "barchasi").</param>
/// <param name="Name">Ko'rinadigan nom.</param>
public sealed record TimetableScopeOption(int Id, string Name);
