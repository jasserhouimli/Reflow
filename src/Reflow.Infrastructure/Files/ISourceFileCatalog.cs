namespace Reflow.Infrastructure.Files;

/// <summary>
/// Read-only view of a user's uploaded sources. Lets the assistant reference a
/// real file id without depending on the module that owns file storage.
/// </summary>
public sealed record SourceFileRef(Guid Id, string FileName);

public interface ISourceFileCatalog
{
    Task<IReadOnlyList<SourceFileRef>> ListAsync(Guid ownerId, CancellationToken ct = default);
}
