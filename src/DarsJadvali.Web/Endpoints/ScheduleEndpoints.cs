using DarsJadvali.Application.Export;
using DarsJadvali.Application.Generation;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Endpoints;

/// <summary>Dars jadvali: ko'rish, joylashtirish, ko'chirish, avtomatik tuzish, tekshirish.</summary>
public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/schedule");

        group.MapGet("/", async (int? classGroupId, int? teacherId, IScheduleService svc, CancellationToken ct) =>
        {
            var items = classGroupId is > 0
                ? await svc.GetByClassGroupAsync(classGroupId.Value, ct)
                : teacherId is > 0
                    ? await svc.GetByTeacherAsync(teacherId.Value, ct)
                    : await svc.GetAllAsync(ct);

            return Results.Ok(items
                .OrderBy(x => (int)x.DayOfWeek).ThenBy(x => x.LessonNumber)
                .Select(x => x.ToDto()));
        });

        // Validatsiya xatolari 200 bilan PlacementResult ichida qaytadi — UI ularni ro'yxat qilib ko'rsatadi.
        group.MapPost("/place", async (ScheduleDraftRequest body, bool? force, IScheduleService svc, CancellationToken ct) =>
        {
            var result = await svc.PlaceAsync(body.ToDraft(), force ?? false, ct);
            return Results.Ok(result.ToDto());
        });

        group.MapPost("/move", async (MoveRequest body, IScheduleService svc, CancellationToken ct) =>
        {
            var result = await svc.MoveAsync(body.EntryId, body.DayOfWeek, body.LessonNumber, body.Force, ct);
            return Results.Ok(result.ToDto());
        });

        group.MapDelete("/{id:int}", async (int id, IScheduleService svc, CancellationToken ct) =>
        {
            await svc.RemoveAsync(id, ct);
            return Results.NoContent();
        });

        // Butun jadvalni yoki bitta sinf jadvalini tozalash.
        group.MapDelete("/", async (int? classGroupId, IScheduleService svc, CancellationToken ct) =>
        {
            await svc.ClearAsync(classGroupId is > 0 ? classGroupId : null, ct);
            return Results.NoContent();
        });

        group.MapPost("/generate", async (
            GenerationOptionsRequest? body,
            IScheduleGenerator generator,
            CancellationToken ct) =>
        {
            var result = await generator.GenerateAsync(body.ToOptions(), null, ct);
            return Results.Ok(result.ToDto());
        });

        group.MapGet("/validate", async (IScheduleValidator validator, CancellationToken ct) =>
        {
            var result = await validator.ValidateAllAsync(ct);
            return Results.Ok(result.ToDto());
        });

        // Jadvalni PDF ga eksport qilish. Barcha parametrlar ixtiyoriy.
        //   GET /api/schedule/pdf?classGroupId=&schoolName=&landscape=&includeTeacher=&includeRoom=
        // Fayl nomi berilgani uchun Results.File "Content-Disposition: attachment" qo'yadi —
        // ya'ni brauzer PDF ni ochmasdan yuklab oladi.
        group.MapGet("/pdf", async (
            HttpRequest request,
            ISchoolTimetablePdfExporter exporter,
            CancellationToken ct) =>
        {
            // Parametrlar qo'lda o'qiladi — noto'g'ri qiymatda 500 emas, tushunarli 400 qaytadi.
            var errors = new List<string>();
            var classGroupId = ReadInt(request, "classGroupId", errors);
            var landscape = ReadBool(request, "landscape", errors);
            var includeTeacher = ReadBool(request, "includeTeacher", errors);
            var includeRoom = ReadBool(request, "includeRoom", errors);

            if (errors.Count > 0)
                return Results.Json(new { error = string.Join(" ", errors) }, statusCode: StatusCodes.Status400BadRequest);

            var schoolName = request.Query["schoolName"].ToString();

            var options = new PdfExportOptions
            {
                ClassGroupId = classGroupId is > 0 ? classGroupId : null,
                SchoolName = string.IsNullOrWhiteSpace(schoolName) ? null : schoolName.Trim(),
                Landscape = landscape ?? true,
                IncludeTeacherName = includeTeacher ?? true,
                IncludeRoom = includeRoom ?? true
            };

            var pdf = await exporter.ExportAsync(options, ct);
            var fileName = exporter.SuggestFileName(options, DateTime.Now);

            return Results.File(pdf, "application/pdf", fileName);
        });
    }

    /// <summary>So'rovdan butun son o'qiydi; qiymat bo'lmasa <c>null</c>, xato bo'lsa xabar qo'shadi.</summary>
    private static int? ReadInt(HttpRequest request, string name, List<string> errors)
    {
        var raw = request.Query[name].ToString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (int.TryParse(raw, out var value)) return value;

        errors.Add($"«{name}» butun son bo'lishi kerak.");
        return null;
    }

    /// <summary>So'rovdan mantiqiy qiymat o'qiydi ("true/false" yoki "1/0").</summary>
    private static bool? ReadBool(HttpRequest request, string name, List<string> errors)
    {
        var raw = request.Query[name].ToString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (bool.TryParse(raw, out var value)) return value;
        if (raw == "1") return true;
        if (raw == "0") return false;

        errors.Add($"«{name}» true yoki false bo'lishi kerak.");
        return null;
    }
}
