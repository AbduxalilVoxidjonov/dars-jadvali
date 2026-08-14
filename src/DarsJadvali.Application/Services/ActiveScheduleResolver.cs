using DarsJadvali.Application.Abstractions;
using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Application.Services;

/// <summary>
/// Faol dars jadvalini aniqlaydigan umumiy yordamchi.
/// Barcha servislar (jadval, validator, generator, eksport) shu orqali ishlaydi —
/// shunda hech bir joyda "qaysi jadval?" filtri tushib qolmaydi.
/// Baza butunlay bo'sh bo'lsa — o'quv yili va "Asosiy jadval" avtomatik yaratiladi,
/// ya'ni dastur hech qachon jadvalsiz qolmaydi.
/// </summary>
public static class ActiveScheduleResolver
{
    /// <summary>Standart jadval nomi.</summary>
    public const string DefaultScheduleName = "Asosiy jadval";

    /// <summary>Joriy sanadan o'quv yili nomini hisoblaydi, masalan "2025–2026".</summary>
    /// <param name="now">Joriy sana (odatda <c>DateTime.Now</c>).</param>
    public static (string Name, int StartYear) CurrentAcademicYearName(DateTime now)
    {
        // Sentyabrgacha bo'lgan oylar oldingi o'quv yiliga tegishli.
        var startYear = now.Month >= 9 ? now.Year : now.Year - 1;
        return ($"{startYear}–{startYear + 1}", startYear);
    }

    /// <summary>
    /// Faol jadvalni qaytaradi. Kerak bo'lsa o'quv yili va jadvalni yaratadi,
    /// hech biri faol bo'lmasa — eng eskisini faol qilib qo'yadi.
    /// </summary>
    public static async Task<Schedule> GetActiveAsync(IUnitOfWork uow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uow);

        var schedules = await uow.Schedules.GetAllAsync(ct).ConfigureAwait(false);

        var active = schedules.FirstOrDefault(s => s.IsActive);
        if (active is not null)
        {
            // Bir nechtasi faol bo'lib qolgan bo'lsa — faqat bittasini qoldiramiz.
            var extras = schedules.Where(s => s.IsActive && s.Id != active.Id).ToList();
            if (extras.Count > 0)
            {
                foreach (var extra in extras)
                {
                    extra.IsActive = false;
                    await uow.Schedules.UpdateAsync(extra, ct).ConfigureAwait(false);
                }

                await uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return active;
        }

        // Jadval bor, lekin hech biri faol emas — eng eskisini faol qilamiz.
        if (schedules.Count > 0)
        {
            var first = schedules.OrderBy(s => s.Id).First();
            first.IsActive = true;
            await uow.Schedules.UpdateAsync(first, ct).ConfigureAwait(false);
            await uow.SaveChangesAsync(ct).ConfigureAwait(false);
            return first;
        }

        // Umuman jadval yo'q — o'quv yili bilan birga yaratamiz.
        var years = await uow.AcademicYears.GetAllAsync(ct).ConfigureAwait(false);
        var year = years.OrderByDescending(y => y.StartYear).ThenByDescending(y => y.Id).FirstOrDefault();
        if (year is null)
        {
            var (name, startYear) = CurrentAcademicYearName(DateTime.Now);
            year = await uow.AcademicYears.AddAsync(
                new AcademicYear { Name = name, StartYear = startYear }, ct).ConfigureAwait(false);
        }

        var created = await uow.Schedules.AddAsync(new Schedule
        {
            AcademicYearId = year.Id,
            Name = DefaultScheduleName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        }, ct).ConfigureAwait(false);

        await uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return created;
    }

    /// <summary>
    /// Ko'rsatilgan jadval Id sini tekshiradi; <c>null</c> bo'lsa faol jadval Id sini qaytaradi.
    /// </summary>
    public static async Task<int> ResolveIdAsync(
        IUnitOfWork uow, int? scheduleId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uow);

        if (scheduleId.HasValue && scheduleId.Value > 0)
        {
            return scheduleId.Value;
        }

        var active = await GetActiveAsync(uow, ct).ConfigureAwait(false);
        return active.Id;
    }
}
