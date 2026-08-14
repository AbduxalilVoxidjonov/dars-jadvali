using System.Diagnostics;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Generation;

/// <summary>
/// Tezkor "ochko'z" (greedy) generator: biriktirmalarni haftalik soati bo'yicha kamayish
/// tartibida oladi va har bir soat uchun validatsiyadan o'tgan birinchi bo'sh joyni band qiladi.
/// Barcha tekshiruvlar xotirada bajariladi — bazaga faqat oxirida yoziladi.
/// </summary>
public sealed class GreedyScheduleGenerator : IScheduleGenerator
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi generator yaratadi.</summary>
    public GreedyScheduleGenerator(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public string Name => "Greedy (tezkor)";

    /// <inheritdoc />
    public string Description =>
        "Biriktirmalarni haftalik soati bo'yicha kamayish tartibida joylashtiradi va " +
        "har bir dars uchun qoidalarga mos keladigan birinchi bo'sh joyni tanlaydi.";

    /// <inheritdoc />
    public async Task<GenerationResult> GenerateAsync(
        GenerationOptions options,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();
        var messages = new List<string>();

        // Faqat bitta jadval bilan ishlaymiz — boshqa yil/variant yozuvlariga tegilmaydi.
        var snapshot = await ScheduleSnapshot.LoadAsync(_uow, options.ScheduleId, ct).ConfigureAwait(false);
        var scheduleId = snapshot.ScheduleId;

        if (options.ClearExisting)
        {
            var removed = await ClearScheduleAsync(snapshot, ct).ConfigureAwait(false);
            if (removed > 0)
            {
                messages.Add($"Eski jadval tozalandi: {removed} ta dars o'chirildi.");
            }
        }

        var workDays = snapshot.ActiveWorkDays;
        if (workDays.Count == 0)
        {
            messages.Add("Faol ish kuni yo'q — avval «Hafta kunlari» bo'limidan ish kunlarini belgilang.");
            stopwatch.Stop();
            return new GenerationResult(false, 0, 0, messages, stopwatch.Elapsed);
        }

        var assignments = OrderAssignments(snapshot.Assignments, options.RandomSeed);
        if (assignments.Count == 0)
        {
            messages.Add("Biriktirmalar yo'q — avval o'qituvchilarga fan va sinf biriktiring.");
            stopwatch.Stop();
            return new GenerationResult(false, 0, 0, messages, stopwatch.Elapsed);
        }

        // Har bir biriktirma bo'yicha qolgan soatlar (ClearExisting=false bo'lsa mavjudlari hisobga olinadi).
        var totalHours = 0;
        var demand = new List<(TeacherAssignment Assignment, int Hours)>();
        foreach (var a in assignments)
        {
            var already = snapshot.Entries.Count(e =>
                e.TeacherId == a.TeacherId && e.SubjectId == a.SubjectId && e.ClassGroupId == a.ClassGroupId);
            var need = Math.Max(0, a.WeeklyHoursCount - already);
            if (need > 0)
            {
                demand.Add((a, need));
                totalHours += need;
            }
        }

        if (totalHours == 0)
        {
            messages.Add("Joylashtirish uchun soat qolmadi — barcha biriktirmalar to'liq qo'yilgan.");
            stopwatch.Stop();
            return new GenerationResult(true, 0, 0, messages, stopwatch.Elapsed);
        }

        var maxIterations = Math.Max(options.MaxIterations, totalHours);
        var newEntries = new List<ScheduleEntry>();
        var placed = 0;
        var unplaced = 0;
        var iterations = 0;
        var cancelled = false;

        progress?.Report(new GenerationProgress(0, totalHours, 0d, $"Boshlandi: jami {totalHours} soat."));

        foreach (var (assignment, hours) in demand)
        {
            if (cancelled)
            {
                break;
            }

            var teacherName = snapshot.TeacherName(assignment.TeacherId);
            var className = snapshot.ClassName(assignment.ClassGroupId);
            var subjectName = snapshot.SubjectName(assignment.SubjectId);
            var room = snapshot.ClassRoom(assignment.ClassGroupId);

            for (var i = 0; i < hours; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                if (++iterations > maxIterations)
                {
                    messages.Add($"Iteratsiyalar chegarasi ({maxIterations}) tugadi, jarayon to'xtatildi.");
                    cancelled = true;
                    break;
                }

                var entry = TryPlace(snapshot, assignment, workDays, room, scheduleId);
                if (entry is null)
                {
                    unplaced++;
                    messages.Add(
                        $"{className} sinfi, {subjectName} ({teacherName}): bo'sh joy topilmadi — " +
                        $"{hours - i} soat qo'yilmadi.");
                    progress?.Report(new GenerationProgress(
                        placed + unplaced, totalHours, Fitness(placed, totalHours),
                        $"{className} — {subjectName}: joy topilmadi."));

                    // Qolgan soatlar ham joylashmaydi, ularni ham hisobga olamiz.
                    unplaced += hours - i - 1;
                    break;
                }

                snapshot.Add(entry);
                newEntries.Add(entry);
                placed++;

                progress?.Report(new GenerationProgress(
                    placed + unplaced, totalHours, Fitness(placed, totalHours),
                    $"{className} — {subjectName} ({teacherName}): " +
                    $"{entry.DayOfWeek.ToUzbek()} {entry.LessonNumber}-soat."));
            }
        }

        foreach (var entry in newEntries)
        {
            await _uow.ScheduleEntries.AddAsync(entry, CancellationToken.None).ConfigureAwait(false);
        }

        if (newEntries.Count > 0)
        {
            await _uow.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }

        stopwatch.Stop();

        if (cancelled && ct.IsCancellationRequested)
        {
            messages.Add("Jarayon to'xtatildi — qo'yilgan darslar saqlandi.");
        }

        messages.Insert(0, unplaced == 0 && !cancelled
            ? $"Jadval tayyor: {placed} ta dars qo'yildi."
            : $"{placed} ta dars qo'yildi, {unplaced} ta soat joylashtirilmadi.");

        progress?.Report(new GenerationProgress(
            placed + unplaced, totalHours, Fitness(placed, totalHours), messages[0]));

        return new GenerationResult(unplaced == 0 && !cancelled, placed, unplaced, messages, stopwatch.Elapsed);
    }

    /// <summary>Qoidalarga mos birinchi bo'sh joyni topadi.</summary>
    private static ScheduleEntry? TryPlace(
        ScheduleSnapshot snapshot,
        TeacherAssignment assignment,
        IReadOnlyList<WorkDay> workDays,
        string? room,
        int scheduleId)
    {
        ScheduleEntryDraft? fallback = null;

        foreach (var day in workDays)
        {
            for (var lesson = 1; lesson <= day.MaxLessonsPerDay; lesson++)
            {
                var draft = new ScheduleEntryDraft(
                    null,
                    assignment.ClassGroupId,
                    assignment.SubjectId,
                    assignment.TeacherId,
                    day.DayOfWeek,
                    lesson,
                    room,
                    scheduleId);

                var conflicts = snapshot.Validate(draft);
                if (conflicts.Count == 0)
                {
                    return ToEntry(draft);
                }

                // Faqat ogohlantirish bo'lsa — zaxira variant sifatida saqlaymiz.
                if (fallback is null && conflicts.All(c => c.Severity == ConflictSeverity.Warning))
                {
                    fallback = draft;
                }
            }
        }

        return fallback is null ? null : ToEntry(fallback);
    }

    private static ScheduleEntry ToEntry(ScheduleEntryDraft draft) => new()
    {
        ScheduleId = draft.ScheduleId ?? 0,
        ClassGroupId = draft.ClassGroupId,
        SubjectId = draft.SubjectId,
        TeacherId = draft.TeacherId,
        DayOfWeek = draft.DayOfWeek,
        LessonNumber = draft.LessonNumber,
        RoomNumber = draft.RoomNumber
    };

    private static List<TeacherAssignment> OrderAssignments(
        IReadOnlyList<TeacherAssignment> assignments, int? seed)
    {
        var ordered = assignments.Where(a => a.WeeklyHoursCount > 0);

        if (seed.HasValue)
        {
            var random = new Random(seed.Value);
            return ordered
                .OrderByDescending(a => a.WeeklyHoursCount)
                .ThenBy(_ => random.Next())
                .ToList();
        }

        return ordered
            .OrderByDescending(a => a.WeeklyHoursCount)
            .ThenBy(a => a.ClassGroupId)
            .ThenBy(a => a.SubjectId)
            .ToList();
    }

    private async Task<int> ClearScheduleAsync(ScheduleSnapshot snapshot, CancellationToken ct)
    {
        var ids = snapshot.Entries.Select(e => e.Id).Where(id => id > 0).ToList();
        foreach (var id in ids)
        {
            await _uow.ScheduleEntries.DeleteAsync(id, ct).ConfigureAwait(false);
        }

        if (ids.Count > 0)
        {
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        snapshot.ClearEntries();
        return ids.Count;
    }

    private static double Fitness(int placed, int total) =>
        total <= 0 ? 0d : Math.Round((double)placed / total, 4);
}
