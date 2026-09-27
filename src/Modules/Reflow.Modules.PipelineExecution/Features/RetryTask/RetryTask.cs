using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.PipelineExecution.Domain;
using Reflow.Modules.PipelineExecution.Persistence;
using Reflow.Modules.PipelineExecution.Worker;

namespace Reflow.Modules.PipelineExecution.Features.RetryTask;

public class RetryTaskHandler(PipelineExecutionDbContext db)
{
    public async Task<Result<object>> Handle(Guid id, Guid ownerId, CancellationToken ct)
    {
        var task = await db.TaskRuns
            .Join(db.PipelineRuns, t => t.RunId, r => r.Id,
                (t, r) => new { Task = t, Run = r })
            .FirstOrDefaultAsync(x => x.Task.Id == id && x.Run.CreatedBy == ownerId, ct);
        if (task is null)
            return Result<object>.Failure("Task not found", 404);
        if (task.Task.Status != TaskRunStatus.Failed)
            return Result<object>.Failure("Only failed tasks can be retried", 400);

        var attempts = await db.TaskAttempts.CountAsync(a => a.TaskRunId == id, ct);
        if (attempts >= ExecutionWorker.MaxTotalAttempts)
            return Result<object>.Failure($"Attempt limit reached ({ExecutionWorker.MaxTotalAttempts})", 400);

        task.Task.Status = TaskRunStatus.Ready;
        task.Task.Error = null;
        task.Task.NotBefore = null;
        if (task.Run.Status == PipelineRunStatus.Failed)
        {
            task.Run.Status = PipelineRunStatus.Running;
            task.Run.CompletedAt = null;
            var skipped = await db.TaskRuns
                .Where(t => t.RunId == task.Run.Id && t.Status == TaskRunStatus.Skipped)
                .ToListAsync(ct);
            foreach (var s in skipped)
                s.Status = TaskRunStatus.Pending;
        }
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { task.Task.Id }, 202);
    }
}

public static class RetryTaskEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/tasks/{id:guid}/retry", async (
            Guid id,
            RetryTaskHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.Handle(id, ownerId, ct);
            return result.IsSuccess
                ? Results.Json(result.Value, statusCode: result.StatusCode)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("RetryTask");
    }
}
