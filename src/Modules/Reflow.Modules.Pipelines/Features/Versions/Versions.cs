using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Versions;

public static class VersionEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/pipelines/{id:guid}/versions", async (
            Guid id,
            PipelinesDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var owned = await db.Pipelines.AnyAsync(p => p.Id == id && p.OwnerId == ownerId, ct);
            if (!owned)
                return Results.Json(new { error = "Pipeline not found" }, statusCode: 404);
            var versions = await db.PipelineVersions.AsNoTracking()
                .Where(v => v.PipelineId == id)
                .OrderBy(v => v.VersionNumber)
                .Select(v => new { v.VersionNumber, v.PublishedAt })
                .ToListAsync(ct);
            return Results.Ok(versions);
        }).RequireAuthorization().WithName("ListVersions");

        app.MapGet("/api/v1/pipelines/{id:guid}/versions/{n:int}", async (
            Guid id,
            int n,
            PipelinesDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var owned = await db.Pipelines.AnyAsync(p => p.Id == id && p.OwnerId == ownerId, ct);
            if (!owned)
                return Results.Json(new { error = "Pipeline not found" }, statusCode: 404);
            var version = await db.PipelineVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.PipelineId == id && v.VersionNumber == n, ct);
            if (version is null)
                return Results.Json(new { error = "Version not found" }, statusCode: 404);
            return Results.Ok(new { version.VersionNumber, definition = version.DefinitionJson });
        }).RequireAuthorization().WithName("GetVersion");
    }
}
