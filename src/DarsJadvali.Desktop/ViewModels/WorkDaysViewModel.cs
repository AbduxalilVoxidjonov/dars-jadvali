using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Hafta kunlari va dars soatlari sozlamalari.</summary>
public sealed partial class WorkDaysViewModel : ViewModelBase
{
    private const int MinLessons = 1;
    private const int MaxLessons = 12;

    private readonly IWorkDayService _workDays;
    private readonly IDialogService _dialogs;

    public WorkDaysViewModel(IWorkDayService workDays, IDialogService dialogs)
    {
        _workDays = workDays;
        _dialogs = dialogs;

        LessonCountOptions = Enumerable.Range(MinLessons, MaxLessons - MinLessons + 1).ToList();
    }

    /// <summary>Hafta kunlari (doim 7 ta).</summary>
    public ObservableCollection<WorkDayRowViewModel> Days { get; } = new();

    /// <summary>Dars soatlari jadvali.</summary>
    public ObservableCollection<LessonSlotRowViewModel> Slots { get; } = new();

    /// <summary>Kunlik dars soni uchun variantlar (1..12).</summary>
    public IReadOnlyList<int> LessonCountOptions { get; }

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            IsBusy = true;

            var existing = await _workDays.GetAllAsync(ct).ConfigureAwait(true);
            var slots = await _workDays.GetLessonSlotsAsync(ct).ConfigureAwait(true);

            Days.Clear();
            foreach (var day in WeekDayExtensions.All)
            {
                var entity = existing.FirstOrDefault(w => w.DayOfWeek == day)
                             ?? new WorkDay
                             {
                                 DayOfWeek = day,
                                 IsActive = day != WeekDay.Yakshanba,
                                 MaxLessonsPerDay = 7,
                             };

                Days.Add(new WorkDayRowViewModel(entity));
            }

            Slots.Clear();
            foreach (var slot in slots.OrderBy(s => s.LessonNumber))
            {
                Slots.Add(new LessonSlotRowViewModel(slot));
            }

            StatusMessage = "Hafta kunlari va dars soatlari.";
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Sozlamalarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddSlotAsync()
    {
        var nextNumber = 1;
        foreach (var row in Slots)
        {
            if (row.TryGetLessonNumber(out var number) && number >= nextNumber)
            {
                nextNumber = number + 1;
            }
        }

        var start = TimeSpan.FromHours(8.5);
        var end = start + TimeSpan.FromMinutes(45);

        if (Slots.Count > 0 && TimeTextHelper.TryParse(Slots[^1].EndText, out var lastEnd))
        {
            start = lastEnd + TimeSpan.FromMinutes(10);
            end = start + TimeSpan.FromMinutes(45);
        }

        if (end >= TimeSpan.FromDays(1))
        {
            await _dialogs.ErrorAsync("Yangi dars soatini qo'shib bo'lmadi: vaqt sutkadan chiqib ketmoqda.");
            return;
        }

        if (nextNumber > MaxLessons)
        {
            await _dialogs.ErrorAsync($"Dars soatlari soni {MaxLessons} tadan oshmasligi kerak.");
            return;
        }

        Slots.Add(new LessonSlotRowViewModel(new LessonSlot
        {
            LessonNumber = nextNumber,
            StartTime = start,
            EndTime = end,
        }));

        StatusMessage = $"{nextNumber}-dars qo'shildi. Saqlashni unutmang.";
    }

    [RelayCommand]
    private async Task RemoveSlotAsync(LessonSlotRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            $"«{row.LessonNumberText}-dars» soati ro'yxatdan olib tashlansinmi?",
            "Dars soatini o'chirish");

        if (!confirmed)
        {
            return;
        }

