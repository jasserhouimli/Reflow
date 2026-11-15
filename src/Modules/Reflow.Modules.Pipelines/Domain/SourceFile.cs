namespace Reflow.Modules.Pipelines.Domain;

// Uploaded source file (CSV/JSON) stored for file.read nodes.
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
