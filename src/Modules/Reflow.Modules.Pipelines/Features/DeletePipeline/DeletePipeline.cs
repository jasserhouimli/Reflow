using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.DeletePipeline;

public static class DeletePipelineEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/api/v1/pipelines/{id:guid}", async (
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
            db.Pipelines.Remove(pipeline);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization().WithName("DeletePipeline");
    }
}
