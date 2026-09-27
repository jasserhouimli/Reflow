namespace Reflow.Modules.PipelineExecution.Domain;

public enum PipelineRunStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
}

public enum TaskRunStatus
{
    Pending = 0,
    Ready = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    RetryScheduled = 5,
    Cancelled = 6,
    Skipped = 7,
}

public static class TaskRunStatusExtensions
{
    public static bool IsTerminal(this TaskRunStatus status) => status is
        TaskRunStatus.Completed or TaskRunStatus.Failed
        or TaskRunStatus.Cancelled or TaskRunStatus.Skipped;
}

public class PipelineRun
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public Guid PipelineVersionId { get; set; }
    public int VersionNumber { get; set; }
    public PipelineRunStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public string TriggerKind { get; set; } = "manual";
    public string EdgesJson { get; set; } = "[]";
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class TaskRun
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public TaskRunStatus Status { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public string? OutputJson { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NotBefore { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class TaskAttempt
{
    public Guid Id { get; set; }
    public Guid TaskRunId { get; set; }
    public int AttemptNumber { get; set; }
    public TaskRunStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ExecutionLog
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid? TaskRunId { get; set; }
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
