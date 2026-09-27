using Reflow.Infrastructure.Results;

namespace Reflow.Modules.PipelineExecution.Snapshots;

public sealed record SnapshotNode(string NodeId, string NodeType, string ConfigJson);
public sealed record SnapshotEdge(string SourceNodeId, string TargetNodeId);

/// <summary>Immutable published pipeline definition for one run.</summary>
public sealed record PipelineSnapshot(
    Guid PipelineId,
    Guid VersionId,
    int VersionNumber,
    IReadOnlyList<SnapshotNode> Nodes,
    IReadOnlyList<SnapshotEdge> Edges);

/// <summary>
/// Implemented by the Pipelines module (it owns versions). Execution only
/// depends on this abstraction — never on Pipelines tables (RULES §2).
/// </summary>
public interface IPipelineSnapshotProvider
{
    Task<Result<PipelineSnapshot>> GetPublishedSnapshotAsync(
        Guid pipelineId, Guid ownerId, CancellationToken ct);
}
