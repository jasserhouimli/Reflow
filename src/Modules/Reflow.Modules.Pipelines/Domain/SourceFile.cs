namespace Reflow.Modules.Pipelines.Domain;

/// <summary>
/// A user-uploaded CSV/JSON source. Content is stored inline because pipelines
/// already embed small tabular text (csv.read); this keeps the data durable
/// across container restarts without a blob dependency.
/// </summary>
public class SourceFile
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
