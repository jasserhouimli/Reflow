namespace Reflow.Infrastructure.Pipelines;

// Contract for verifying pipeline ownership across module boundaries.
public interface IPipelineAccessChecker
{
    Task<bool> OwnsAsync(Guid pipelineId, Guid ownerId, CancellationToken ct);
}
