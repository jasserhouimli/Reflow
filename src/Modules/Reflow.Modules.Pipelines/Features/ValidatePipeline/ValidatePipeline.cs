using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;
using Reflow.Modules.Pipelines.Validation;

namespace Reflow.Modules.Pipelines.Features.ValidatePipeline;

public static class ValidatePipelineEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/pipelines/{id:guid}/validate", async (
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

            var errors = PipelineGraphValidator.Validate(
                pipeline.Nodes.Select(n => new GraphNode(n.NodeId, n.NodeType, n.ConfigJson)).ToList(),
                pipeline.Edges.Select(e => new GraphEdge(e.SourceNodeId, e.TargetNodeId)).ToList());
            return Results.Ok(new { valid = errors.Count == 0, errors });
        }).RequireAuthorization().WithName("ValidatePipeline");
    }
}
