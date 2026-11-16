using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reflow.Infrastructure.Results;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.Pipelines.Domain;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines.Features.Files;

public record UploadFileRequest(string FileName, string Content);

public record UploadFileResponse(
    Guid Id, string FileName, string ContentType, long SizeBytes, string Sha256);

public class UploadFileHandler(
    PipelinesDbContext db,
    IHttpClientFactory clientFactory,
    IOptions<StorageOptions> storageOptions,
    ILogger<UploadFileHandler> logger)
{
    public const int MaxContentChars = 10 * 1024 * 1024;
    public const int MaxFileNameChars = 260;

    private static readonly Dictionary<string, string> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".csv"] = "text/csv",
            [".json"] = "application/json",
        };

    public async Task<Result<UploadFileResponse>> Handle(
        UploadFileRequest request, Guid ownerId, CancellationToken ct)
    {
        var fileName = request.FileName?.Trim() ?? string.Empty;
        if (fileName.Length == 0 || fileName.Length > MaxFileNameChars)
            return Result<UploadFileResponse>.Failure(
                $"File name must be 1-{MaxFileNameChars} characters", 400);

        // Store only the leaf name; never trust a path from the client.
        fileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var contentType))
            return Result<UploadFileResponse>.Failure("Only .csv and .json files are supported", 400);

        var content = request.Content ?? string.Empty;
        if (content.Length == 0)
            return Result<UploadFileResponse>.Failure("File content is required", 400);
        if (content.Length > MaxContentChars)
            return Result<UploadFileResponse>.Failure(
                $"File is too large (max {MaxContentChars / (1024 * 1024)}MB)", 400);

        var bytes = Encoding.UTF8.GetBytes(content);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes));

        var existing = await db.SourceFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.OwnerId == ownerId
                && f.Sha256 == sha256
                && f.FileName == fileName, ct);
        if (existing is not null)
            return Result<UploadFileResponse>.Success(ToResponse(existing), 200);

        var file = new SourceFile
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = bytes.Length,
            Sha256 = sha256,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };
        db.SourceFiles.Add(file);
        await db.SaveChangesAsync(ct);

        // Sync uploaded source file to Supabase Storage if configured
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
                    var objectPath = $"sources/{file.Id:N}/{fileName}";
                    var uploadUrl = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";

                    using var req = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                    req.Headers.Add("apikey", opts.SupabaseKey);
                    if (opts.SupabaseKey.StartsWith("ey", StringComparison.OrdinalIgnoreCase))
                    {
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.SupabaseKey);
                    }
                    req.Headers.Add("x-upsert", "true");
                    req.Content = new ByteArrayContent(bytes);
                    req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                    var res = await client.SendAsync(req);
                    if (res.IsSuccessStatusCode)
                    {
                        logger.LogInformation("Successfully synced source file {FileName} to Supabase bucket '{Bucket}' at '{ObjectPath}'", fileName, bucket, objectPath);
                    }
                    else
                    {
                        var err = await res.Content.ReadAsStringAsync();
                        logger.LogWarning("Failed to sync source file to Supabase: {Status} - {Body}", res.StatusCode, err);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Exception syncing source file {FileName} to Supabase", fileName);
                }
            });
        }

        return Result<UploadFileResponse>.Success(ToResponse(file), 201);
    }

    private static UploadFileResponse ToResponse(SourceFile file) =>
        new(file.Id, file.FileName, file.ContentType, file.SizeBytes, file.Sha256);
}

public static class UploadFileEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/files", async (
            UploadFileHandler handler,
            UploadFileRequest request,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!PipelineAuth.TryOwnerId(http.User, out var ownerId))
                return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
            var result = await handler.Handle(request, ownerId, ct);
            return result.IsSuccess
                ? Results.Json(result.Value, statusCode: result.StatusCode)
                : Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        }).RequireAuthorization().WithName("UploadFile").Produces(201).Produces(400);
    }
}
