using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Files;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Files;

// Returns user-owned source file metadata (id and name only).
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
