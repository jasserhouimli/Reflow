using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.NodeTypes.FileRead;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Files;

/// <summary>
/// Implements the node-side port. Node handlers are singletons, so this adapter
/// opens its own scope per read instead of capturing a scoped DbContext.
/// </summary>
public sealed class FileSourceReader(IServiceScopeFactory scopes) : IFileSourceReader
{
    public async Task<SourceFileContent?> FindAsync(Guid fileId, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PipelinesDbContext>();

        return await db.SourceFiles
            .AsNoTracking()
            .Where(f => f.Id == fileId)
            .Select(f => new SourceFileContent(f.FileName, f.ContentType, f.Content))
            .FirstOrDefaultAsync(ct);
    }
}