        Slots.Remove(row);
        StatusMessage = "Dars soati olib tashlandi. Saqlashni unutmang.";
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct = default)
    {
        // 1) Kunlar tekshiruvi
        if (Days.All(d => !d.IsActive))
        {
            await _dialogs.ErrorAsync("Kamida bitta ish kuni faol bo'lishi kerak.");
            return;
        }

        foreach (var day in Days)
        {
            if (day.MaxLessonsPerDay < MinLessons || day.MaxLessonsPerDay > MaxLessons)
            {
                await _dialogs.ErrorAsync(
                    $"{day.DayName} kuni uchun darslar soni {MinLessons} dan {MaxLessons} gacha bo'lishi kerak.");
                return;
            }
        }

        // 2) Dars soatlari tekshiruvi — qator raqami bilan aniq xabar beriladi.
        var slotEntities = new List<LessonSlot>();
        var usedNumbers = new Dictionary<int, int>();

        for (var i = 0; i < Slots.Count; i++)
        {
            var row = Slots[i];
            var rowNo = i + 1;

            if (!row.TryGetLessonNumber(out var number) || number < MinLessons || number > MaxLessons)
            {
                await _dialogs.ErrorAsync(
                    $"{rowNo}-qator: dars raqami noto'g'ri («{row.LessonNumberText}»).\n\n" +
                    $"{MinLessons} dan {MaxLessons} gacha butun son kiriting.");
                return;
            }

            if (usedNumbers.TryGetValue(number, out var firstRow))
            {
                await _dialogs.ErrorAsync(
                    $"{rowNo}-qator: {number}-dars raqami takrorlangan ({firstRow}-qatorda ham bor).\n\n" +
                    "Har bir dars raqami faqat bir marta bo'lishi kerak.");
                return;
            }

            usedNumbers[number] = rowNo;

            if (!TimeTextHelper.TryParse(row.StartText, out var start))
            {
                await _dialogs.ErrorAsync(
                    $"{rowNo}-qator ({number}-dars): boshlanish vaqti noto'g'ri («{row.StartText}»).\n\n" +
                    "Format: HH:mm (masalan 08:30).");
                return;
            }

            if (!TimeTextHelper.TryParse(row.EndText, out var end))
            {
                await _dialogs.ErrorAsync(
                    $"{rowNo}-qator ({number}-dars): tugash vaqti noto'g'ri («{row.EndText}»).\n\n" +
                    "Format: HH:mm (masalan 09:15).");
                return;
            }

            if (end <= start)
            {
                await _dialogs.ErrorAsync(
                    $"{rowNo}-qator ({number}-dars): tugash vaqti ({row.EndText}) boshlanish vaqtidan ({row.StartText}) keyin bo'lishi kerak.");
                return;
            }

            row.Entity.LessonNumber = number;
            row.Entity.StartTime = start;
            row.Entity.EndTime = end;
            slotEntities.Add(row.Entity);
        }

        try
        {
            IsBusy = true;

            await _workDays.SaveAllAsync(Days.Select(d => d.ToEntity()).ToList(), ct).ConfigureAwait(true);
            await _workDays
                .SaveLessonSlotsAsync(slotEntities.OrderBy(s => s.LessonNumber).ToList(), ct)
                .ConfigureAwait(true);

            StatusMessage = "Sozlamalar saqlandi.";
            await _dialogs.InfoAsync("Hafta kunlari va dars soatlari saqlandi.");

            await LoadAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            await _dialogs.ErrorAsync("Saqlashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>Hafta kunlari jadvalidagi bitta kun.</summary>
public sealed partial class WorkDayRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private int _maxLessonsPerDay;

    public WorkDayRowViewModel(WorkDay workDay)
    {
        Entity = workDay ?? throw new ArgumentNullException(nameof(workDay));
        _isActive = workDay.IsActive;
        _maxLessonsPerDay = workDay.MaxLessonsPerDay;
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

/// <summary>Dars soatlari jadvalidagi bitta qator.</summary>
public sealed partial class LessonSlotRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _lessonNumberText = "1";

    [ObservableProperty]
    private string _startText = "08:30";

    [ObservableProperty]
    private string _endText = "09:15";

    public LessonSlotRowViewModel(LessonSlot slot)
    {
        Entity = slot ?? throw new ArgumentNullException(nameof(slot));
        _lessonNumberText = slot.LessonNumber.ToString(CultureInfo.InvariantCulture);
        _startText = TimeTextHelper.ToText(slot.StartTime);
        _endText = TimeTextHelper.ToText(slot.EndTime);
    }

    /// <summary>Bazadagi yozuv (Id = 0 bo'lsa yangi).</summary>
    public LessonSlot Entity { get; }

    /// <summary>Matn ko'rinishidagi dars raqamini butun songa aylantiradi.</summary>
    public bool TryGetLessonNumber(out int number)
        => int.TryParse(
            (LessonNumberText ?? string.Empty).Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out number);
}
