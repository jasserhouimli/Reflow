using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Auth;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Features.RunTasks;

public static class RunTasksEndpoints
{
    private static bool TryOwner(HttpContext http, out Guid ownerId) =>
        http.User.TryGetUserId(out ownerId);

    private static async Task<bool> OwnsRun(
        PipelineExecutionDbContext db, Guid runId, Guid ownerId, CancellationToken ct) =>
        await db.PipelineRuns.AnyAsync(r => r.Id == runId && r.CreatedBy == ownerId, ct);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/runs/{id:guid}/tasks", async (
            Guid id,
            PipelineExecutionDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            if (!await OwnsRun(db, id, ownerId, ct))
                return Results.Json(new { error = "Run not found" }, statusCode: 404);
            var tasks = await db.TaskRuns.AsNoTracking()
                .Where(t => t.RunId == id)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id, t.NodeId, t.NodeType, status = (int)t.Status,
                    t.AttemptCount, t.Error, t.StartedAt, t.CompletedAt,
                })
                .ToListAsync(ct);
            return Results.Ok(tasks);
        }).RequireAuthorization().WithName("ListRunTasks");

        app.MapGet("/api/v1/runs/{id:guid}/logs", async (
            Guid id,
            PipelineExecutionDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            if (!await OwnsRun(db, id, ownerId, ct))
                return Results.Json(new { error = "Run not found" }, statusCode: 404);
            var logs = await db.ExecutionLogs.AsNoTracking()
                .Where(l => l.RunId == id)
                .OrderBy(l => l.Timestamp)
                .Select(l => new { l.TaskRunId, l.Level, l.Message, l.Timestamp })
                .ToListAsync(ct);
            return Results.Ok(logs);
        }).RequireAuthorization().WithName("ListRunLogs");

        app.MapGet("/api/v1/tasks/{id:guid}", async (
            Guid id,
            PipelineExecutionDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var task = await db.TaskRuns.AsNoTracking()
                .Join(db.PipelineRuns, t => t.RunId, r => r.Id,
                    (t, r) => new { Task = t, r.CreatedBy })
                .FirstOrDefaultAsync(x => x.Task.Id == id && x.CreatedBy == ownerId, ct);
            if (task is null)
                return Results.Json(new { error = "Task not found" }, statusCode: 404);
            var t = task.Task;
            return Results.Ok(new
            {
                t.Id, t.RunId, t.NodeId, t.NodeType, status = (int)t.Status,
                t.ConfigJson, t.OutputJson, t.AttemptCount, t.Error,
                t.StartedAt, t.CompletedAt,
            });
        }).RequireAuthorization().WithName("GetTaskRun");

        app.MapGet("/api/v1/tasks/{id:guid}/attempts", async (
            Guid id,
            PipelineExecutionDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var owned = await db.TaskRuns.AsNoTracking()
                .Join(db.PipelineRuns, t => t.RunId, r => r.Id,
                    (t, r) => new { t.Id, r.CreatedBy })
                .AnyAsync(x => x.Id == id && x.CreatedBy == ownerId, ct);
            if (!owned)
                return Results.Json(new { error = "Task not found" }, statusCode: 404);
            var attempts = await db.TaskAttempts.AsNoTracking()
                .Where(a => a.TaskRunId == id)
                .OrderBy(a => a.AttemptNumber)
                .Select(a => new
                {
                    a.AttemptNumber, status = (int)a.Status, a.Error, a.StartedAt, a.CompletedAt,
                })
                .ToListAsync(ct);
            return Results.Ok(attempts);
        }).RequireAuthorization().WithName("ListTaskAttempts");

        app.MapGet("/api/v1/runs/{id:guid}/artifacts/{nodeId}/status", async (
            Guid id,
            string nodeId,
            PipelineExecutionDbContext db,
            IDataArtifactStore artifacts,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            if (!await OwnsRun(db, id, ownerId, ct))
                return Results.Json(new { error = "Run not found" }, statusCode: 404);

            var status = await artifacts.GetStatusAsync(id, nodeId, ct);
            return Results.Ok(new { status });
        }).RequireAuthorization().WithName("GetArtifactStatus");

        app.MapGet("/api/v1/runs/{id:guid}/artifacts/{nodeId}", async (
            Guid id,
            string nodeId,
            PipelineExecutionDbContext db,
            IDataArtifactStore artifacts,
            IDataWriter writer,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryOwner(http, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            if (!await OwnsRun(db, id, ownerId, ct))
                return Results.Json(new { error = "Run not found" }, statusCode: 404);

            var file = await artifacts.LoadAsync(id, nodeId, ct);
            if (file is not null)
            {
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(file.Value.Content),
                    file.Value.ContentType, file.Value.FileName);
            }

            // Fallback: If artifact file is missing from disk/storage, generate from database task output
            var task = await db.TaskRuns.AsNoTracking()
                .FirstOrDefaultAsync(t => t.RunId == id && t.NodeId == nodeId, ct);
            if (task is null || string.IsNullOrWhiteSpace(task.OutputJson))
                return Results.Json(new { error = "Artifact not found" }, statusCode: 404);

            var format = "csv";
            var fileName = $"{nodeId}.csv";
            if (!string.IsNullOrWhiteSpace(task.ConfigJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(task.ConfigJson);
                    if (doc.RootElement.TryGetProperty("format", out var f) && f.GetString() == "json")
                    {
                        format = "json";
                        fileName = $"{nodeId}.json";
                    }
                    if (doc.RootElement.TryGetProperty("fileName", out var fn) && !string.IsNullOrWhiteSpace(fn.GetString()))
                    {
                        fileName = fn.GetString()!;
                    }
                }
                catch { }
            }

            if (format == "json")
            {
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(task.OutputJson),
                    "application/json", fileName);
            }

            var frame = JsonCodec.FromJson(task.OutputJson, null);
            var csv = writer.ToCsv(frame, ',', true);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv", fileName);
        }).RequireAuthorization().WithName("DownloadArtifact");
    }
}
