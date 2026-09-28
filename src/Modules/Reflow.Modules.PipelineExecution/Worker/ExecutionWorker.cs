using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.Abstractions;
using Reflow.Modules.NodeTypes.Registry;
using Reflow.Modules.PipelineExecution.Domain;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Worker;

/// <summary>Pure dependency/readiness helpers. Unit-tested without a database.</summary>
public static class ExecutionGraph
{
    public static TimeSpan RetryDelay(int attemptNumber) =>
        TimeSpan.FromSeconds(5 * attemptNumber);

    public static bool IsReady(
        string nodeId,
        IReadOnlyList<(string Source, string Target)> edges,
        Func<string, TaskRunStatus> statusOf)
    {
        foreach (var edge in edges)
        {
            if (string.Equals(edge.Target, nodeId, StringComparison.Ordinal)
                && statusOf(edge.Source) != TaskRunStatus.Completed)
                return false;
        }
        return true;
    }
}

public class ExecutionWorker(
    IServiceProvider services,
    ILogger<ExecutionWorker> logger) : BackgroundService
{
    public const int MaxAutoAttempts = 3;
    public const int MaxTotalAttempts = 5;
    private static readonly TimeSpan TaskTimeout = TimeSpan.FromSeconds(120);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Execution batch failed");
            }
        }
    }

    private async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PipelineExecutionDbContext>();
        var stuck = await db.TaskRuns
            .Where(t => t.Status == TaskRunStatus.Running)
            .ToListAsync(ct);
        foreach (var task in stuck)
        {
            task.Status = TaskRunStatus.Ready;
            task.Error = "Worker interrupted; requeued";
        }
        var openAttempts = await db.TaskAttempts
            .Where(a => a.Status == TaskRunStatus.Running)
            .ToListAsync(ct);
        foreach (var attempt in openAttempts)
        {
            attempt.Status = TaskRunStatus.Failed;
            attempt.Error = "Worker interrupted";
            attempt.CompletedAt = DateTime.UtcNow;
        }
        if (stuck.Count > 0 || openAttempts.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PipelineExecutionDbContext>();
        var now = DateTime.UtcNow;

        var ids = await db.TaskRuns.AsNoTracking()
            .Where(t => t.Status == TaskRunStatus.Ready
                || (t.Status == TaskRunStatus.RetryScheduled && t.NotBefore <= now))
            .OrderBy(t => t.CreatedAt)
            .Take(5)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested)
                return;
            var claimed = await db.Database.ExecuteSqlRawAsync(
                """UPDATE "pipeline_execution"."TaskRuns" SET "Status"=2, "StartedAt"=NOW() WHERE "Id"={0} AND ("Status"=1 OR ("Status"=5 AND "NotBefore"<=NOW()))""",
                new object[] { id }, ct);
            if (claimed == 0)
                continue;
            await ExecuteTaskAsync(id, ct);
        }
    }

    private async Task ExecuteTaskAsync(Guid taskId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PipelineExecutionDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<NodeTypeRegistry>();
        var writer = scope.ServiceProvider.GetRequiredService<IDataWriter>();

        var task = await db.TaskRuns.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null)
            return;
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.Id == task.RunId, ct);
        if (run is null)
            return;

        if (run.Status == PipelineRunStatus.Cancelled)
        {
            task.Status = TaskRunStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        task.AttemptCount++;
        var attempt = new TaskAttempt
        {
            Id = Guid.NewGuid(),
            TaskRunId = task.Id,
            AttemptNumber = task.AttemptCount,
            Status = TaskRunStatus.Running,
            StartedAt = DateTime.UtcNow,
        };
        db.TaskAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TaskTimeout);

        try
        {
            if (!registry.TryGet(task.NodeType, out var handler) || handler is null)
                throw new PermanentFailureException($"Unknown node type '{task.NodeType}'");

            using var config = JsonDocument.Parse(task.ConfigJson);
            var configErrors = handler.ValidateConfig(config.RootElement);
            if (configErrors.Count > 0)
                throw new PermanentFailureException(
                    "Invalid config: " + string.Join("; ", configErrors));

            var inputs = await LoadInputsAsync(db, run, task, ct);
            Frame output;
            if (handler is ITriggerPayloadHandler payloadHandler)
                output = await payloadHandler.ExecuteWithPayload(
                    config.RootElement, run.TriggerPayloadJson, timeout.Token);
            else
                output = await handler.ExecuteAsync(inputs, config.RootElement, timeout.Token);

            task.OutputJson = writer.ToJson(output);
            task.Status = TaskRunStatus.Completed;
            task.Error = null;
            task.CompletedAt = DateTime.UtcNow;
            attempt.Status = TaskRunStatus.Completed;
            attempt.CompletedAt = DateTime.UtcNow;
            AddLog(db, run.Id, task.Id, "Info",
                $"Node '{task.NodeId}' completed ({output.RowCount} rows)");

            await db.SaveChangesAsync(ct);
            await UnblockDownstreamAsync(task.RunId, ct);
            await FinalizeRunAsync(task.RunId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var timedOut = ex is OperationCanceledException;
            var permanent = ex is PermanentFailureException
                || ex.Message.StartsWith("Binder", StringComparison.Ordinal)
                || ex.Message.StartsWith("Parser", StringComparison.Ordinal)
                || ex.Message.StartsWith("Catalog Error", StringComparison.Ordinal)
                || ex.Message.StartsWith("Invalid trigger payload", StringComparison.Ordinal);
            var message = timedOut ? "Task timed out" : Trim(ex.Message);

            if (!permanent && task.AttemptCount < MaxAutoAttempts)
            {
                task.Status = TaskRunStatus.RetryScheduled;
                task.NotBefore = DateTime.UtcNow + ExecutionGraph.RetryDelay(task.AttemptCount);
                task.Error = message;
                attempt.Status = TaskRunStatus.RetryScheduled;
                attempt.Error = message;
                attempt.CompletedAt = DateTime.UtcNow;
                AddLog(db, run.Id, task.Id, "Warning",
                    $"Node '{task.NodeId}' failed (attempt {task.AttemptCount}), retry scheduled: {message}");
                await db.SaveChangesAsync(ct);
                return;
            }

            task.Status = TaskRunStatus.Failed;
            task.Error = message;
            task.CompletedAt = DateTime.UtcNow;
            attempt.Status = TaskRunStatus.Failed;
            attempt.Error = message;
            attempt.CompletedAt = DateTime.UtcNow;
            AddLog(db, run.Id, task.Id, "Error",
                $"Node '{task.NodeId}' failed: {message}");
            await db.SaveChangesAsync(ct);
            await FailRunAsync(task.RunId, message, ct);
        }
    }

    private static async Task<IReadOnlyList<NodeInput>> LoadInputsAsync(
        PipelineExecutionDbContext db, PipelineRun run, TaskRun task, CancellationToken ct)
    {
        var edges = JsonSerializer.Deserialize<List<EdgeDto>>(run.EdgesJson) ?? new();
        var sources = edges
            .Where(e => string.Equals(e.TargetNodeId, task.NodeId, StringComparison.Ordinal)
                && e.SourceNodeId is not null)
            .Select(e => e.SourceNodeId!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        if (sources.Count == 0)
            return Array.Empty<NodeInput>();

        var frames = new List<NodeInput>();
        foreach (var source in sources)
        {
            var upstream = await db.TaskRuns.AsNoTracking().FirstOrDefaultAsync(
                t => t.RunId == run.Id && t.NodeId == source, ct);
            if (upstream?.OutputJson is not null)
                frames.Add(new NodeInput(source, JsonCodec.FromJson(upstream.OutputJson, null)));
        }
        return frames;
    }

    private async Task UnblockDownstreamAsync(Guid runId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PipelineExecutionDbContext>();
        var run = await db.PipelineRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
            return;
        var edges = JsonSerializer.Deserialize<List<EdgeDto>>(run.EdgesJson) ?? new();
        var pairs = edges.Select(e => (e.SourceNodeId, e.TargetNodeId)).ToList();
        var tasks = await db.TaskRuns.Where(t => t.RunId == runId).ToListAsync(ct);
        var byNode = tasks.ToDictionary(t => t.NodeId, StringComparer.Ordinal);

        foreach (var task in tasks.Where(t => t.Status == TaskRunStatus.Pending))
        {
            if (ExecutionGraph.IsReady(task.NodeId, pairs, n =>
                byNode.TryGetValue(n, out var t) ? t.Status : TaskRunStatus.Failed))
            {
                task.Status = TaskRunStatus.Ready;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task FinalizeRunAsync(Guid runId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PipelineExecutionDbContext>();
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || run.Status != PipelineRunStatus.Running)
            return;
        var tasks = await db.TaskRuns.AsNoTracking().Where(t => t.RunId == runId).ToListAsync(ct);
        if (tasks.Count == 0 || tasks.Any(t => !t.Status.IsTerminal()))
            return;
        run.Status = tasks.Any(t => t.Status == TaskRunStatus.Failed)
            ? PipelineRunStatus.Failed
            : PipelineRunStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        AddLog(db, run.Id, null, "Info", $"Run {run.Status}");
        await db.SaveChangesAsync(ct);
    }

    private async Task FailRunAsync(Guid runId, string error, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PipelineExecutionDbContext>();
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null || run.Status != PipelineRunStatus.Running)
            return;
        run.Status = PipelineRunStatus.Failed;
        run.Error = Trim(error);
        run.CompletedAt = DateTime.UtcNow;
        var skipped = await db.TaskRuns
            .Where(t => t.RunId == runId
                && (t.Status == TaskRunStatus.Pending
                    || t.Status == TaskRunStatus.Ready
                    || t.Status == TaskRunStatus.RetryScheduled))
            .ToListAsync(ct);
        foreach (var task in skipped)
        {
            task.Status = TaskRunStatus.Skipped;
            task.CompletedAt = DateTime.UtcNow;
        }
        AddLog(db, run.Id, null, "Error", $"Run failed: {Trim(error)}");
        await db.SaveChangesAsync(ct);
    }

    private static void AddLog(
        PipelineExecutionDbContext db, Guid runId, Guid? taskRunId,
        string level, string message)
    {
        db.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            TaskRunId = taskRunId,
            Level = level,
            Message = message.Length > 2000 ? message[..2000] : message,
            Timestamp = DateTime.UtcNow,
        });
    }

    private static string Trim(string message) =>
        message.Length > 500 ? message[..500] : message;

    private sealed record EdgeDto(string SourceNodeId, string TargetNodeId);
}

/// <summary>Terminal failure: config errors, unknown types. Never retried.</summary>
public sealed class PermanentFailureException(string message) : InvalidOperationException(message);
