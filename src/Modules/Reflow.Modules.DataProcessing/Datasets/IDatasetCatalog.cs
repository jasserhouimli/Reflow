namespace Reflow.Modules.DataProcessing.Datasets;

/// <summary>
/// Dataset metadata registry. In-memory now; EF persistence arrives with the
/// Pipelines control-plane design (needs producing-run references).
/// </summary>
public interface IDatasetCatalog
{
    Task<DatasetMetadata> RegisterAsync(DatasetMetadata dataset, CancellationToken ct = default);
    Task<DatasetMetadata?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DatasetMetadata>> ListAsync(
        DatasetLayer? layer = null, CancellationToken ct = default);
}
