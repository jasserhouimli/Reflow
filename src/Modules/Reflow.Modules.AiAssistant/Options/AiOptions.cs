namespace Reflow.Modules.AiAssistant.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Kill switch. AI stays off until an operator turns it on.</summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public string Model { get; set; } = "llama-3.3-70b-versatile";

    /// <summary>Never set in appsettings.json: comes from secrets or env vars.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Bounded retries for transient provider capacity errors (HTTP 503). Free
    /// tiers reject bursts with 503 rather than queueing, so one retry turns a
    /// busy provider into a slower success instead of a hard failure.
    /// </summary>
    public int MaxTransientRetries { get; set; } = 1;

    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>How many times an invalid draft is sent back for repair.</summary>
    public int MaxRepairs { get; set; } = 2;

    public int MaxOutputTokens { get; set; } = 1200;

    public int MaxPromptChars { get; set; } = 2000;

    public int MaxDraftNodes { get; set; } = 20;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
