namespace Reflow.Modules.NodeTypes.FileRead;

/// <summary>
/// Port owned by the file.read slice. The module that persists uploaded
/// sources implements it; the node never knows how storage works.
/// </summary>
public interface IFileSourceReader
{
    Task<SourceFileContent?> FindAsync(Guid fileId, CancellationToken ct = default);
}

public sealed record SourceFileContent(string FileName, string ContentType, string Content);
