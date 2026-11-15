using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Auth;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Realtime;

// Streams runChanged notifications over SignalR to clients in run-scoped groups.
[Authorize]
public class RunHub(PipelineExecutionDbContext db) : Hub
{
    public static string GroupFor(Guid runId) => $"run:{runId:N}";

    public async Task JoinRun(Guid runId)
    {
        if (!Context.User.TryGetUserId(out var userId))
            throw new HubException("Unauthorized");
        var owns = await db.PipelineRuns.AnyAsync(r => r.Id == runId && r.CreatedBy == userId);
        if (!owns)
            throw new HubException("Run not found");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(runId));
    }

    public Task LeaveRun(Guid runId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(runId));
}

// Push abstraction so execution handlers can notify without direct SignalR coupling.
public interface IRunNotifier
{
    Task RunChangedAsync(Guid runId, CancellationToken ct = default);
}

public class SignalRRunNotifier(IHubContext<RunHub> hub) : IRunNotifier
{
    public Task RunChangedAsync(Guid runId, CancellationToken ct = default) =>
        hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("runChanged", runId.ToString(), ct);
}
