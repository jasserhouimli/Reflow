using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Files;

public class DeleteFileHandler(PipelinesDbContext db)
{
    public async Task<Result<object>> Handle(Guid id, Guid ownerId, CancellationToken ct)
    {
        var deleted = await db.SourceFiles
            .Where(f => f.Id == id && f.OwnerId == ownerId)
            .ExecuteDeleteAsync(ct);

        return deleted == 0
            ? Result<object>.Failure("File not found", 404)
            : Result<object>.Success(new { id }, 204);
    }
}

public static class DeleteFileEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapDelete("/api/v1/files/{id:guid}", async (
            Guid id,
            DeleteFileHandler handler,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.Handle(id, ownerId, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("DeleteFile").Produces(204).Produces(404);
    }
}
