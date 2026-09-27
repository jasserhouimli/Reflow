using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Pipelines.Domain;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.UpdatePipeline;

public record NodeDto(
    string NodeId, string NodeType, string? ConfigJson, string? Label, double PositionX, double PositionY);
public record EdgeDto(string SourceNodeId, string TargetNodeId);
public record UpdatePipelineRequest(string? Name, List<NodeDto>? Nodes, List<EdgeDto>? Edges);

public class UpdatePipelineHandler(PipelinesDbContext db)
{
    public async Task<Result<object>> Handle(
        Guid id, UpdatePipelineRequest request, Guid ownerId, CancellationToken ct)
    {
        var pipeline = await db.Pipelines
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, ct);
        if (pipeline is null)
            return Result<object>.Failure("Pipeline not found", 404);
        if (pipeline.Status == PipelineStatus.Archived)
            return Result<object>.Failure("Archived pipelines cannot be edited", 409);

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
                return Result<object>.Failure("Name must be 1-200 characters", 400);
            pipeline.Name = request.Name.Trim();
        }

        if (request.Nodes is not null || request.Edges is not null)
        {
            var nodes = request.Nodes ?? new();
            var edges = request.Edges ?? new();
            if (nodes.Count > Validation.PipelineGraphValidator.MaxNodes)
                return Result<object>.Failure($"Too many nodes (max {Validation.PipelineGraphValidator.MaxNodes})", 400);
            foreach (var n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n.NodeId) || string.IsNullOrWhiteSpace(n.NodeType))
                    return Result<object>.Failure("Each node needs nodeId and nodeType", 400);
                if (n.ConfigJson is not null)
                {
                    try { System.Text.Json.JsonDocument.Parse(n.ConfigJson); }
                    catch (System.Text.Json.JsonException)
                    {
                        return Result<object>.Failure($"Node '{n.NodeId}' has invalid config JSON", 400);
                    }
                }
            }

            await db.PipelineNodes
                .Where(n => n.PipelineId == pipeline.Id)
                .ExecuteDeleteAsync(ct);
            await db.PipelineEdges
                .Where(e => e.PipelineId == pipeline.Id)
                .ExecuteDeleteAsync(ct);
            db.PipelineNodes.AddRange(nodes.Select(n => new PipelineNode
            {
                Id = Guid.NewGuid(),
                PipelineId = pipeline.Id,
                NodeId = n.NodeId,
                NodeType = n.NodeType,
                ConfigJson = n.ConfigJson ?? "{}",
                Label = n.Label,
                PositionX = n.PositionX,
                PositionY = n.PositionY,
            }));
            db.PipelineEdges.AddRange(edges.Select(e => new PipelineEdge
            {
                Id = Guid.NewGuid(),
                PipelineId = pipeline.Id,
                SourceNodeId = e.SourceNodeId,
                TargetNodeId = e.TargetNodeId,
            }));
        }

        pipeline.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { pipeline.Id }, 200);
    }
}

public static class UpdatePipelineEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/api/v1/pipelines/{id:guid}", async (
            Guid id,
            UpdatePipelineHandler handler,
            UpdatePipelineRequest request,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.Handle(id, request, ownerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("UpdatePipeline");
    }
}
