using System.Collections.Concurrent;

namespace Reflow.Modules.DataProcessing.Lineage;

public sealed class InMemoryLineageStore : ILineageStore
{
    private readonly ConcurrentBag<LineageEdge> _edges = new();

    public Task AddAsync(LineageEdge edge, CancellationToken ct = default)
    {
        if (edge.FromDatasetId == Guid.Empty || edge.ToDatasetId == Guid.Empty)
            throw new InvalidOperationException("Lineage edge endpoints are required");
        if (edge.FromDatasetId == edge.ToDatasetId)
            throw new InvalidOperationException("Lineage edge cannot be a self-loop");
        _edges.Add(edge);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LineageEdge>> UpstreamOfAsync(Guid datasetId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LineageEdge>>(
            _edges.Where(e => e.ToDatasetId == datasetId).ToList());

    public Task<IReadOnlyList<LineageEdge>> DownstreamOfAsync(Guid datasetId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<LineageEdge>>(
            _edges.Where(e => e.FromDatasetId == datasetId).ToList());
}
