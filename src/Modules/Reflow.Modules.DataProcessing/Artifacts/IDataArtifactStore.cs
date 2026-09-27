using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.Artifacts;

public sealed record ArtifactInfo(
    string FileName,
    long SizeBytes,
    int RowCount,
    string ChecksumSha256);

/// <summary>
/// Content-addressed run artifacts (JSON/CSV now, Parquet later).
/// Local disk first; blob storage implements this later unchanged.
/// </summary>
public interface IDataArtifactStore
{
    Task<ArtifactInfo> SaveAsync(
        Guid runId,
        string nodeId,
        Frame frame,
        string format,
        string? fileName = null,
        char delimiter = ',',
        bool includeHeader = true,
        CancellationToken ct = default);

    Task<(string Content, string ContentType, string FileName)?> LoadAsync(
        Guid runId,
        string nodeId,
        CancellationToken ct = default);
}
