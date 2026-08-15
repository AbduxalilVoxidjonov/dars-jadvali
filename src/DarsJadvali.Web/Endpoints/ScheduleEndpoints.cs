using DarsJadvali.Application.Export;
using DarsJadvali.Application.Generation;
using DarsJadvali.Infrastructure.Export;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Endpoints;

/// <summary>
/// <b>ESKIRGAN</b> — eski <c>ScheduleEntry</c> modeli asosidagi dars jadvali endpointlari.
/// </summary>
/// <remarks>
/// O'rniga <c>/api/board/*</c> (<see cref="BoardEndpoints"/>) ishlatiladi: u juft darsni
/// (<c>Length</c>), A/B haftani (<c>WeeksMask</c>), guruh bo'linmasini va bazaga
/// saqlanadigan qulfni ham beradi. Bu yerdagi endpointlar Desktop ko'chib bo'lgunicha
/// yonma-yon ishlaydi va har javobda <c>Deprecation: true</c> sarlavhasi bilan belgilanadi.
/// <para>
/// <b>Nega hali o'chirilmadi.</b> Dastur bilan birga keladigan veb sahifasi
/// (<c>wwwroot/index.html</c>) hali shu yo'lni 8 ta joyda chaqiradi: bosh sahifadagi
/// jadval ko'rinishi, tez amallar (tuzish / tekshirish / tozalash) va butun
/// «Dars jadvali (eski)» sahifasi. Bundan tashqari <c>/api/board</c> da "butun jadvalni
/// tozalash" endpointi YO'Q va u sinfni <c>SchoolClass.Id</c> bilan biladi, sahifa esa
/// eski <c>ClassGroup.Id</c> bilan ishlaydi. Shu sababli bu yo'lni olib tashlash
/// sahifaning o'zini ko'chirishni talab qiladi — u alohida ish sifatida rejalashtirilgan.
/// </para>
/// </remarks>
public static class ScheduleEndpoints
{
    /// <summary>"Eskirgan" belgisi sarlavhasi.</summary>
    public const string DeprecationHeader = "Deprecation";

    /// <summary>Eskirgan endpointlarni ro'yxatdan o'tkazadi.</summary>
    /// <param name="api"><c>/api</c> guruhi.</param>
    [Obsolete("Eski ScheduleEntry modeli. O'rniga /api/board/* (MapBoardEndpoints) ishlatiladi.")]
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/schedule");

        // Har javobga "eskirgan" belgisi qo'yiladi — mijoz ko'chganini tekshira oladi.
        group.AddEndpointFilter(async (context, next) =>
        {
            var response = context.HttpContext.Response;
            response.OnStarting(() =>
            {
                response.Headers[DeprecationHeader] = "true";
                response.Headers["Link"] = "</api/board>; rel=\"successor-version\"";
                return Task.CompletedTask;
            });

            return await next(context);
        });

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
            IScopedTimetablePdfExporter exporter,
            CancellationToken ct) =>
        {
            // Parametrlar qo'lda o'qiladi — noto'g'ri qiymatda 500 emas, tushunarli 400 qaytadi.
            var errors = new List<string>();
            var classGroupId = ReadInt(request, "classGroupId", errors);
            var teacherId = ReadInt(request, "teacherId", errors);
            var landscape = ReadBool(request, "landscape", errors);
            var includeTeacher = ReadBool(request, "includeTeacher", errors);
            var includeRoom = ReadBool(request, "includeRoom", errors);

            if (errors.Count > 0)
                return Results.Json(new { error = string.Join(" ", errors) }, statusCode: StatusCodes.Status400BadRequest);

            if (classGroupId is > 0 && teacherId is > 0)
            {
                return Results.Json(
                    new { error = "Bir vaqtda ham sinf, ham o'qituvchi tanlab bo'lmaydi." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var schoolName = request.Query["schoolName"].ToString();

            var options = new PdfExportOptions
            {
                SchoolName = string.IsNullOrWhiteSpace(schoolName) ? null : schoolName.Trim(),
                Landscape = landscape ?? true,
                IncludeTeacherName = includeTeacher ?? true,
                IncludeRoom = includeRoom ?? true
            };

            // Qamrov ANIQ tanlanadi: "scope=school" so'ralmasa, sinf/o'qituvchi ko'rsatilishi shart.
            var scope = request.Query["scope"].ToString();
            var wantsSchool = scope.Equals("school", StringComparison.OrdinalIgnoreCase);

            if (!wantsSchool && classGroupId is not > 0 && teacherId is not > 0)
            {
                return Results.Json(
                    new { error = "Qamrov ko'rsatilmagan: classGroupId, teacherId yoki scope=school bering." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var document = classGroupId is > 0
                ? await exporter.ExportClassScheduleAsync(classGroupId.Value, options, ct)
                : teacherId is > 0
                    ? await exporter.ExportTeacherScheduleAsync(teacherId.Value, options, ct)
                    : await exporter.ExportSchoolScheduleAsync(options, ct);

            return Results.File(document.Content, "application/pdf", document.FileName);
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
