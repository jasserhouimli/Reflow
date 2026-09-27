using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Pipelines.Domain;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.ArchivePipeline;

public static class ArchivePipelineEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/pipelines/{id:guid}/archive", async (
            Guid id,
            PipelinesDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var pipeline = await db.Pipelines
                .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, ct);
            if (pipeline is null)
                return Results.Json(new { error = "Pipeline not found" }, statusCode: 404);
            pipeline.Status = PipelineStatus.Archived;
            pipeline.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { pipeline.Id });
        }).RequireAuthorization().WithName("ArchivePipeline");
    }
}
