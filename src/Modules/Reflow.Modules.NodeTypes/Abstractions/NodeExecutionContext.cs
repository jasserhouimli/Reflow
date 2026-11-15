using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.NodeTypes.Abstractions;

// Execution context provided to a node handler during task run.
public sealed record NodeExecutionContext(
    Guid RunId,
    string NodeId,
    IReadOnlyList<NodeInput> Inputs,
    JsonElement Config,
    string? TriggerPayloadJson = null,
    CancellationToken CancellationToken = default)
{
    public Frame RequireSingleInput(string nodeType)
    {
        if (Inputs.Count != 1)
            throw new InvalidOperationException($"{nodeType} needs exactly one input table");
        return Inputs[0].Frame;
    }

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
