using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.PipelineExecution.Snapshots;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Snapshots;

/// <summary>
/// Bridges Pipelines → Execution. Lives here (not in Execution) because only
/// this module may read version tables (RULES §2).
/// </summary>
public class PipelineSnapshotProvider(PipelinesDbContext db) : IPipelineSnapshotProvider
{
    public async Task<Result<PipelineSnapshot>> GetPublishedSnapshotAsync(
        Guid pipelineId, Guid ownerId, CancellationToken ct)
    {
        var pipeline = await db.Pipelines.AsNoTracking()
            .Include(p => p.Nodes).Include(p => p.Edges)
            .FirstOrDefaultAsync(p => p.Id == pipelineId && p.OwnerId == ownerId, ct);
        if (pipeline is null)
            return Result<PipelineSnapshot>.Failure("Pipeline not found", 404);

        var version = await db.PipelineVersions.AsNoTracking()
            .Where(v => v.PipelineId == pipelineId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version is null)
            return Result<PipelineSnapshot>.Failure("Pipeline has no published version", 400);

        var definition = JsonDocument.Parse(version.DefinitionJson).RootElement;
        var nodes = definition.GetProperty("nodes").EnumerateArray()
            .Select(n => new SnapshotNode(
                n.GetProperty("nodeId").GetString()!,
                n.GetProperty("nodeType").GetString()!,
                n.TryGetProperty("configJson", out var c) ? c.GetString() ?? "{}" : "{}"))
            .ToList();
        var edges = definition.TryGetProperty("edges", out var e)
            ? e.EnumerateArray()
                .Select(x => new SnapshotEdge(
                    x.GetProperty("sourceNodeId").GetString()!,
                    x.GetProperty("targetNodeId").GetString()!))
                .ToList()
            : new List<SnapshotEdge>();

        return Result<PipelineSnapshot>.Success(new PipelineSnapshot(
            pipelineId, version.Id, version.VersionNumber, nodes, edges));
    }
}
