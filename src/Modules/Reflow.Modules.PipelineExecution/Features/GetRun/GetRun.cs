using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.PipelineExecution.Persistence;

namespace Reflow.Modules.PipelineExecution.Features.GetRun;

public static class GetRunEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/runs/{id:guid}", async (
            Guid id,
            PipelineExecutionDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var run = await db.PipelineRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.CreatedBy == ownerId, ct);
            if (run is null)
                return Results.Json(new { error = "Run not found" }, statusCode: 404);
            return Results.Ok(new
            {
                run.Id,
                run.PipelineId,
                run.VersionNumber,
                status = (int)run.Status,
                run.TriggerKind,
                run.Error,
                run.CreatedAt,
                run.StartedAt,
                run.CompletedAt,
            });
        }).RequireAuthorization().WithName("GetRun");
    }
}
