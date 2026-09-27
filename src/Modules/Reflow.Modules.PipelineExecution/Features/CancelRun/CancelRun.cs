using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.PipelineExecution.Domain;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Features.CancelRun;

public class CancelRunHandler(PipelineExecutionDbContext db)
{
    public async Task<Result<object>> Handle(Guid id, Guid ownerId, CancellationToken ct)
    {
        var run = await db.PipelineRuns
            .FirstOrDefaultAsync(r => r.Id == id && r.CreatedBy == ownerId, ct);
        if (run is null)
            return Result<object>.Failure("Run not found", 404);
        if (run.Status is PipelineRunStatus.Completed or PipelineRunStatus.Failed or PipelineRunStatus.Cancelled)
            return Result<object>.Failure($"Cannot cancel a {run.Status} run", 400);

        run.Status = PipelineRunStatus.Cancelled;
        run.CompletedAt = DateTime.UtcNow;
        var queued = await db.TaskRuns
            .Where(t => t.RunId == id
                && (t.Status == TaskRunStatus.Pending
                    || t.Status == TaskRunStatus.Ready
                    || t.Status == TaskRunStatus.RetryScheduled))
            .ToListAsync(ct);
        foreach (var task in queued)
        {
            task.Status = TaskRunStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
        }
        db.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Level = "Info",
            Message = "Run cancelled by user",
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { run.Id }, 200);
    }
}

public static class CancelRunEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/runs/{id:guid}/cancel", async (
            Guid id,
            CancelRunHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.Handle(id, ownerId, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("CancelRun");
    }
}
