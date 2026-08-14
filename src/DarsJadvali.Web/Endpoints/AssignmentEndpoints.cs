using DarsJadvali.Application.Services;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Endpoints;

/// <summary>Biriktirmalar (o'qituvchi + fan + sinf + haftalik soat).</summary>
public static class AssignmentEndpoints
{
    public static void MapAssignmentEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/assignments");

        group.MapGet("/", async (int? teacherId, int? classGroupId, IAssignmentService svc, CancellationToken ct) =>
        {
            var items = teacherId is > 0
                ? await svc.GetByTeacherAsync(teacherId.Value, ct)
                : classGroupId is > 0
                    ? await svc.GetByClassGroupAsync(classGroupId.Value, ct)
                    : await svc.GetAllAsync(ct);

            return Results.Ok(items.Select(x => x.ToDto()));
        });

        group.MapGet("/{id:int}", async (int id, IAssignmentService svc, CancellationToken ct) =>
        {
            var item = (await svc.GetAllAsync(ct)).FirstOrDefault(x => x.Id == id);
            return item is null
                ? Results.NotFound(new { error = "Biriktirma topilmadi." })
                : Results.Ok(item.ToDto());
        });

        group.MapGet("/{id:int}/hours", async (int id, IAssignmentService svc, CancellationToken ct) =>
        {
            var (weekly, placed, remaining) = await svc.GetHoursSummaryAsync(id, ct);
            return Results.Ok(new HoursSummaryDto(weekly, placed, remaining));
        });

        group.MapPost("/", async (AssignmentDto dto, IAssignmentService svc, CancellationToken ct) =>
        {
            if (dto.TeacherId <= 0 || dto.SubjectId <= 0 || dto.ClassGroupId <= 0)
                return Results.BadRequest(new { error = "O'qituvchi, fan va sinf tanlanishi shart." });
            if (dto.WeeklyHoursCount <= 0)
                return Results.BadRequest(new { error = "Haftalik soat 0 dan katta bo'lishi kerak." });

            var entity = dto.ToEntity();
            entity.Id = 0;
            var created = await svc.CreateAsync(entity, ct);

            // Navigatsiyalar to'ldirilgan holda qaytarish uchun ro'yxatdan qayta o'qiymiz.
            var full = (await svc.GetAllAsync(ct)).FirstOrDefault(x => x.Id == created.Id);
            return Results.Ok((full ?? created).ToDto());
        });

        group.MapPut("/{id:int}", async (int id, AssignmentDto dto, IAssignmentService svc, CancellationToken ct) =>
        {
            var existing = (await svc.GetAllAsync(ct)).FirstOrDefault(x => x.Id == id);
            if (existing is null)
                return Results.NotFound(new { error = "Biriktirma topilmadi." });

            var entity = dto.ToEntity();
            entity.Id = id;
            await svc.UpdateAsync(entity, ct);

            var updated = (await svc.GetAllAsync(ct)).FirstOrDefault(x => x.Id == id);
            return Results.Ok(updated?.ToDto());
        });

        group.MapDelete("/{id:int}", async (int id, IAssignmentService svc, CancellationToken ct) =>
        {
            var existing = (await svc.GetAllAsync(ct)).FirstOrDefault(x => x.Id == id);
            if (existing is null)
                return Results.NotFound(new { error = "Biriktirma topilmadi." });

            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
