using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Export;
using DarsJadvali.Application.Scheduling;
using DarsJadvali.Application.Services;
using DarsJadvali.Application.Validation;
using DarsJadvali.Infrastructure.Export.Printing;
using DarsJadvali.Scheduling.Pipeline;
using DarsJadvali.Web.Dtos;
using DarsJadvali.Web.Services;

namespace DarsJadvali.Web.Endpoints;

/// <summary>
/// Jadval to'ri — YANGI <c>Card</c>/<c>Lesson</c> modeli.
/// </summary>
/// <remarks>
/// Eski <c>/api/schedule</c> (<c>ScheduleEntry</c>) endpointlari o'chirilmadi, ammo
/// eskirgan deb belgilangan: Desktop ko'chib bo'lgach ular olib tashlanadi.
/// Bu yerdagi yo'l ularning o'rnini bosadi va qo'shimcha ravishda juft dars
/// (<c>Length</c>), A/B hafta (<c>WeeksMask</c>), guruh bo'linmasi va qulfni beradi.
/// </remarks>
public static class BoardEndpoints
{
    /// <summary>To'r endpointlarini ro'yxatdan o'tkazadi.</summary>
    /// <param name="api"><c>/api</c> guruhi.</param>
    public static void MapBoardEndpoints(this IEndpointRouteBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var group = api.MapGroup("/board");

        // -----------------------------------------------------------------
        // O'qish
        // -----------------------------------------------------------------

        // To'rning o'qlari va ma'lumotnomalari — sahifa buni bir marta oladi.
        group.MapGet("/axes", async (
            int? scheduleId,
            IUnitOfWork uow,
            ISchedulingStore store,
            CancellationToken ct) =>
        {
            var id = await ActiveScheduleResolver.ResolveIdAsync(uow, scheduleId, ct);
            var input = await store.LoadAsync(id, ct);

            var days = CardPrintableAdapter.ToDays(input.WorkDays)
                .Select(d => new BoardDayDto(d.Index, d.Name, d.DisplayShort))
                .ToList();

            var shiftById = input.Shifts.ToDictionary(s => s.Id);

            var periods = input.Periods
                .Where(p => !p.IsBreak)
                .OrderBy(p => p.PeriodNo)
                .Select(p => new BoardPeriodDto(
                    p.Id,
                    p.PeriodNo,
                    string.IsNullOrWhiteSpace(p.ShortName) ? p.PeriodNo.ToString() : p.ShortName!,
                    p.StartTime.ToString("HH:mm"),
                    p.EndTime.ToString("HH:mm"),
                    p.ShiftId,
                    p.ShiftId is int sid && shiftById.TryGetValue(sid, out var sh) ? sh.Name : null))
                .ToList();

            return Results.Ok(new BoardAxesDto(
                id,
                input.Schedule.Name,
                Math.Max(1, input.Schedule.WeeksInCycle),
                days,
                periods,
                input.Shifts.OrderBy(s => s.ShiftNo)
                    .Select(s => new BoardShiftDto(s.Id, s.ShiftNo, s.Name)).ToList(),
                input.Classes.Where(c => !c.IsDeleted)
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new BoardClassDto(c.Id, c.Name, c.ShiftId, c.StudentCount)).ToList(),
                input.Teachers.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
                    .Select(t => new BoardTeacherDto(t.Id, t.FullName, t.ColorCode)).ToList(),
                input.Subjects.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new BoardSubjectDto(s.Id, s.Name, s.ShortName, s.ColorCode)).ToList()));
        });

        // Kartochkalar — sinf / o'qituvchi / xona bo'yicha filtr bilan.
        group.MapGet("/cards", async (
            int? scheduleId,
            int? classId,
            int? teacherId,
            string? room,
            ICardBoardService board,
            IUnitOfWork uow,
            ISchedulingStore store,
            CancellationToken ct) =>
        {
            var id = await ActiveScheduleResolver.ResolveIdAsync(uow, scheduleId, ct);
            var cards = await board.GetCardsAsync(id, ct);

            IEnumerable<CardView> filtered = cards;

            if (classId is > 0)
                filtered = filtered.Where(c => c.SchoolClassIds.Contains(classId.Value));

            if (teacherId is > 0)
                filtered = filtered.Where(c => c.TeacherIds.Contains(teacherId.Value));

            if (!string.IsNullOrWhiteSpace(room))
            {
                var needle = room.Trim();
                filtered = filtered.Where(c =>
                    !string.IsNullOrWhiteSpace(c.RoomNumber) &&
                    c.RoomNumber!.Trim().Equals(needle, StringComparison.OrdinalIgnoreCase));
            }

            // A/B nishoni faqat ko'p haftali siklda ma'noga ega.
            var weeksInCycle = Math.Max(1, (await store.LoadAsync(id, ct)).Schedule.WeeksInCycle);

            return Results.Ok(filtered
                .OrderBy(c => c.DayNo).ThenBy(c => c.PeriodNo)
                .Select(c => c.ToDto(weeksInCycle))
                .ToList());
        });

        // Joylashtirilmagan darslar: reja (PeriodsPerWeek) − fakt (SUM(Card.Length)).
        group.MapGet("/unplaced", async (int? scheduleId, ICardBoardService board, CancellationToken ct) =>
        {
            var items = await board.GetUnplacedAsync(scheduleId, ct);
            return Results.Ok(items.Select(BoardMapper.ToDto).ToList());
        });

        // Application darajasidagi tekshiruv (jumladan GROUP_DIVISION_OVERLAP).
        group.MapGet("/validate", async (
            int? scheduleId, IScheduleGenerationService generation, CancellationToken ct) =>
        {
            var conflicts = await generation.ValidateAsync(scheduleId, ct);
            return Results.Ok(new
            {
                isValid = conflicts.All(c => c.Severity != ConflictSeverity.Error),
                conflicts = conflicts.Select(Mapper.ToDto).ToList(),
            });
        });

        // -----------------------------------------------------------------
        // Yozish (kalitsiz — 401, tekshiruv UseApiKeyAuthorization da)
        // -----------------------------------------------------------------

        // Ommaviy ko'chirish: bittasi rad etilsa HECH BIRI yozilmaydi.
        group.MapPost("/place", async (
            CardPlaceRequest? body, ICardBoardService board, CancellationToken ct) =>
        {
            var placements = body?.Placements;
            if (placements is null || placements.Count == 0)
            {
                return Results.Json(
                    new { error = "Ko'chiriladigan kartochka ko'rsatilmagan." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await board.PlaceManyAsync(
                placements.Select(p => new CardPlacement(p.CardId, p.DayNo, p.PeriodId, p.WeeksMask)).ToList(),
                body!.Force,
                body.ScheduleId,
                ct);

            // To'qnashuv — xato emas, natija: sahifa sabablarni ro'yxat qilib ko'rsatadi.
            return Results.Ok(result.ToDto());
        });

        // Qulf BAZAGA yoziladi (ilgari faqat xotirada edi va dastur yopilganda yo'qolardi).
        group.MapPost("/lock", async (
            CardLockRequest? body, ICardBoardService board, CancellationToken ct) =>
        {
            if (body is null || body.CardId <= 0)
            {
                return Results.Json(
                    new { error = "Kartochka ko'rsatilmagan." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var ok = await board.SetLockAsync(body.CardId, body.IsLocked, ct);
            return ok
                ? Results.Ok(new { cardId = body.CardId, isLocked = body.IsLocked })
                : Results.NotFound(new { error = "Kartochka topilmadi." });
        });

        // -----------------------------------------------------------------
        // Generatsiya (uzoq davom etadi — fon rejimida)
        // -----------------------------------------------------------------

        group.MapPost("/generate", (BoardGenerationRequest? body, GenerationJobs jobs) =>
        {
            if (jobs.HasRunning)
            {
                return Results.Json(
                    new { error = "Generatsiya allaqachon ishlamoqda. Avval uni kuting yoki bekor qiling." },
                    statusCode: StatusCodes.Status409Conflict);
            }

            if (!TryReadOptions(body, out var options, out var error))
                return Results.Json(new { error }, statusCode: StatusCodes.Status400BadRequest);

            var job = jobs.Start(options);
            return Results.Accepted($"/api/board/generate/{job.Id}", job.ToDto());
        });

        group.MapGet("/generate/{jobId}", (string jobId, GenerationJobs jobs) =>
        {
            var job = jobs.Find(jobId);
            return job is null
                ? Results.NotFound(new { error = "Bunday generatsiya topilmadi." })
                : Results.Ok(job.ToDto());
        });

        group.MapDelete("/generate/{jobId}", (string jobId, GenerationJobs jobs) =>
            jobs.Cancel(jobId)
                ? Results.Ok(new { jobId, cancelled = true })
                : Results.NotFound(new { error = "Bekor qilinadigan generatsiya topilmadi." }));

        // -----------------------------------------------------------------
        // Chop etish
        // -----------------------------------------------------------------

        group.MapGet("/designs", () => Results.Ok(BuiltInPrintDesigns.Keys
            .Select(k =>
            {
                var design = BuiltInPrintDesigns.Get(k);
                return new PrintDesignDto(
                    k,
                    string.IsNullOrWhiteSpace(design.Name) ? k : design.Name,
                    design.Scope.ToString());
            })
            .ToList()));

        // PDF / HTML. Qamrov MAJBURIY: qamrovsiz 400 (E-01 qoidasi buzilmaydi).
        group.MapGet("/print", async (
            HttpRequest request,
            ICardBoardService board,
            ISchedulingStore store,
            IUnitOfWork uow,
            CancellationToken ct) =>
        {
            var errors = new List<string>();
            var classId = ReadInt(request, "classId", errors) ?? ReadInt(request, "classGroupId", errors);
            var teacherId = ReadInt(request, "teacherId", errors);
            var scheduleId = ReadInt(request, "scheduleId", errors);
            var landscape = ReadBool(request, "landscape", errors);
            var includeTeacher = ReadBool(request, "includeTeacher", errors);
            var includeRoom = ReadBool(request, "includeRoom", errors);

            if (errors.Count > 0)
                return Results.Json(new { error = string.Join(" ", errors) }, statusCode: StatusCodes.Status400BadRequest);

            if (classId is > 0 && teacherId is > 0)
            {
                return Results.Json(
                    new { error = "Bir vaqtda ham sinf, ham o'qituvchi tanlab bo'lmaydi." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var scope = request.Query["scope"].ToString();
            var wantsSchool = scope.Equals("school", StringComparison.OrdinalIgnoreCase);

            if (!wantsSchool && classId is not > 0 && teacherId is not > 0)
            {
                return Results.Json(
                    new { error = "Qamrov ko'rsatilmagan: classId, teacherId yoki scope=school bering." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Dizayn kaliti tekshiriladi: noma'lum kalitda 500 emas, tushunarli 400.
            var design = request.Query["design"].ToString().Trim();
            if (design.Length > 0 &&
                !BuiltInPrintDesigns.Keys.Contains(design, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Json(
                    new { error = $"Noma'lum dizayn: «{design}». Mavjudlari: {string.Join(", ", BuiltInPrintDesigns.Keys)}." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var format = request.Query["format"].ToString();
            var wantsHtml = format.Equals("html", StringComparison.OrdinalIgnoreCase);

            var schoolName = request.Query["schoolName"].ToString();

            var options = new PdfExportOptions
            {
                SchoolName = string.IsNullOrWhiteSpace(schoolName) ? null : schoolName.Trim(),
                Landscape = landscape ?? true,
                IncludeTeacherName = includeTeacher ?? true,
                IncludeRoom = includeRoom ?? true,
                ScheduleId = scheduleId is > 0 ? scheduleId : null,
            };

            var designOptions = new DesignExportOptions
            {
                AcademicYear = NullIfEmpty(request.Query["academicYear"].ToString()),
                Term = NullIfEmpty(request.Query["term"].ToString()),
                ClassDesignKey = design.Length > 0 && !wantsSchool && teacherId is not > 0
                    ? design
                    : BuiltInPrintDesigns.ClassBlue,
                TeacherDesignKey = design.Length > 0 && teacherId is > 0
                    ? design
                    : BuiltInPrintDesigns.TeacherGreen,
                SchoolDesignKey = design.Length > 0 && wantsSchool
                    ? design
                    : BuiltInPrintDesigns.SchoolCompact,
            };

            // Yangi (Card) konstruktor: juft dars, A/B hafta, guruh va xona HAQIQIY manbadan.
            var exporter = new DesignBasedTimetablePdfExporter(board, store, uow, designOptions);

            try
            {
                var document = wantsHtml
                    ? classId is > 0
                        ? await exporter.ExportClassScheduleHtmlAsync(classId.Value, options, ct)
                        : teacherId is > 0
                            ? await exporter.ExportTeacherScheduleHtmlAsync(teacherId.Value, options, ct)
                            : await exporter.ExportSchoolScheduleHtmlAsync(options, ct)
                    : classId is > 0
                        ? await exporter.ExportClassScheduleAsync(classId.Value, options, ct)
                        : teacherId is > 0
                            ? await exporter.ExportTeacherScheduleAsync(teacherId.Value, options, ct)
                            : await exporter.ExportSchoolScheduleAsync(options, ct);

                return Results.File(
                    document.Content,
                    wantsHtml ? "text/html; charset=utf-8" : "application/pdf",
                    document.FileName);
            }
            catch (ArgumentException ex)
            {
                // Mavjud bo'lmagan sinf/o'qituvchi — 500 emas, 400.
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }

    // ---------------------------------------------------------------------

    /// <summary>So'rov modelini generatsiya sozlamalariga o'giradi.</summary>
    private static bool TryReadOptions(
        BoardGenerationRequest? body,
        out ScheduleGenerationOptions options,
        out string? error)
    {
        error = null;
        var complexity = Complexity.Normal;

        if (!string.IsNullOrWhiteSpace(body?.Complexity) &&
            !Enum.TryParse(body!.Complexity, ignoreCase: true, out complexity))
        {
            options = new ScheduleGenerationOptions();
            error = $"Noma'lum murakkablik: «{body.Complexity}». " +
                    $"Mavjudlari: {string.Join(", ", Enum.GetNames<Complexity>())}.";
            return false;
        }

        if (body?.TimeLimitSeconds is int seconds && seconds <= 0)
        {
            options = new ScheduleGenerationOptions();
            error = "«timeLimitSeconds» musbat son bo'lishi kerak.";
            return false;
        }

        options = new ScheduleGenerationOptions
        {
            ScheduleId = body?.ScheduleId is > 0 ? body!.ScheduleId : null,
            Seed = body?.Seed ?? 12345,
            Complexity = complexity,
            TimeLimit = body?.TimeLimitSeconds is int s ? TimeSpan.FromSeconds(s) : null,
            SavePartial = body?.SavePartial ?? true,
            AllowRelaxation = body?.AllowRelaxation ?? true,
            KeepLocked = body?.KeepLocked ?? true,
        };

        return true;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>So'rovdan butun son o'qiydi; xato bo'lsa 400 uchun xabar qo'shadi.</summary>
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
