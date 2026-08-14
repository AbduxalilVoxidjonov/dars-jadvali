using DarsJadvali.Application.Services;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Endpoints;

/// <summary>
/// O'quv yillari va dars jadvali <b>variantlari</b> uchun endpoint'lar.
/// Dars yozuvlarining o'zi bilan <see cref="ScheduleEndpoints"/> ishlaydi.
/// </summary>
public static class ScheduleSetEndpoints
{
    public static void MapScheduleSetEndpoints(this IEndpointRouteBuilder api)
    {
        MapAcademicYears(api);
        MapSchedules(api);
    }

    // ---------------------------------------------------------------------
    // O'quv yillari
    // ---------------------------------------------------------------------
    private static void MapAcademicYears(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/academicyears");

        group.MapGet("/", async (IAcademicYearService years, IScheduleSetService sets, CancellationToken ct) =>
        {
            var all = await years.GetAllAsync(ct);
            var schedules = await sets.GetAllAsync(ct);
            return Results.Ok(all.Select(y =>
                y.ToDto(schedules.Count(s => s.AcademicYearId == y.Id))));
        });

        group.MapGet("/{id:int}", async (int id, IAcademicYearService years, IScheduleSetService sets, CancellationToken ct) =>
        {
            var year = await years.GetByIdAsync(id, ct);
            if (year is null)
                return Results.NotFound(new { error = "O'quv yili topilmadi." });

            var schedules = await sets.GetByAcademicYearAsync(id, ct);
            return Results.Ok(year.ToDto(schedules.Count));
        });

        group.MapPost("/", async (AcademicYearRequest body, IAcademicYearService years, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body?.Name))
                return Results.BadRequest(new { error = "O'quv yili nomi bo'sh bo'lmasligi kerak." });

            return await GuardAsync(async () =>
            {
                var startYear = body.StartYear ?? GuessStartYear(body.Name);
                var created = await years.CreateAsync(body.Name, startYear, body.Note, ct);
                return Results.Ok(created.ToDto(0));
            });
        });

        group.MapPut("/{id:int}", async (int id, AcademicYearRequest body, IAcademicYearService years, IScheduleSetService sets, CancellationToken ct) =>
        {
            if (await years.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "O'quv yili topilmadi." });
            if (string.IsNullOrWhiteSpace(body?.Name))
                return Results.BadRequest(new { error = "O'quv yili nomi bo'sh bo'lmasligi kerak." });

            return await GuardAsync(async () =>
            {
                await years.RenameAsync(id, body.Name, body.StartYear, body.Note, ct);
                var year = await years.GetByIdAsync(id, ct);
                var schedules = await sets.GetByAcademicYearAsync(id, ct);
                return year is null
                    ? Results.NotFound(new { error = "O'quv yili topilmadi." })
                    : Results.Ok(year.ToDto(schedules.Count));
            });
        });

        // Kaskad: o'quv yili → jadvallari → dars yozuvlari.
        // Oxirgi o'quv yilini o'chirib bo'lmaydi — servis InvalidOperationException tashlaydi,
        // uni 500 emas, tushunarli 400 qilib qaytaramiz.
        group.MapDelete("/{id:int}", async (int id, IAcademicYearService years, CancellationToken ct) =>
        {
            if (await years.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "O'quv yili topilmadi." });

            return await GuardAsync(async () =>
            {
                await years.DeleteAsync(id, ct);
                return Results.NoContent();
            });
        });
    }

    // ---------------------------------------------------------------------
    // Dars jadvali variantlari
    // ---------------------------------------------------------------------
    private static void MapSchedules(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/schedules");

        group.MapGet("/", async (int? academicYearId, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct) =>
        {
            var items = academicYearId is > 0
                ? await sets.GetByAcademicYearAsync(academicYearId.Value, ct)
                : await sets.GetAllAsync(ct);

            return Results.Ok(await ToDtosAsync(items, sets, years, ct));
        });

        group.MapGet("/active", async (IScheduleSetService sets, IAcademicYearService years, CancellationToken ct) =>
        {
            var active = await sets.GetActiveAsync(ct);
            return Results.Ok(await ToDtoAsync(active, sets, years, ct));
        });

        group.MapGet("/{id:int}", async (int id, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct) =>
        {
            var schedule = await sets.GetByIdAsync(id, ct);
            return schedule is null
                ? Results.NotFound(new { error = "Dars jadvali topilmadi." })
                : Results.Ok(await ToDtoAsync(schedule, sets, years, ct));
        });

        group.MapPost("/", async (ScheduleSetRequest body, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body?.Name))
                return Results.BadRequest(new { error = "Jadval nomi bo'sh bo'lmasligi kerak." });

            return await GuardAsync(async () =>
            {
                // O'quv yili ko'rsatilmasa — joriy (yoki eng yangi) yil ichida yaratiladi.
                var yearId = body.AcademicYearId is > 0
                    ? body.AcademicYearId.Value
                    : (await years.GetOrCreateCurrentAsync(ct)).Id;

                var created = await sets.CreateAsync(yearId, body.Name, ct);
                return Results.Ok(await ToDtoAsync(created, sets, years, ct));
            });
        });

        group.MapPut("/{id:int}", async (int id, ScheduleSetRequest body, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct) =>
        {
            if (await sets.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Dars jadvali topilmadi." });
            if (string.IsNullOrWhiteSpace(body?.Name))
                return Results.BadRequest(new { error = "Jadval nomi bo'sh bo'lmasligi kerak." });

            return await GuardAsync(async () =>
            {
                await sets.RenameAsync(id, body.Name, ct);
                var schedule = await sets.GetByIdAsync(id, ct);
                return schedule is null
                    ? Results.NotFound(new { error = "Dars jadvali topilmadi." })
                    : Results.Ok(await ToDtoAsync(schedule, sets, years, ct));
            });
        });

        // Nusxa barcha dars yozuvlari bilan olinadi; asl jadvalga tegilmaydi.
        group.MapPost("/{id:int}/duplicate", async (int id, ScheduleSetRequest? body, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct) =>
        {
            if (await sets.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Dars jadvali topilmadi." });

            return await GuardAsync(async () =>
            {
                var copy = await sets.DuplicateAsync(id, body?.Name, ct);
                return Results.Ok(await ToDtoAsync(copy, sets, years, ct));
            });
        });

        group.MapPost("/{id:int}/activate", async (int id, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct) =>
        {
            if (await sets.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Dars jadvali topilmadi." });

            return await GuardAsync(async () =>
            {
                await sets.SetActiveAsync(id, ct);
                var active = await sets.GetActiveAsync(ct);
                return Results.Ok(await ToDtoAsync(active, sets, years, ct));
            });
        });

        // Oxirgi jadvalni o'chirib bo'lmaydi → 400 (o'zbekcha xabar bilan).
        group.MapDelete("/{id:int}", async (int id, IScheduleSetService sets, CancellationToken ct) =>
        {
            if (await sets.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Dars jadvali topilmadi." });

            return await GuardAsync(async () =>
            {
                await sets.DeleteAsync(id, ct);
                return Results.NoContent();
            });
        });
    }

    // ---------------------------------------------------------------------
    // Yordamchilar
    // ---------------------------------------------------------------------

    /// <summary>
    /// Servis <see cref="InvalidOperationException"/> tashlasa (masalan «oxirgi jadval»),
    /// foydalanuvchiga 500 emas, o'zbekcha 400 qaytadi.
    /// </summary>
    private static async Task<IResult> GuardAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<ScheduleSetDto> ToDtoAsync(
        Schedule schedule, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct)
    {
        var count = await sets.GetEntryCountAsync(schedule.Id, ct);
        var year = schedule.AcademicYear ?? await years.GetByIdAsync(schedule.AcademicYearId, ct);
        return schedule.ToDto(count, year?.Name);
    }

    private static async Task<IReadOnlyList<ScheduleSetDto>> ToDtosAsync(
        IReadOnlyList<Schedule> schedules, IScheduleSetService sets, IAcademicYearService years, CancellationToken ct)
    {
        var allYears = await years.GetAllAsync(ct);
        var nameById = allYears.ToDictionary(y => y.Id, y => y.Name);

        var result = new List<ScheduleSetDto>(schedules.Count);
        foreach (var schedule in schedules)
        {
            var count = await sets.GetEntryCountAsync(schedule.Id, ct);
            nameById.TryGetValue(schedule.AcademicYearId, out var yearName);
            result.Add(schedule.ToDto(count, yearName));
        }

        return result;
    }

    /// <summary>"2025–2026" dan 2025 ni ajratadi; topilmasa joriy kalendar yili.</summary>
    private static int GuessStartYear(string name)
    {
        var digits = new string(name.TakeWhile(char.IsDigit).ToArray());
        return digits.Length == 4 && int.TryParse(digits, out var year) ? year : DateTime.Now.Year;
    }
}
