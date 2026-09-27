using System.Security.Cryptography;
using System.Text;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.Artifacts;

/// <summary>Local-disk artifact store. Paths are sanitized; sizes capped.</summary>
public sealed class LocalArtifactStore : IDataArtifactStore
{
    public const long MaxArtifactBytes = 10 * 1024 * 1024;

    private readonly string _root;
    private readonly IDataWriter _writer;

    public LocalArtifactStore(IDataWriter writer, string? root = null)
    {
        _writer = writer;
        _root = root ?? Path.Combine(AppContext.BaseDirectory, "artifacts");
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
        var safeNode = MakeSafe(nodeId);
        var dir = Path.Combine(_root, runId.ToString("N"));
        Directory.CreateDirectory(dir);

        string actualName, content, contentType;
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            actualName = string.IsNullOrWhiteSpace(fileName)
                ? safeNode + ".csv" : EnsureExtension(MakeSafe(fileName), ".csv");
            content = _writer.ToCsv(frame, delimiter, includeHeader);
            contentType = "text/csv";
        }
        else
        {
            actualName = string.IsNullOrWhiteSpace(fileName)
                ? safeNode + ".json" : EnsureExtension(MakeSafe(fileName), ".json");
            content = _writer.ToJson(frame);
            contentType = "application/json";
        }
        _ = contentType;

        var bytes = Encoding.UTF8.GetBytes(content);
        if (bytes.Length > MaxArtifactBytes)
            throw new InvalidOperationException($"Output too large ({bytes.Length} bytes, max {MaxArtifactBytes})");

        await File.WriteAllTextAsync(Path.Combine(dir, actualName), content, Encoding.UTF8, ct);
        var defaultName = safeNode + (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? ".csv" : ".json");
        if (!actualName.Equals(defaultName, StringComparison.OrdinalIgnoreCase))
            await File.WriteAllTextAsync(Path.Combine(dir, safeNode + ".ref"), actualName, ct);

        return new ArtifactInfo(actualName, bytes.Length, frame.RowCount,
            Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public async Task<(string Content, string ContentType, string FileName)?> LoadAsync(
        Guid runId,
        string nodeId,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(_root, runId.ToString("N"));
        if (!Directory.Exists(dir))
            return null;

        var safeNode = MakeSafe(nodeId);
        var refPath = Path.Combine(dir, safeNode + ".ref");
        if (File.Exists(refPath))
        {
            var referenced = (await File.ReadAllTextAsync(refPath, ct)).Trim();
            var refFull = Path.Combine(dir, Path.GetFileName(referenced));
            if (File.Exists(refFull) && IsDataFile(refFull))
                return await ReadDataFile(refFull, ct);
        }

        var match = Directory.GetFiles(dir)
            .Select(p => new FileInfo(p))
            .Where(f => IsDataFile(f.FullName)
                && f.Name.StartsWith(safeNode + ".", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return match is null ? null : await ReadDataFile(match.FullName, ct);
    }

    private static bool IsDataFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(string Content, string ContentType, string FileName)?> ReadDataFile(
        string path, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(path, ct);
        var name = Path.GetFileName(path);
        return (content,
            Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "application/json",
            name);
    }

    internal static string MakeSafe(string nodeId)
    {
        var sb = new StringBuilder();
        foreach (var ch in nodeId)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
                sb.Append(ch);
            else
                sb.Append('_');
        }
        var safe = sb.ToString().Trim('_', '.');
        return string.IsNullOrEmpty(safe) ? "node" : safe[..Math.Min(safe.Length, 80)];
    }

    private static string EnsureExtension(string name, string ext) =>
        name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;
}
