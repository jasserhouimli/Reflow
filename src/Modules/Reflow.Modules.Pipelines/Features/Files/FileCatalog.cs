using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Files;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Files;

/// <summary>
/// Implements the assistant's read-only catalog port. Only ids and file names
/// cross the module boundary; content never reaches the model.
/// </summary>
public sealed class FileCatalog(PipelinesDbContext db) : ISourceFileCatalog
{
    public const int MaxFiles = 50;

    public async Task<IReadOnlyList<SourceFileRef>> ListAsync(
        Guid ownerId, CancellationToken ct = default) =>
        await db.SourceFiles
            .AsNoTracking()
            .Where(f => f.OwnerId == ownerId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(MaxFiles)
            .Select(f => new SourceFileRef(f.Id, f.FileName))
            .ToListAsync(ct);
}
