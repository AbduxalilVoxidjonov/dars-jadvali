using System.Text.Json;
using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Endpoints;

/// <summary>Hafta kunlari, dars soatlari va o'qituvchi vaqti (availability).</summary>
public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder api)
    {
        MapWorkDays(api);
        MapLessonSlots(api);
        MapAvailability(api);
    }

    private static void MapWorkDays(IEndpointRouteBuilder api)
    {
        api.MapGet("/workdays", async (IWorkDayService svc, CancellationToken ct) =>
            Results.Ok((await svc.GetAllAsync(ct))
                .OrderBy(x => (int)x.DayOfWeek)
                .Select(x => x.ToDto())));

        api.MapPut("/workdays", async (List<WorkDayDto> items, IWorkDayService svc, CancellationToken ct) =>
        {
            var days = items.Select(x => x.ToEntity()).ToList();
            foreach (var day in days)
                if (day.MaxLessonsPerDay < 1) day.MaxLessonsPerDay = 1;

            await svc.SaveAllAsync(days, ct);

            return Results.Ok((await svc.GetAllAsync(ct))
                .OrderBy(x => (int)x.DayOfWeek)
                .Select(x => x.ToDto()));
        });
    }

    private static void MapLessonSlots(IEndpointRouteBuilder api)
    {
        api.MapGet("/lessonslots", async (IWorkDayService svc, CancellationToken ct) =>
            Results.Ok((await svc.GetLessonSlotsAsync(ct))
                .OrderBy(x => x.LessonNumber)
                .Select(x => x.ToDto())));

        api.MapPut("/lessonslots", async (List<LessonSlotDto> items, IWorkDayService svc, CancellationToken ct) =>
        {
            var slots = items.Select(x => x.ToEntity()).ToList();

            // Tugash vaqti boshlanish vaqtidan keyin bo'lishi shart.
            foreach (var slot in slots)
            {
                if (slot.EndTime <= slot.StartTime)
                {
                    return Results.BadRequest(new
                    {
                        error = $"{slot.LessonNumber}-dars: tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak."
                    });
                }
            }

            await svc.SaveLessonSlotsAsync(slots, ct);

            return Results.Ok((await svc.GetLessonSlotsAsync(ct))
                .OrderBy(x => x.LessonNumber)
                .Select(x => x.ToDto()));
        });
    }

    private static void MapAvailability(IEndpointRouteBuilder api)
    {
        // Yo'q o'qituvchi uchun bo'sh ro'yxat emas, 404 qaytadi —
        // /availability/{id}/lessons bilan bir xil xatti-harakat.
        api.MapGet("/availability/{teacherId:int}",
            async (int teacherId, ITeacherService teachers, IAvailabilityService svc, CancellationToken ct) =>
        {
            if (await teachers.GetByIdAsync(teacherId, ct) is null)
                return Results.NotFound(new { error = "O'qituvchi topilmadi." });

            return Results.Ok((await svc.GetByTeacherAsync(teacherId, ct))
                .OrderBy(x => (int)x.DayOfWeek).ThenBy(x => x.StartTime)
                .Select(x => x.ToDto()));
        });

        api.MapPut("/availability/{teacherId:int}",
            async (int teacherId, List<AvailabilityDto> items,
                   ITeacherService teachers, IAvailabilityService svc, CancellationToken ct) =>
        {
            if (await teachers.GetByIdAsync(teacherId, ct) is null)
                return Results.NotFound(new { error = "O'qituvchi topilmadi." });

            var entities = items.Select(x => x.ToEntity(teacherId)).ToList();
            foreach (var item in entities) item.Id = 0;

            await svc.ReplaceForTeacherAsync(teacherId, entities, ct);

            return Results.Ok((await svc.GetByTeacherAsync(teacherId, ct))
                .OrderBy(x => (int)x.DayOfWeek).ThenBy(x => x.StartTime)
                .Select(x => x.ToDto()));
        });

        MapLessonAvailability(api);
    }

    /// <summary>
    /// Dars soati raqamlari bilan ishlaydigan yangi interfeys (UI shundan foydalanadi).
    /// Vaqt oraliqlariga o'girish IAvailabilityService ichida bajariladi.
    /// </summary>
    private static void MapLessonAvailability(IEndpointRouteBuilder api)
    {
        api.MapGet("/availability/{teacherId:int}/lessons",
            async (int teacherId, ITeacherService teachers, IAvailabilityService svc, CancellationToken ct) =>
        {
            if (await teachers.GetByIdAsync(teacherId, ct) is null)
                return Results.NotFound(new { error = "O'qituvchi topilmadi." });

            var days = await svc.GetLessonAvailabilityAsync(teacherId, ct);
            return Results.Ok(days.Select(d => d.ToDto()).ToList());
        });

        api.MapPut("/availability/{teacherId:int}/lessons",
            async (int teacherId, HttpRequest request,
                   ITeacherService teachers, IAvailabilityService svc, CancellationToken ct) =>
        {
            // Qo'lda o'qiladi: noto'g'ri kun nomida 500 emas, tushunarli 400 qaytadi.
            List<LessonAvailabilityDto>? items;
            try
            {
                items = await request.ReadFromJsonAsync<List<LessonAvailabilityDto>>(ct);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "So'rov tanasi noto'g'ri: kun nomi yoki soat raqamini o'qib bo'lmadi." });
            }

            if (items is null)
                return Results.BadRequest(new { error = "Kunlar ro'yxati yuborilmadi." });

            if (await teachers.GetByIdAsync(teacherId, ct) is null)
                return Results.NotFound(new { error = "O'qituvchi topilmadi." });

            // Bir kun bir marta: takror yuborilsa oxirgisi kuchda qoladi.
            var byDay = new Dictionary<WeekDay, TeacherDayAvailability>();
            foreach (var item in items)
            {
                if (item is null)
                    return Results.BadRequest(new { error = "Kun ma'lumoti bo'sh bo'lishi mumkin emas." });

                if (!Enum.IsDefined(typeof(WeekDay), item.Day))
                    return Results.BadRequest(new { error = "Noto'g'ri hafta kuni." });

                if (item.HasRestriction &&
                    (item.AllowedLessonNumbers ?? Array.Empty<int>()).Any(n => n < 1))
                {
                    return Results.BadRequest(new { error = "Dars soati raqami 1 dan kichik bo'lishi mumkin emas." });
                }

                byDay[item.Day] = item.ToModel();
            }

            await svc.SaveLessonAvailabilityAsync(teacherId, byDay.Values, ct);

            var days = await svc.GetLessonAvailabilityAsync(teacherId, ct);
            return Results.Ok(days.Select(d => d.ToDto()).ToList());
        });
    }
}
