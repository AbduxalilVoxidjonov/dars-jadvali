using System.Globalization;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Export;

/// <summary>Bazadan o'qib, PDF uchun tayyor jadval modelini quradi.</summary>
public interface ITimetableExportModelBuilder
{
    /// <summary>Modelni quradi. Ma'lumot bir marta yuklanadi.</summary>
    Task<TimetableDocumentModel> BuildAsync(PdfExportOptions options, CancellationToken ct = default);
}

/// <summary><see cref="ITimetableExportModelBuilder"/> implementatsiyasi.</summary>
public sealed class TimetableExportModelBuilder : ITimetableExportModelBuilder
{
    private readonly IScheduleService _schedules;
    private readonly IWorkDayService _workDays;
    private readonly IClassGroupService _classGroups;

    /// <summary>Yangi quruvchi yaratadi.</summary>
    public TimetableExportModelBuilder(
        IScheduleService schedules,
        IWorkDayService workDays,
        IClassGroupService classGroups)
    {
        _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        _workDays = workDays ?? throw new ArgumentNullException(nameof(workDays));
        _classGroups = classGroups ?? throw new ArgumentNullException(nameof(classGroups));
    }

    /// <inheritdoc />
    public async Task<TimetableDocumentModel> BuildAsync(PdfExportOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // --- Ma'lumot BIR MARTA yuklanadi; quyidagi sikllar faqat xotirada ishlaydi. ---
        var activeDays = await _workDays.GetActiveAsync(ct).ConfigureAwait(false);
        var slots = await _workDays.GetLessonSlotsAsync(ct).ConfigureAwait(false);
        var groups = await _classGroups.GetAllAsync(ct).ConfigureAwait(false);
        // Faqat bitta jadval chiziladi (options.ScheduleId, null bo'lsa — faol jadval).
        var entries = await _schedules.GetAllAsync(options.ScheduleId, ct).ConfigureAwait(false);

        var days = activeDays.Select(d => d.DayOfWeek).Distinct().OrderBy(d => (int)d).ToList();
        var dayNames = days.Select(d => d.ToUzbek()).ToList();

        var selectedGroups = groups
            .Where(g => options.ClassGroupId is null || g.Id == options.ClassGroupId.Value)
            .OrderBy(g => g.Name, NaturalNameComparer.Instance)
            .ToList();

        var selectedIds = selectedGroups.Select(g => g.Id).ToHashSet();
        var dayIndex = days
            .Select((d, i) => (d, i))
            .ToDictionary(x => x.d, x => x.i);

        // Tanlangan sinflarga va faol kunlarga tegishli yozuvlar.
        var relevant = entries
            .Where(e => selectedIds.Contains(e.ClassGroupId) && dayIndex.ContainsKey(e.DayOfWeek))
            .ToList();

        // (sinf, kun, soat) -> yozuv
        var byCell = new Dictionary<(int ClassGroupId, WeekDay Day, int Lesson), ScheduleEntry>();
        foreach (var entry in relevant)
        {
            byCell.TryAdd((entry.ClassGroupId, entry.DayOfWeek, entry.LessonNumber), entry);
        }

        var timeLabels = new Dictionary<int, string>();
        foreach (var slot in slots)
        {
            timeLabels[slot.LessonNumber] = $"{Format(slot.StartTime)}-{Format(slot.EndTime)}";
        }

        // N = maksimal dars soati (faol kunlar ichida). Sozlanmagan bo'lsa — mavjud yozuvlarga qarab.
        var maxLessons = await _workDays.GetMaxLessonNumberAsync(ct).ConfigureAwait(false);
        if (maxLessons <= 0)
        {
            maxLessons = relevant.Count == 0 ? 0 : relevant.Max(e => e.LessonNumber);
        }
        else if (relevant.Count > 0)
        {
            // Diapazondan tashqarida qolgan eski yozuvlar ham ko'rinsin.
            maxLessons = Math.Max(maxLessons, relevant.Max(e => e.LessonNumber));
        }

        var blocks = new List<TimetableClassBlockModel>(selectedGroups.Count);
        if (days.Count > 0 && maxLessons > 0)
        {
            foreach (var group in selectedGroups)
            {
                var rows = new List<TimetableRowModel>(maxLessons);
                for (var lesson = 1; lesson <= maxLessons; lesson++)
                {
                    var cells = new TimetableCellModel?[days.Count];
                    for (var d = 0; d < days.Count; d++)
                    {
                        if (!byCell.TryGetValue((group.Id, days[d], lesson), out var entry))
                            continue;

                        cells[d] = ToCell(entry, group, options);
                    }

                    timeLabels.TryGetValue(lesson, out var time);
                    rows.Add(new TimetableRowModel(
                        lesson,
                        $"{lesson.ToString(CultureInfo.InvariantCulture)}-soat",
                        time,
                        cells));
                }

                blocks.Add(new TimetableClassBlockModel(group.Id, group.Name, rows));
            }
        }

        return new TimetableDocumentModel(
            string.IsNullOrWhiteSpace(options.SchoolName) ? null : options.SchoolName!.Trim(),
            days,
            dayNames,
            blocks,
            relevant.Count);
    }

    private static TimetableCellModel ToCell(ScheduleEntry entry, ClassGroup group, PdfExportOptions options)
    {
        var subject = entry.Subject?.Name;
        if (string.IsNullOrWhiteSpace(subject))
            subject = entry.Subject?.Code;
        if (string.IsNullOrWhiteSpace(subject))
            subject = "(fan ko'rsatilmagan)";

        string? teacher = null;
        if (options.IncludeTeacherName)
        {
            teacher = entry.Teacher?.FullName;
            if (string.IsNullOrWhiteSpace(teacher))
                teacher = null;
        }

        string? room = null;
        if (options.IncludeRoom)
        {
            room = entry.RoomNumber;
            if (string.IsNullOrWhiteSpace(room))
                room = group.RoomNumber;
            if (string.IsNullOrWhiteSpace(room))
                room = null;
        }

        return new TimetableCellModel(subject!, teacher, room);
    }

    private static string Format(TimeSpan time) =>
        string.Create(CultureInfo.InvariantCulture, $"{time.Hours:00}:{time.Minutes:00}");
}

/// <summary>
/// "5-A", "5-B", "10-A" kabi nomlarni odam kutgan tartibda saralaydi
/// (oddiy matn taqqoslashda "10-A" "5-A" dan oldin kelib qolardi).
/// </summary>
internal sealed class NaturalNameComparer : IComparer<string>
{
    public static readonly NaturalNameComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        x ??= string.Empty;
        y ??= string.Empty;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var si = i;
                var sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;

                // Bosh nollarni tashlab, avval uzunlik, keyin belgi-belgi taqqoslanadi —
                // shunda juda uzun raqamlarda ham to'lib ketish (overflow) bo'lmaydi.
                var dx = x.AsSpan(si, i - si).TrimStart('0');
                var dy = y.AsSpan(sj, j - sj).TrimStart('0');
                if (dx.Length != dy.Length) return dx.Length.CompareTo(dy.Length);

                var digitCmp = dx.CompareTo(dy, StringComparison.Ordinal);
                if (digitCmp != 0) return digitCmp;
            }
            else
            {
                var cmp = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (cmp != 0) return cmp;
                i++;
                j++;
            }
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
