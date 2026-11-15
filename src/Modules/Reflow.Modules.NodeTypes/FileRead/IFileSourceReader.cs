namespace Reflow.Modules.NodeTypes.FileRead;

// Abstraction for fetching raw uploaded source file content.
public interface IFileSourceReader
{
    Task<SourceFileContent?> FindAsync(Guid fileId, CancellationToken ct = default);
}

public sealed record SourceFileContent(string FileName, string ContentType, string Content);
