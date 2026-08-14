using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.UI.Models;
using DarsJadvali.UI.Services;

namespace DarsJadvali.UI.ViewModels;

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
            _dialogs.Error("Sozlamalarni yuklashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddSlot()
    {
        var nextNumber = Slots.Count == 0 ? 1 : Slots.Max(s => s.LessonNumber) + 1;
        var start = TimeSpan.FromHours(8.5);
        var end = start + TimeSpan.FromMinutes(45);

        if (Slots.Count > 0)
        {
            var last = Slots[^1];
            if (TimeTextHelper.TryParse(last.EndText, out var lastEnd))
            {
                start = lastEnd + TimeSpan.FromMinutes(10);
                end = start + TimeSpan.FromMinutes(45);
            }
        }

        if (end >= TimeSpan.FromDays(1))
        {
            _dialogs.Error("Yangi dars soatini qo'shib bo'lmadi: vaqt sutkadan chiqib ketmoqda.");
            return;
        }

        var slot = new LessonSlot
        {
            LessonNumber = nextNumber,
            StartTime = start,
            EndTime = end,
        };

        Slots.Add(new LessonSlotRowViewModel(slot));
        StatusMessage = $"{nextNumber}-dars qo'shildi. Saqlashni unutmang.";
    }

    [RelayCommand]
    private void RemoveSlot(LessonSlotRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_dialogs.Confirm($"{row.LessonNumber}-dars soati ro'yxatdan olib tashlansinmi?", "Dars soatini o'chirish"))
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
            _dialogs.Error("Kamida bitta ish kuni faol bo'lishi kerak.");
            return;
        }

        foreach (var day in Days)
        {
            if (day.MaxLessonsPerDay < MinLessons || day.MaxLessonsPerDay > MaxLessons)
            {
                _dialogs.Error($"{day.DayName} kuni uchun darslar soni {MinLessons} dan {MaxLessons} gacha bo'lishi kerak.");
                return;
            }
        }

        // 2) Dars soatlari tekshiruvi
        var slotEntities = new List<LessonSlot>();
        var usedNumbers = new HashSet<int>();

        foreach (var row in Slots.OrderBy(s => s.LessonNumber))
        {
            if (row.LessonNumber < 1)
            {
                _dialogs.Error("Dars raqami 1 dan kichik bo'lishi mumkin emas.");
                return;
            }

            if (!usedNumbers.Add(row.LessonNumber))
            {
                _dialogs.Error($"{row.LessonNumber}-dars raqami takrorlangan. Har bir dars raqami bir marta bo'lishi kerak.");
                return;
            }

            if (!TimeTextHelper.TryParse(row.StartText, out var start))
            {
                _dialogs.Error($"{row.LessonNumber}-dars boshlanish vaqti noto'g'ri. Format: HH:mm (masalan 08:30).");
                return;
            }

            if (!TimeTextHelper.TryParse(row.EndText, out var end))
            {
                _dialogs.Error($"{row.LessonNumber}-dars tugash vaqti noto'g'ri. Format: HH:mm (masalan 09:15).");
                return;
            }

            if (end <= start)
            {
                _dialogs.Error($"{row.LessonNumber}-dars tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak.");
                return;
            }

            row.Entity.LessonNumber = row.LessonNumber;
            row.Entity.StartTime = start;
            row.Entity.EndTime = end;
            slotEntities.Add(row.Entity);
        }

        try
        {
            IsBusy = true;

            await _workDays.SaveAllAsync(Days.Select(d => d.ToEntity()).ToList(), ct).ConfigureAwait(true);
            await _workDays.SaveLessonSlotsAsync(slotEntities, ct).ConfigureAwait(true);

            StatusMessage = "Sozlamalar saqlandi.";
            _dialogs.Info("Hafta kunlari va dars soatlari saqlandi.");

            await LoadAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        catch (Exception ex)
        {
            _dialogs.Error("Saqlashda xatolik yuz berdi.\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
