using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Infrastructure.Auth;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Realtime;

// Streams real-time run snapshots and updates over SignalR to clients in run-scoped groups.
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

        var run = await db.PipelineRuns.AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => new
            {
                id = r.Id,
                pipelineId = r.PipelineId,
                versionNumber = r.VersionNumber,
                status = (int)r.Status,
                triggerKind = r.TriggerKind,
                error = r.Error,
                createdAt = r.CreatedAt,
                startedAt = r.StartedAt,
                completedAt = r.CompletedAt,
            })
            .FirstOrDefaultAsync();

        if (run is null) return;

        var tasks = await db.TaskRuns.AsNoTracking()
            .Where(t => t.RunId == runId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new
            {
                id = t.Id,
                nodeId = t.NodeId,
                nodeType = t.NodeType,
                status = (int)t.Status,
                attemptCount = t.AttemptCount,
                error = t.Error,
                startedAt = t.StartedAt,
                completedAt = t.CompletedAt,
            })
            .ToListAsync();

        var logs = await db.ExecutionLogs.AsNoTracking()
            .Where(l => l.RunId == runId)
            .OrderBy(l => l.Timestamp)
            .Select(l => new
            {
                taskRunId = l.TaskRunId,
                level = l.Level,
                message = l.Message,
                timestamp = l.Timestamp,
            })
            .ToListAsync();

        await Clients.Caller.SendAsync("runSnapshot", new
        {
            runId = runId.ToString(),
            run,
            tasks,
            logs,
        });
    }

    public Task LeaveRun(Guid runId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(runId));
}

// Push abstraction so execution handlers can notify without direct SignalR coupling.
public interface IRunNotifier
{
    Task RunChangedAsync(Guid runId, CancellationToken ct = default);
}

public class SignalRRunNotifier(IHubContext<RunHub> hub, IServiceProvider services) : IRunNotifier
{
    public async Task RunChangedAsync(Guid runId, CancellationToken ct = default)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PipelineExecutionDbContext>();

            var run = await db.PipelineRuns.AsNoTracking()
                .Where(r => r.Id == runId)
                .Select(r => new
                {
                    id = r.Id,
                    pipelineId = r.PipelineId,
                    versionNumber = r.VersionNumber,
                    status = (int)r.Status,
                    triggerKind = r.TriggerKind,
                    error = r.Error,
                    createdAt = r.CreatedAt,
                    startedAt = r.StartedAt,
                    completedAt = r.CompletedAt,
                })
                .FirstOrDefaultAsync(ct);

            if (run is null) return;

            var tasks = await db.TaskRuns.AsNoTracking()
                .Where(t => t.RunId == runId)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new
                {
                    id = t.Id,
                    nodeId = t.NodeId,
                    nodeType = t.NodeType,
                    status = (int)t.Status,
                    attemptCount = t.AttemptCount,
                    error = t.Error,
                    startedAt = t.StartedAt,
                    completedAt = t.CompletedAt,
                })
                .ToListAsync(ct);

            var logs = await db.ExecutionLogs.AsNoTracking()
                .Where(l => l.RunId == runId)
                .OrderBy(l => l.Timestamp)
                .Select(l => new
                {
                    taskRunId = l.TaskRunId,
                    level = l.Level,
                    message = l.Message,
                    timestamp = l.Timestamp,
                })
                .ToListAsync(ct);

            var snapshot = new
            {
                runId = runId.ToString(),
                run,
                tasks,
                logs,
            };

            await hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("runSnapshot", snapshot, ct);
            await hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("runChanged", runId.ToString(), ct);
        }
        catch
        {
            await hub.Clients.Group(RunHub.GroupFor(runId)).SendAsync("runChanged", runId.ToString(), ct);
        }
    }
}
