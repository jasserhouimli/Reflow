using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reflow.Infrastructure.Results;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Files;

public class DeleteFileHandler(
    PipelinesDbContext db,
    IHttpClientFactory clientFactory,
    IOptions<StorageOptions> storageOptions,
    ILogger<DeleteFileHandler> logger)
{
    public async Task<Result<object>> Handle(Guid id, Guid ownerId, CancellationToken ct)
    {
        var file = await db.SourceFiles
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId, ct);
        if (file is null)
            return Result<object>.Failure("File not found", 404);

        db.SourceFiles.Remove(file);
        await db.SaveChangesAsync(ct);

        // Delete from Supabase Storage if configured
        var opts = storageOptions.Value;
        if (!string.IsNullOrWhiteSpace(opts.SupabaseUrl) && !string.IsNullOrWhiteSpace(opts.SupabaseKey))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var client = clientFactory.CreateClient();
                    var baseUrl = SupabaseArtifactStore.SanitizeBaseUrl(opts.SupabaseUrl);
                    var bucket = string.IsNullOrWhiteSpace(opts.Bucket) ? "reflow" : opts.Bucket.Trim('/');
                    var objectPath = $"sources/{id:N}/{file.FileName}";
                    var deleteUrl = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";

                    using var req = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                    req.Headers.Add("apikey", opts.SupabaseKey);
                    if (opts.SupabaseKey.StartsWith("ey", StringComparison.OrdinalIgnoreCase))
                    {
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.SupabaseKey);
                    }

                    await client.SendAsync(req);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete source file from Supabase: {Id}", id);
                }
            });
        }

        return Result<object>.Success(new { id }, 204);
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
