using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.Artifacts;

public sealed class SupabaseArtifactStore : IDataArtifactStore
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IDataWriter _writer;
    private readonly StorageOptions _options;
    private readonly IDataArtifactStore _fallback;
    private readonly ILogger<SupabaseArtifactStore> _logger;

    public SupabaseArtifactStore(
        IHttpClientFactory clientFactory,
        IDataWriter writer,
        IOptions<StorageOptions> options,
        IDataArtifactStore fallback,
        ILogger<SupabaseArtifactStore> logger)
    {
        _clientFactory = clientFactory;
        _writer = writer;
        _options = options.Value;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<ArtifactInfo> SaveAsync(
        Guid runId,
        string nodeId,
        Frame frame,
        string format,
        string? fileName = null,
        char delimiter = ',',
        bool includeHeader = true,
        CancellationToken ct = default)
    {
        var isCsv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        var safeNode = MakeSafe(nodeId);
        var actualName = string.IsNullOrWhiteSpace(fileName)
            ? safeNode + (isCsv ? ".csv" : ".json")
            : (isCsv ? EnsureExtension(MakeSafe(fileName), ".csv") : EnsureExtension(MakeSafe(fileName), ".json"));

        var content = isCsv ? _writer.ToCsv(frame, delimiter, includeHeader) : _writer.ToJson(frame);
        var contentType = isCsv ? "text/csv" : "application/json";
        var bytes = Encoding.UTF8.GetBytes(content);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes));

        // Always save to fallback local store for fast local caching
        try
        {
            await _fallback.SaveAsync(runId, nodeId, frame, format, fileName, delimiter, includeHeader, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save local artifact cache for run {RunId}, node {NodeId}", runId, nodeId);
        }

        // Upload to Supabase Storage REST API
        if (!string.IsNullOrWhiteSpace(_options.SupabaseUrl) && !string.IsNullOrWhiteSpace(_options.SupabaseKey))
        {
            try
            {
                var client = _clientFactory.CreateClient();
                var baseUrl = SanitizeBaseUrl(_options.SupabaseUrl);
                var bucket = GetBucket();
                var objectPath = $"{runId:N}/{actualName}";
                var uploadUrl = $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";

                using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SupabaseKey);
                request.Headers.Add("apikey", _options.SupabaseKey);
                request.Headers.Add("x-upsert", "true");
                request.Content = new ByteArrayContent(bytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Supabase Storage upload failed with {StatusCode}: {Body}", response.StatusCode, errorBody);
                }
                else
                {
                    _logger.LogInformation("Successfully uploaded artifact {ObjectPath} to Supabase bucket '{Bucket}'", objectPath, bucket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload artifact to Supabase Storage for run {RunId}", runId);
            }
        }

        return new ArtifactInfo(actualName, bytes.Length, frame.RowCount, checksum);
    }

    public async Task<(string Content, string ContentType, string FileName)?> LoadAsync(
        Guid runId,
        string nodeId,
        CancellationToken ct = default)
    {
        // Try Supabase Storage first if configured
        if (!string.IsNullOrWhiteSpace(_options.SupabaseUrl) && !string.IsNullOrWhiteSpace(_options.SupabaseKey))
        {
            try
            {
                var client = _clientFactory.CreateClient();
                var baseUrl = SanitizeBaseUrl(_options.SupabaseUrl);
                var bucket = GetBucket();
                var safeNode = MakeSafe(nodeId);

                // Check possible file extensions
                foreach (var ext in new[] { ".csv", ".json" })
                {
                    var actualName = safeNode + ext;
                    var objectPath = $"{runId:N}/{actualName}";

                    // 1. Try authenticated endpoint
                    var authUrl = $"{baseUrl}/storage/v1/object/authenticated/{bucket}/{objectPath}";
                    using var authReq = new HttpRequestMessage(HttpMethod.Get, authUrl);
                    authReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SupabaseKey);
                    authReq.Headers.Add("apikey", _options.SupabaseKey);

                    var authRes = await client.SendAsync(authReq, ct);
                    if (authRes.IsSuccessStatusCode)
                    {
                        var content = await authRes.Content.ReadAsStringAsync(ct);
                        var contentType = ext == ".csv" ? "text/csv" : "application/json";
                        return (content, contentType, actualName);
                    }

                    // 2. Fallback to public endpoint if bucket is public
                    var pubUrl = $"{baseUrl}/storage/v1/object/public/{bucket}/{objectPath}";
                    using var pubReq = new HttpRequestMessage(HttpMethod.Get, pubUrl);
                    var pubRes = await client.SendAsync(pubReq, ct);
                    if (pubRes.IsSuccessStatusCode)
                    {
                        var content = await pubRes.Content.ReadAsStringAsync(ct);
                        var contentType = ext == ".csv" ? "text/csv" : "application/json";
                        return (content, contentType, actualName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load artifact from Supabase Storage for run {RunId}, node {NodeId}", runId, nodeId);
            }
        }

        // Fallback to local store
        return await _fallback.LoadAsync(runId, nodeId, ct);
    }

    private string GetBucket() =>
        string.IsNullOrWhiteSpace(_options.Bucket) ? "artifacts" : _options.Bucket.Trim('/');

    public static string SanitizeBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }
        var s = trimmed.TrimEnd('/');
        if (s.EndsWith("/storage/v1/s3", StringComparison.OrdinalIgnoreCase))
            s = s[..^"/storage/v1/s3".Length];
        else if (s.EndsWith("/storage/v1", StringComparison.OrdinalIgnoreCase))
            s = s[..^"/storage/v1".Length];
        return s.TrimEnd('/');
    }

    private static string MakeSafe(string s) =>
        new string(s.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());

    private static string EnsureExtension(string name, string ext) =>
        name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;
}
