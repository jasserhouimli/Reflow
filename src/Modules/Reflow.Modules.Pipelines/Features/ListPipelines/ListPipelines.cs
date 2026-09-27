using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.ListPipelines;

public static class ListPipelinesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/pipelines", async (
            PipelinesDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var items = await db.Pipelines.AsNoTracking()
                .Where(p => p.OwnerId == ownerId)
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new { p.Id, p.Name, status = (int)p.Status, p.CurrentVersion, p.UpdatedAt })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization().WithName("ListPipelines");
    }
}
