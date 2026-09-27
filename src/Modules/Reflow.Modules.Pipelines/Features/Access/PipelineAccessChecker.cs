using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;
using Reflow.Modules.Triggers;

namespace Reflow.Modules.Pipelines.Features.Access;

/// <summary>Implements Triggers' ownership contract from pipeline tables.</summary>
public class PipelineAccessChecker(PipelinesDbContext db) : IPipelineAccessChecker
{
    public async Task<bool> OwnsAsync(Guid pipelineId, Guid ownerId, CancellationToken ct) =>
        await db.Pipelines.AnyAsync(p => p.Id == pipelineId && p.OwnerId == ownerId, ct);
}
