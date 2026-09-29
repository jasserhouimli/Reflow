using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Realtime;

/// <summary>
/// Live run updates. Clients join per-run groups (ownership-checked) and
/// receive `runChanged` pushes; they refetch state over REST. Small,
/// version-proof payloads instead of full entity streams.
/// </summary>
[Authorize]
public class RunHub(PipelineExecutionDbContext db) : Hub
{
    public static string GroupFor(Guid runId) => $"run:{runId:N}";

    public async Task JoinRun(Guid runId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            throw new HubException("Unauthorized");
        var owns = await db.PipelineRuns.AnyAsync(r => r.Id == runId && r.CreatedBy == userId);
        if (!owns)
            throw new HubException("Run not found");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(runId));
    }

    public Task LeaveRun(Guid runId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(runId));
}

/// <summary>Push helper so slices can notify without referencing SignalR types.</summary>
public interface IRunNotifier
{
    Task RunChangedAsync(Guid runId, CancellationToken ct = default);
}

public class SignalRRunNotifier(IHubContext<RunHub> hub) : IRunNotifier
{
    public Task RunChangedAsync(Guid runId, CancellationToken ct = default) =>
        hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("runChanged", runId.ToString(), ct);
}
