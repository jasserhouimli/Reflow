using System.Text;

namespace Reflow.Modules.DataProcessing.Artifacts;

/// <summary>
/// Helper for artifact path sanitization, extension normalization, and naming conventions.
/// Adheres to Single Responsibility Principle and eliminates duplication across artifact store implementations.
/// </summary>
public static class ArtifactPathHelper
{
    public static string MakeSafe(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "node";

        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
                sb.Append(ch);
            else
                sb.Append('_');
        }
        var safe = sb.ToString().Trim('_', '.');
        return string.IsNullOrEmpty(safe) ? "node" : safe[..Math.Min(safe.Length, 80)];
    }

    public static string EnsureExtension(string name, string ext) =>
        name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;

    public static string GetExtension(string format) =>
        string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? ".csv" : ".json";

    public static string GetContentType(string format) =>
        string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "application/json";

    public static string BuildFileName(string safeNode, string format, string? customFileName = null)
    {
        var ext = GetExtension(format);
        return string.IsNullOrWhiteSpace(customFileName)
            ? safeNode + ext
            : EnsureExtension(MakeSafe(customFileName), ext);
    }
}
