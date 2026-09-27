namespace Reflow.Modules.Triggers;

/// <summary>
/// Implemented by the Pipelines module (it owns pipelines). Triggers only
/// depends on this abstraction — never on Pipelines tables (RULES §2).
/// </summary>
public interface IPipelineAccessChecker
{
    Task<bool> OwnsAsync(Guid pipelineId, Guid ownerId, CancellationToken ct);
}
