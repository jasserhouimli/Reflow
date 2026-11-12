using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.NodeTypes.Abstractions;

/// <summary>
/// Encapsulates all runtime execution parameters provided to a node handler.
/// Follows the Parameter Object pattern to keep INodeHandler unified and extensible.
/// </summary>
public sealed record NodeExecutionContext(
    Guid RunId,
    string NodeId,
    IReadOnlyList<NodeInput> Inputs,
    JsonElement Config,
    string? TriggerPayloadJson = null,
    CancellationToken CancellationToken = default)
{
    /// <summary>
    /// Enforces that this node receives exactly one upstream input table.
    /// </summary>
    public Frame RequireSingleInput(string nodeType)
    {
        if (Inputs.Count != 1)
            throw new InvalidOperationException($"{nodeType} needs exactly one input table");
        return Inputs[0].Frame;
    }

    /// <summary>
    /// Convenience factory for unit tests and local execution.
    /// </summary>
    public static NodeExecutionContext ForTest(
        IReadOnlyList<NodeInput>? inputs = null,
        JsonElement? config = null,
        Guid? runId = null,
        string? nodeId = null,
        string? triggerPayloadJson = null,
        CancellationToken ct = default)
    {
        using var emptyDoc = JsonDocument.Parse("{}");
        var cfg = config ?? emptyDoc.RootElement.Clone();
        return new NodeExecutionContext(
            runId ?? Guid.NewGuid(),
            nodeId ?? "test_node",
            inputs ?? Array.Empty<NodeInput>(),
            cfg,
            triggerPayloadJson,
            ct);
    }
}
