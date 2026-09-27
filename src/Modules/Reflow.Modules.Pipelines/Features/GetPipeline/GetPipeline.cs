using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.GetPipeline;

public static class GetPipelineEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/pipelines/{id:guid}", async (
            Guid id,
            PipelinesDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var pipeline = await db.Pipelines.AsNoTracking()
                .Include(p => p.Nodes).Include(p => p.Edges)
                .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, ct);
            if (pipeline is null)
                return Results.Json(new { error = "Pipeline not found" }, statusCode: 404);
            return Results.Ok(new
            {
                pipeline.Id,
                pipeline.Name,
                status = (int)pipeline.Status,
                pipeline.CurrentVersion,
                nodes = pipeline.Nodes.Select(n => new
                {
                    n.NodeId, n.NodeType, n.ConfigJson, n.Label, n.PositionX, n.PositionY,
                }),
                edges = pipeline.Edges.Select(e => new { e.SourceNodeId, e.TargetNodeId }),
            });
        }).RequireAuthorization().WithName("GetPipeline");
    }
}
