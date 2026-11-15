namespace Reflow.Infrastructure.Files;

// Read-only metadata view of user source files for cross-module callers.
public sealed record SourceFileRef(Guid Id, string FileName);

public interface ISourceFileCatalog
{
    Task<IReadOnlyList<SourceFileRef>> ListAsync(Guid ownerId, CancellationToken ct = default);
}
