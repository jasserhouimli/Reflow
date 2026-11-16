namespace Reflow.Modules.DataProcessing.Artifacts;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string Provider { get; set; } = "Local";
    public string? SupabaseUrl { get; set; }
    public string? SupabaseKey { get; set; }
    public string Bucket { get; set; } = "artifacts";
}
