using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Pipelines.Domain;
using Reflow.Modules.Pipelines.Persistence;
using Reflow.Modules.Pipelines.Validation;

namespace Reflow.Modules.Pipelines.Features.PublishPipeline;

public class PublishPipelineHandler(PipelinesDbContext db)
{
    public async Task<Result<object>> Handle(Guid id, Guid ownerId, CancellationToken ct)
    {
        var pipeline = await db.Pipelines
            .Include(p => p.Nodes).Include(p => p.Edges)
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, ct);
        if (pipeline is null)
            return Result<object>.Failure("Pipeline not found", 404);
        if (pipeline.Status == PipelineStatus.Archived)
            return Result<object>.Failure("Archived pipelines cannot be published", 409);

        var errors = PipelineGraphValidator.Validate(
            pipeline.Nodes.Select(n => new GraphNode(n.NodeId, n.NodeType, n.ConfigJson)).ToList(),
            pipeline.Edges.Select(e => new GraphEdge(e.SourceNodeId, e.TargetNodeId)).ToList());
        if (errors.Count > 0)
            return Result<object>.Failure("Pipeline is invalid: " + string.Join("; ", errors), 400);

        var next = await db.PipelineVersions
            .Where(v => v.PipelineId == pipeline.Id)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        next++;

        var definition = JsonSerializer.Serialize(new
        {
            name = pipeline.Name,
            nodes = pipeline.Nodes.Select(n => new
            {
                nodeId = n.NodeId, nodeType = n.NodeType,
                configJson = n.ConfigJson, label = n.Label,
            }),
            edges = pipeline.Edges.Select(e => new
            {
                sourceNodeId = e.SourceNodeId, targetNodeId = e.TargetNodeId,
            }),
        });

        db.PipelineVersions.Add(new PipelineVersion
        {
            Id = Guid.NewGuid(),
            PipelineId = pipeline.Id,
            VersionNumber = next,
            DefinitionJson = definition,
            PublishedBy = ownerId,
            PublishedAt = DateTime.UtcNow,
        });
        pipeline.Status = PipelineStatus.Published;
        pipeline.CurrentVersion = next;
        pipeline.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { version = next }, 200);
    }
}

public static class PublishPipelineEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/pipelines/{id:guid}/publish", async (
            Guid id,
            PublishPipelineHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.Handle(id, ownerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("PublishPipeline");
    }
}
