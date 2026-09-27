using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.PipelineExecution.Features.StartRun;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Features.StartRun;

public static class StartRunEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/pipelines/{id:guid}/runs", async (
            Guid id,
            PipelineRunStarter starter,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await starter.StartRunAsync(id, ownerId, "manual", ct);
            return result.IsSuccess
                ? Results.Json(new { id = result.Value }, statusCode: result.StatusCode)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("StartRun");

        app.MapGet("/api/v1/pipelines/{id:guid}/runs", async (
            Guid id,
            PipelineExecutionDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var runs = await db.PipelineRuns.AsNoTracking()
                .Where(r => r.PipelineId == id && r.CreatedBy == ownerId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id, r.VersionNumber, status = (int)r.Status,
                    r.TriggerKind, r.CreatedAt, r.CompletedAt, r.Error,
                })
                .ToListAsync(ct);
            return Results.Ok(runs);
        }).RequireAuthorization().WithName("ListRuns");
    }
}
