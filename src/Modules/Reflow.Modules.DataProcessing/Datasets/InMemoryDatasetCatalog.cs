using System.Collections.Concurrent;

namespace Reflow.Modules.DataProcessing.Datasets;

public sealed class InMemoryDatasetCatalog : IDatasetCatalog
{
    private readonly ConcurrentDictionary<Guid, DatasetMetadata> _store = new();

    public Task<DatasetMetadata> RegisterAsync(DatasetMetadata dataset, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dataset.Name))
            throw new InvalidOperationException("Dataset name is required");
        _store[dataset.Id] = dataset;
        return Task.FromResult(dataset);
    }

    public Task<DatasetMetadata?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var found) ? found : null);

    public Task<IReadOnlyList<DatasetMetadata>> ListAsync(
        DatasetLayer? layer = null, CancellationToken ct = default)
    {
        var all = _store.Values
            .Where(d => layer is null || d.Layer == layer)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<DatasetMetadata>>(all);
    }
}
