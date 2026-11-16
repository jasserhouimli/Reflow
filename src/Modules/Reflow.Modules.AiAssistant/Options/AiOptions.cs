namespace Reflow.Modules.AiAssistant.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    // Toggle for AI drafting features.
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public string Model { get; set; } = "llama-3.3-70b-versatile";

    // Set via environment variable or user-secrets (e.g. Ai:ApiKey).
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    // Retry transient HTTP 503 provider capacity errors.
    public int MaxTransientRetries { get; set; } = 1;

    public int RetryDelayMilliseconds { get; set; } = 1000;

    // Maximum self-correction repair attempts when draft validation fails.
    public int MaxRepairs { get; set; } = 2;

    public int MaxOutputTokens { get; set; } = 1200;

    public int MaxPromptChars { get; set; } = 2000;

    public int MaxDraftNodes { get; set; } = 20;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
