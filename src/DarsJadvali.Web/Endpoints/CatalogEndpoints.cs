using DarsJadvali.Application.Services;
using DarsJadvali.Web.Dtos;

namespace DarsJadvali.Web.Endpoints;

/// <summary>O'qituvchilar, fanlar va sinflar uchun oddiy CRUD endpoint'lari.</summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        MapTeachers(api);
        MapSubjects(api);
        MapClassGroups(api);
    }

    private static void MapTeachers(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/teachers");

        group.MapGet("/", async (ITeacherService svc, CancellationToken ct) =>
            Results.Ok((await svc.GetAllAsync(ct)).Select(x => x.ToDto())));

        group.MapGet("/{id:int}", async (int id, ITeacherService svc, CancellationToken ct) =>
        {
            var item = await svc.GetByIdAsync(id, ct);
            return item is null
                ? Results.NotFound(new { error = "O'qituvchi topilmadi." })
                : Results.Ok(item.ToDto());
        });

        group.MapPost("/", async (TeacherDto dto, ITeacherService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.FullName))
                return Results.BadRequest(new { error = "O'qituvchi F.I.O. bo'sh bo'lmasligi kerak." });

            var entity = dto.ToEntity();
            entity.Id = 0;
            var created = await svc.CreateAsync(entity, ct);
            return Results.Ok(created.ToDto());
        });

        group.MapPut("/{id:int}", async (int id, TeacherDto dto, ITeacherService svc, CancellationToken ct) =>
        {
            if (await svc.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "O'qituvchi topilmadi." });

            var entity = dto.ToEntity();
            entity.Id = id;
            await svc.UpdateAsync(entity, ct);
            return Results.Ok((await svc.GetByIdAsync(id, ct))?.ToDto());
        });

        group.MapDelete("/{id:int}", async (int id, ITeacherService svc, CancellationToken ct) =>
        {
            if (await svc.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "O'qituvchi topilmadi." });
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapSubjects(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/subjects");

        group.MapGet("/", async (ISubjectService svc, CancellationToken ct) =>
            Results.Ok((await svc.GetAllAsync(ct)).Select(x => x.ToDto())));

        group.MapGet("/{id:int}", async (int id, ISubjectService svc, CancellationToken ct) =>
        {
            var item = await svc.GetByIdAsync(id, ct);
            return item is null
                ? Results.NotFound(new { error = "Fan topilmadi." })
                : Results.Ok(item.ToDto());
        });

        group.MapPost("/", async (SubjectDto dto, ISubjectService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { error = "Fan nomi bo'sh bo'lmasligi kerak." });

            var entity = dto.ToEntity();
            entity.Id = 0;
            var created = await svc.CreateAsync(entity, ct);
            return Results.Ok(created.ToDto());
        });

        group.MapPut("/{id:int}", async (int id, SubjectDto dto, ISubjectService svc, CancellationToken ct) =>
        {
            if (await svc.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Fan topilmadi." });

            var entity = dto.ToEntity();
            entity.Id = id;
            await svc.UpdateAsync(entity, ct);
            return Results.Ok((await svc.GetByIdAsync(id, ct))?.ToDto());
        });

        group.MapDelete("/{id:int}", async (int id, ISubjectService svc, CancellationToken ct) =>
        {
            if (await svc.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Fan topilmadi." });
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapClassGroups(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/classgroups");

        group.MapGet("/", async (IClassGroupService svc, CancellationToken ct) =>
            Results.Ok((await svc.GetAllAsync(ct)).Select(x => x.ToDto())));

        group.MapGet("/{id:int}", async (int id, IClassGroupService svc, CancellationToken ct) =>
        {
            var item = await svc.GetByIdAsync(id, ct);
            return item is null
                ? Results.NotFound(new { error = "Sinf topilmadi." })
                : Results.Ok(item.ToDto());
        });

        group.MapPost("/", async (ClassGroupDto dto, IClassGroupService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { error = "Sinf nomi bo'sh bo'lmasligi kerak." });

            var entity = dto.ToEntity();
            entity.Id = 0;
            var created = await svc.CreateAsync(entity, ct);
            return Results.Ok(created.ToDto());
        });

        group.MapPut("/{id:int}", async (int id, ClassGroupDto dto, IClassGroupService svc, CancellationToken ct) =>
        {
            if (await svc.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Sinf topilmadi." });

            var entity = dto.ToEntity();
            entity.Id = id;
            await svc.UpdateAsync(entity, ct);
            return Results.Ok((await svc.GetByIdAsync(id, ct))?.ToDto());
        });

        group.MapDelete("/{id:int}", async (int id, IClassGroupService svc, CancellationToken ct) =>
        {
            if (await svc.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Sinf topilmadi." });
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
