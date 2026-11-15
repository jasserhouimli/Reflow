using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;
using Reflow.Infrastructure.Pipelines;

namespace Reflow.Modules.Pipelines.Features.Access;

// Checks pipeline ownership for cross-module callers.
public class PipelineAccessChecker(PipelinesDbContext db) : IPipelineAccessChecker
{
    public async Task<bool> OwnsAsync(Guid pipelineId, Guid ownerId, CancellationToken ct) =>
        await db.Pipelines.AnyAsync(p => p.Id == pipelineId && p.OwnerId == ownerId, ct);
}
