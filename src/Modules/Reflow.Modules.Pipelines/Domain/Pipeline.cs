namespace Reflow.Modules.Pipelines.Domain;

public enum PipelineStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

public class Pipeline
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PipelineStatus Status { get; set; } = PipelineStatus.Draft;
    public int CurrentVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<PipelineNode> Nodes { get; set; } = new();
    public List<PipelineEdge> Edges { get; set; } = new();
}

public class PipelineNode
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public string? Label { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
}

public class PipelineEdge
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
}

public class PipelineVersion
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public int VersionNumber { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public Guid PublishedBy { get; set; }
    public DateTime PublishedAt { get; set; }
}
