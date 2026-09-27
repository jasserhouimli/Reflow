using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Features.RunTasks;

public static class RunTasksEndpoints
{
    private static bool TryOwner(HttpContext http, out Guid ownerId) =>
        Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out ownerId);

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
    }
}
