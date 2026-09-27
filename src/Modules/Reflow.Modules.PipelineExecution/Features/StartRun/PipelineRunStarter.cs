using System.Text.Json;
using Reflow.Infrastructure.Results;
using Reflow.Modules.PipelineExecution.Domain;
using Reflow.Modules.PipelineExecution.Persistence;
using Reflow.Modules.PipelineExecution.Snapshots;

namespace Reflow.Modules.PipelineExecution.Features.StartRun;

public class PipelineRunStarter(
    PipelineExecutionDbContext db,
    IPipelineSnapshotProvider snapshots)
{
    public async Task<Result<Guid>> StartRunAsync(
        Guid pipelineId, Guid ownerId, string triggerKind, CancellationToken ct)
    {
        var snapshot = await snapshots.GetPublishedSnapshotAsync(pipelineId, ownerId, ct);
        if (!snapshot.IsSuccess)
            return Result<Guid>.Failure(snapshot.Error!, snapshot.StatusCode);

        var now = DateTime.UtcNow;
        var run = new PipelineRun
        {
            Id = Guid.NewGuid(),
            PipelineId = pipelineId,
            PipelineVersionId = snapshot.Value!.VersionId,
            VersionNumber = snapshot.Value.VersionNumber,
            Status = PipelineRunStatus.Running,
            CreatedBy = ownerId,
            TriggerKind = triggerKind,
            EdgesJson = JsonSerializer.Serialize(snapshot.Value.Edges),
            CreatedAt = now,
            StartedAt = now,
        };

        var targets = new HashSet<string>(
            snapshot.Value.Edges.Select(e => e.TargetNodeId), StringComparer.Ordinal);
        foreach (var node in snapshot.Value.Nodes)
        {
            db.TaskRuns.Add(new TaskRun
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                NodeId = node.NodeId,
                NodeType = node.NodeType,
                Status = targets.Contains(node.NodeId)
                    ? TaskRunStatus.Pending
                    : TaskRunStatus.Ready,
                ConfigJson = node.ConfigJson,
                CreatedAt = now,
            });
        }

        db.PipelineRuns.Add(run);
        db.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Level = "Info",
            Message = $"Run started (version {run.VersionNumber}, trigger {triggerKind})",
            Timestamp = now,
        });
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(run.Id, 202);
    }
}
