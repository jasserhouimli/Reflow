using System.Text.Json;

namespace Reflow.Modules.Pipelines.Validation;

public sealed record GraphNode(string NodeId, string NodeType, string? ConfigJson);
public sealed record GraphEdge(string SourceNodeId, string TargetNodeId);

/// <summary>
/// Structural DAG validation. Per-type config schemas arrive with NodeTypes;
/// here we check shape only (ids, edges, cycles, limits).
/// </summary>
public static class PipelineGraphValidator
{
    public const int MaxNodes = 100;

    public static IReadOnlyList<string> Validate(
        IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<GraphEdge> edges)
    {
        var errors = new List<string>();

        if (nodes.Count == 0)
        {
            errors.Add("Pipeline must contain at least one node");
            return errors;
        }

        if (nodes.Count > MaxNodes)
            errors.Add($"Pipeline has too many nodes ({nodes.Count}, max {MaxNodes})");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                errors.Add("Node id is required");
                continue;
            }
            if (!ids.Add(node.NodeId))
                errors.Add($"Duplicate node id '{node.NodeId}'");
            if (string.IsNullOrWhiteSpace(node.NodeType))
                errors.Add($"Node '{node.NodeId}' is missing a type");
            if (node.ConfigJson is not null)
            {
                try
                {
                    JsonDocument.Parse(node.ConfigJson);
                }
                catch (JsonException)
                {
                    errors.Add($"Node '{node.NodeId}' has invalid config JSON");
                }
            }
        }

        var idSet = new HashSet<string>(nodes.Select(n => n.NodeId), StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (string.Equals(edge.SourceNodeId, edge.TargetNodeId, StringComparison.Ordinal))
                errors.Add($"Self-edge on node '{edge.SourceNodeId}' is not allowed");
            if (!idSet.Contains(edge.SourceNodeId))
                errors.Add($"Edge references unknown source node '{edge.SourceNodeId}'");
            if (!idSet.Contains(edge.TargetNodeId))
                errors.Add($"Edge references unknown target node '{edge.TargetNodeId}'");
        }

        if (HasCycle(nodes.Select(n => n.NodeId), edges))
            errors.Add("Pipeline graph contains a cycle");

        return errors;
    }

    internal static bool HasCycle(IEnumerable<string> nodeIds, IReadOnlyList<GraphEdge> edges)
    {
        var adjacency = edges
            .GroupBy(e => e.SourceNodeId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.TargetNodeId).ToList(),
                StringComparer.Ordinal);
        var state = new Dictionary<string, int>(StringComparer.Ordinal);

        bool Visit(string id)
        {
            state[id] = 1;
            if (adjacency.TryGetValue(id, out var next))
            {
                foreach (var target in next)
                {
                    if (!state.TryGetValue(target, out var s))
                    {
                        if (Visit(target))
                            return true;
                    }
                    else if (s == 1)
                    {
                        return true;
                    }
                }
            }
            state[id] = 2;
            return false;
        }

        return nodeIds.Any(id => !state.ContainsKey(id) && Visit(id));
    }
}
