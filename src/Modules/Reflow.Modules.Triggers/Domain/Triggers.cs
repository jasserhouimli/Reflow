namespace Reflow.Modules.Triggers.Domain;

public enum TriggerKind
{
    Schedule = 0,
    Webhook = 1,
}

public enum OverlapPolicy
{
    Skip = 0,
    Queue = 1,
}

public class Trigger
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public Guid OwnerId { get; set; }
    public TriggerKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public string? Cron { get; set; }
    public string? Timezone { get; set; }
    public OverlapPolicy Overlap { get; set; } = OverlapPolicy.Skip;
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastFiredAt { get; set; }

    public string? SecretTokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum WebhookProcessingStatus
{
    Received = 0,
    Started = 1,
    Duplicate = 2,
    Rejected = 3,
}

/// <summary>
/// Persisted inbound event. Idempotency key is (TriggerId, ExternalEventId)
/// when the caller supplies one (RULES §6).
/// </summary>
public class WebhookEvent
{
    public Guid Id { get; set; }
    public Guid TriggerId { get; set; }
    public string? ExternalEventId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public WebhookProcessingStatus ProcessingStatus { get; set; }
    public Guid? PipelineRunId { get; set; }
    public DateTime ReceivedAt { get; set; }
}
