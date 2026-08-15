using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Services;

/// <summary>
/// Bir kun uchun o'qituvchining bandligi — DARS SOATI raqamlari bilan.
/// <para><c>HasRestriction == false</c> — o'sha kuni cheklov yo'q (barcha soatlarda dars o'ta oladi).</para>
/// <para><c>HasRestriction == true</c> — FAQAT <c>AllowedLessonNumbers</c> dagi soatlarda dars o'ta oladi
/// (ro'yxat bo'sh bo'lsa — o'sha kuni umuman ishlamaydi).</para>
/// </summary>
public sealed record TeacherDayAvailability(
    WeekDay Day,
    bool HasRestriction,
    IReadOnlyList<int> AllowedLessonNumbers);

/// <summary>O'qituvchining ish vaqti oraliqlari servisi.</summary>
public interface IAvailabilityService
{
    /// <summary>O'qituvchi bo'yicha oraliqlar.</summary>
    Task<IReadOnlyList<TeacherAvailability>> GetByTeacherAsync(int teacherId, CancellationToken ct = default);

    /// <summary>Yangi oraliq qo'shadi.</summary>
    Task<TeacherAvailability> CreateAsync(TeacherAvailability a, CancellationToken ct = default);

    /// <summary>Oraliqni yangilaydi.</summary>
    Task UpdateAsync(TeacherAvailability a, CancellationToken ct = default);

    /// <summary>Oraliqni o'chiradi.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>O'qituvchining barcha oraliqlarini yangilari bilan almashtiradi.</summary>
    Task ReplaceForTeacherAsync(int teacherId, IEnumerable<TeacherAvailability> items, CancellationToken ct = default);

    // --- Dars soati bo'yicha interfeys (UI shundan foydalanadi) ---

    /// <summary>Har bir FAOL ish kuni uchun bitta yozuv qaytaradi (kun tartibida).</summary>
    Task<IReadOnlyList<TeacherDayAvailability>> GetLessonAvailabilityAsync(
        int teacherId, CancellationToken ct = default);

    /// <summary>
    /// <b>Ommaviy variant:</b> BARCHA o'qituvchilar uchun bandlikni <b>bitta</b> o'qishda
    /// qaytaradi (o'qituvchi Id → kunlar ro'yxati).
    /// </summary>
    /// <remarks>
    /// Ilgari UI har bir o'qituvchi uchun <see cref="GetLessonAvailabilityAsync"/> ni
    /// alohida chaqirardi: 40 ta o'qituvchi = 40 ta so'rov to'plami (har biri 3 ta
    /// to'liq <c>SELECT</c>). Bu yerda ish kunlari, dars soatlari va oraliqlar
    /// bir marta o'qiladi va o'girish xotirada bajariladi.
    /// <para>
    /// Cheklovi yo'q o'qituvchi ham natijada bo'ladi (barcha kunlar
    /// <c>HasRestriction = false</c> bilan) — chaqiruvchi <c>TryGetValue</c> ni
    /// tekshirishga majbur bo'lmasligi uchun.
    /// </para>
    /// </remarks>
    Task<IReadOnlyDictionary<int, IReadOnlyList<TeacherDayAvailability>>> GetLessonAvailabilityForAllAsync(
        CancellationToken ct = default);

    /// <summary>Berilgan kunlar bo'yicha bandlikni to'liq ALMASHTIRADI.</summary>
    Task SaveLessonAvailabilityAsync(
        int teacherId, IEnumerable<TeacherDayAvailability> days, CancellationToken ct = default);
}

