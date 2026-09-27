using Reflow.Modules.Pipelines.Validation;
using Xunit;

namespace Reflow.UnitTests;

public class PipelineGraphValidatorTests
{
    [Fact]
    public void EmptyGraph_IsInvalid()
    {
        var errors = PipelineGraphValidator.Validate(new List<GraphNode>(), new List<GraphEdge>());
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidChain_Passes()
    {
        var nodes = new List<GraphNode>
        {
            new("a", "csv.read", "{}"),
            new("b", "filter", "{}"),
        };
        var edges = new List<GraphEdge> { new("a", "b") };
        Assert.Empty(PipelineGraphValidator.Validate(nodes, edges));
    }

    [Fact]
    public void Cycle_IsInvalid()
    {
        var nodes = new List<GraphNode> { new("a", "t", "{}"), new("b", "t", "{}") };
        var edges = new List<GraphEdge> { new("a", "b"), new("b", "a") };
        Assert.Contains(PipelineGraphValidator.Validate(nodes, edges),
            e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SelfEdge_IsInvalid()
    {
        var nodes = new List<GraphNode> { new("a", "t", "{}") };
        var edges = new List<GraphEdge> { new("a", "a") };
        Assert.Contains(PipelineGraphValidator.Validate(nodes, edges),
            e => e.Contains("Self-edge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DanglingEdge_IsInvalid()
    {
        var nodes = new List<GraphNode> { new("a", "t", "{}") };
        var edges = new List<GraphEdge> { new("a", "ghost") };
        Assert.Contains(PipelineGraphValidator.Validate(nodes, edges),
            e => e.Contains("ghost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DuplicateNodeIds_AreInvalid()
    {
        var nodes = new List<GraphNode> { new("a", "t", "{}"), new("a", "t", "{}") };
        Assert.Contains(PipelineGraphValidator.Validate(nodes, new List<GraphEdge>()),
            e => e.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BadConfigJson_IsInvalid()
    {
        var nodes = new List<GraphNode> { new("a", "t", "{oops") };
        Assert.Contains(PipelineGraphValidator.Validate(nodes, new List<GraphEdge>()),
            e => e.Contains("config JSON", StringComparison.OrdinalIgnoreCase));
    }
}
