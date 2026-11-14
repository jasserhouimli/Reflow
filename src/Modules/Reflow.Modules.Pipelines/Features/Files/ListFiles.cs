using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Files;

public record SourceFileSummary(
    Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);

public class ListFilesHandler(PipelinesDbContext db)
{
    public const int MaxResults = 200;

    public async Task<IReadOnlyList<SourceFileSummary>> Handle(Guid ownerId, CancellationToken ct) =>
        await db.SourceFiles
            .AsNoTracking()
            .Where(f => f.OwnerId == ownerId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(MaxResults)
            // Metadata only: the content never leaves the server through this endpoint.
            .Select(f => new SourceFileSummary(
                f.Id, f.FileName, f.ContentType, f.SizeBytes, f.CreatedAt))
            .ToListAsync(ct);
}

public static class ListFilesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/files", async (
            ListFilesHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            return Results.Ok(await handler.Handle(ownerId, ct));
        }).RequireAuthorization().WithName("ListFiles");
    }
}