/// <summary><see cref="IAvailabilityService"/> implementatsiyasi.</summary>
public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IUnitOfWork _uow;

    /// <summary>Yangi servis yaratadi.</summary>
    public AvailabilityService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherAvailability>> GetByTeacherAsync(int teacherId, CancellationToken ct = default)
    {
        var all = await _uow.Availabilities.GetAllAsync(ct).ConfigureAwait(false);
        return all
            .Where(a => a.TeacherId == teacherId)
            .OrderBy(a => (int)a.DayOfWeek)
            .ThenBy(a => a.StartTime)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<TeacherAvailability> CreateAsync(TeacherAvailability a, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        var created = await _uow.Availabilities.AddAsync(a, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TeacherAvailability a, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        await _uow.Availabilities.UpdateAsync(a, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _uow.Availabilities.DeleteAsync(id, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReplaceForTeacherAsync(
        int teacherId, IEnumerable<TeacherAvailability> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var all = await _uow.Availabilities.GetAllAsync(ct).ConfigureAwait(false);
        foreach (var old in all.Where(a => a.TeacherId == teacherId))
        {
            await _uow.Availabilities.DeleteAsync(old.Id, ct).ConfigureAwait(false);
        }

        foreach (var item in items)
        {
            item.TeacherId = teacherId;
            item.Id = 0;
            await _uow.Availabilities.AddAsync(item, ct).ConfigureAwait(false);
        }

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------
    // Dars soati bo'yicha interfeys
    // -----------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeacherDayAvailability>> GetLessonAvailabilityAsync(
        int teacherId, CancellationToken ct = default)
    {
        // Ma'lumot bir marta yuklanadi — siklda bazaga borilmaydi.
        var workDays = await _uow.WorkDays.GetAllAsync(ct).ConfigureAwait(false);
        var slots = (await _uow.LessonSlots.GetAllAsync(ct).ConfigureAwait(false))
            .OrderBy(s => s.LessonNumber)
            .ToList();
        var availabilities = (await _uow.Availabilities.GetAllAsync(ct).ConfigureAwait(false))
            .Where(a => a.TeacherId == teacherId)
            .ToList();

        return Convert(availabilities, ActiveDays(workDays), slots);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<TeacherDayAvailability>>>
        GetLessonAvailabilityForAllAsync(CancellationToken ct = default)
    {
        // BITTA o'qish to'plami — o'qituvchilar soniga bog'liq emas.
        var teachers = await _uow.Teachers.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var workDays = await _uow.WorkDays.GetAllReadOnlyAsync(ct).ConfigureAwait(false);
        var slots = (await _uow.LessonSlots.GetAllReadOnlyAsync(ct).ConfigureAwait(false))
            .OrderBy(s => s.LessonNumber)
            .ToList();
        var availabilities = await _uow.Availabilities.GetAllReadOnlyAsync(ct).ConfigureAwait(false);

        var days = ActiveDays(workDays);
        var byTeacher = availabilities
            .GroupBy(a => a.TeacherId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TeacherAvailability>)g.ToList());

        var result = new Dictionary<int, IReadOnlyList<TeacherDayAvailability>>(teachers.Count);
        foreach (var teacher in teachers)
        {
            var items = byTeacher.TryGetValue(teacher.Id, out var list)
                ? list
                : Array.Empty<TeacherAvailability>();

            result[teacher.Id] = Convert(items, days, slots);
        }

        return result;
    }

    /// <summary>Faol ish kunlari, kun tartibida.</summary>
    private static List<WorkDay> ActiveDays(IEnumerable<WorkDay> workDays) =>
        workDays.Where(d => d.IsActive).OrderBy(d => (int)d.DayOfWeek).ToList();

    /// <summary>
    /// Vaqt oraliqlarini dars soati raqamlariga o'giradi. Qoida
    /// <see cref="LessonAvailabilityRules"/> dan olinadi — validatsiya bilan bir manbadan.
    /// </summary>
    private static IReadOnlyList<TeacherDayAvailability> Convert(
        IReadOnlyList<TeacherAvailability> items,
        IReadOnlyList<WorkDay> activeDays,
        IReadOnlyList<LessonSlot> slots)
    {
        var byDay = items
            .GroupBy(a => a.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<TeacherDayAvailability>(activeDays.Count);

        foreach (var day in activeDays)
        {
            if (!byDay.TryGetValue(day.DayOfWeek, out var dayItems) || dayItems.Count == 0)
            {
                // Yozuv yo'q — cheklov ham yo'q.
                result.Add(new TeacherDayAvailability(day.DayOfWeek, false, Array.Empty<int>()));
                continue;
            }

            var allowed = slots
                .Where(s => LessonAvailabilityRules.IsAllowed(dayItems, s.StartTime, s.EndTime))
                .Select(s => s.LessonNumber)
                .ToList();

            result.Add(new TeacherDayAvailability(day.DayOfWeek, true, allowed));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SaveLessonAvailabilityAsync(
        int teacherId, IEnumerable<TeacherDayAvailability> days, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(days);

        var slots = (await _uow.LessonSlots.GetAllAsync(ct).ConfigureAwait(false))
            .OrderBy(s => s.LessonNumber)
            .ToList();

        var items = new List<TeacherAvailability>();

        // Dars soatlari umuman sozlanmagan bo'lsa — o'girib bo'lmaydi, lekin istisno ham tashlanmaydi.
        if (slots.Count > 0)
        {
            var byNumber = new Dictionary<int, LessonSlot>();
            foreach (var slot in slots)
            {
                byNumber.TryAdd(slot.LessonNumber, slot);
            }

            var dayStart = slots.Min(s => s.StartTime);
            var dayEnd = slots.Max(s => s.EndTime);

            foreach (var day in days)
            {
                if (day is null || !day.HasRestriction)
                {
                    // Cheklov yo'q — bu kun uchun umuman yozuv yozilmaydi.
                    continue;
                }

                // Noma'lum soat raqamlari e'tiborsiz qoldiriladi.
                var numbers = (day.AllowedLessonNumbers ?? Array.Empty<int>())
                    .Where(byNumber.ContainsKey)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                if (numbers.Count == 0)
                {
                    // Cheklov bor, lekin bironta soat ruxsat etilmagan → o'sha kuni umuman ishlamaydi.
                    // Bo'sh oq ro'yxat "cheklov yo'q" degan ma'noni berardi, shuning uchun
                    // butun kunni qamrab oluvchi qora ro'yxat yozuvi yoziladi.
                    items.Add(new TeacherAvailability
                    {
                        TeacherId = teacherId,
                        DayOfWeek = day.Day,
                        StartTime = dayStart,
                        EndTime = dayEnd,
                        IsAvailable = false
                    });
                    continue;
                }

                // Ketma-ket soatlar bitta oraliqqa birlashtiriladi: 1,2,3,5 → (1..3) va (5..5).
                var rangeStart = numbers[0];
                var previous = numbers[0];

                for (var i = 1; i <= numbers.Count; i++)
                {
                    var isLast = i == numbers.Count;
                    if (!isLast && numbers[i] == previous + 1)
                    {
                        previous = numbers[i];
                        continue;
                    }

                    items.Add(new TeacherAvailability
                    {
                        TeacherId = teacherId,
                        DayOfWeek = day.Day,
                        StartTime = byNumber[rangeStart].StartTime,
                        EndTime = byNumber[previous].EndTime,
                        IsAvailable = true
                    });

                    if (!isLast)
                    {
                        rangeStart = numbers[i];
                        previous = numbers[i];
                    }
                }
            }
        }

        // To'liq almashtirish: eski yozuvlar o'chiriladi, yangilari yoziladi.
        await ReplaceForTeacherAsync(teacherId, items, ct).ConfigureAwait(false);
    }
}
