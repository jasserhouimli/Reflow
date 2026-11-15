namespace Reflow.Infrastructure.Pipelines;

/// <summary>
/// Cross-module contract for verifying pipeline ownership without
/// directly accessing the Pipelines database tables.
/// </summary>
public interface IPipelineAccessChecker
{
    Task<bool> OwnsAsync(Guid pipelineId, Guid ownerId, CancellationToken ct);
}
