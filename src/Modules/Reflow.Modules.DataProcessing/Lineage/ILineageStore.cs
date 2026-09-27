namespace Reflow.Modules.DataProcessing.Lineage;

/// <summary>Upstream → downstream dataset edge (spec §14).</summary>
public sealed record LineageEdge(
    Guid FromDatasetId,
    Guid ToDatasetId,
    Guid? RunId,
    string? Transform);

public interface ILineageStore
{
    Task AddAsync(LineageEdge edge, CancellationToken ct = default);
    Task<IReadOnlyList<LineageEdge>> UpstreamOfAsync(Guid datasetId, CancellationToken ct = default);
    Task<IReadOnlyList<LineageEdge>> DownstreamOfAsync(Guid datasetId, CancellationToken ct = default);
}
