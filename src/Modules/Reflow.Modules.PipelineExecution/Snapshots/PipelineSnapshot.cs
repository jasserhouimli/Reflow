using Reflow.Infrastructure.Results;

namespace Reflow.Modules.PipelineExecution.Snapshots;

public sealed record SnapshotNode(string NodeId, string NodeType, string ConfigJson);
public sealed record SnapshotEdge(string SourceNodeId, string TargetNodeId);

// Immutable published pipeline version captured for execution.
public sealed record PipelineSnapshot(
    Guid PipelineId,
    Guid VersionId,
    int VersionNumber,
    IReadOnlyList<SnapshotNode> Nodes,
    IReadOnlyList<SnapshotEdge> Edges);

// Provider contract to fetch pipeline snapshots without direct table access.
public interface IPipelineSnapshotProvider
{
    Task<Result<PipelineSnapshot>> GetPublishedSnapshotAsync(
        Guid pipelineId, Guid ownerId, CancellationToken ct);
}
