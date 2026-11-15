using System.Text.Json;

namespace Reflow.Modules.AiAssistant.Features.GenerateWorkflow;

public sealed record DraftNode(string NodeId, string NodeType, string? Label, JsonElement Config);

public sealed record DraftEdge(string SourceNodeId, string TargetNodeId);

public sealed record WorkflowDraft(
    string Name,
    string Explanation,
    IReadOnlyList<DraftNode> Nodes,
    IReadOnlyList<DraftEdge> Edges,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Turns model output into a draft. Deliberately tolerant about aliases and
/// strict about structure: anything ambiguous becomes an error the repair loop
/// can act on, never a partially-applied workflow.
/// </summary>
public static class WorkflowDraftParser
{
    public static (WorkflowDraft? Draft, string? Error) Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, "the model returned an empty response");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(StripCodeFences(raw));
        }
        catch (JsonException)
        {
            return (null, "the response was not valid JSON");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, "the response must be a JSON object");

            if (!root.TryGetProperty("nodes", out var nodesElement)
                || nodesElement.ValueKind != JsonValueKind.Array
                || nodesElement.GetArrayLength() == 0)
            {
                return (null, "the response must contain a non-empty 'nodes' array");
            }

            var nodes = new List<DraftNode>();
            var index = 0;
            foreach (var element in nodesElement.EnumerateArray())
            {
                index++;
                if (element.ValueKind != JsonValueKind.Object)
                    return (null, $"node #{index} must be a JSON object");

                var nodeId = ReadString(element, "nodeId", "id");
                var nodeType = ReadString(element, "nodeType", "type");
                if (string.IsNullOrWhiteSpace(nodeId))
                    return (null, $"node #{index} is missing 'nodeId'");
                if (string.IsNullOrWhiteSpace(nodeType))
                    return (null, $"node '{nodeId}' is missing 'nodeType'");

                var config = ReadConfig(element);
                if (config is null)
                    return (null, $"node '{nodeId}' config must be a JSON object");

                nodes.Add(new DraftNode(
                    nodeId.Trim(),
                    nodeType.Trim(),
                    ReadString(element, "label"),
                    config.Value));
            }

            var edges = new List<DraftEdge>();
            if (root.TryGetProperty("edges", out var edgesElement)
                && edgesElement.ValueKind == JsonValueKind.Array)
            {
                index = 0;
                foreach (var element in edgesElement.EnumerateArray())
                {
                    index++;
                    if (element.ValueKind != JsonValueKind.Object)
                        return (null, $"edge #{index} must be a JSON object");

                    var source = ReadString(element, "sourceNodeId", "source");
                    var target = ReadString(element, "targetNodeId", "target");
                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                        return (null, $"edge #{index} needs 'sourceNodeId' and 'targetNodeId'");

                    edges.Add(new DraftEdge(source.Trim(), target.Trim()));
                }
            }

            var warnings = new List<string>();
            if (root.TryGetProperty("warnings", out var warningsElement)
                && warningsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in warningsElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(element.GetString()))
                        warnings.Add(element.GetString()!.Trim());
                }
            }

            var name = ReadString(root, "name");
            var explanation = ReadString(root, "explanation") ?? string.Empty;

            return (new WorkflowDraft(
                string.IsNullOrWhiteSpace(name) ? "Generated workflow" : name.Trim(),
                explanation.Trim(),
                nodes,
                edges,
                warnings), null);
        }
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }
        return null;
    }

    private static JsonElement? ReadConfig(JsonElement element)
    {
        if (!element.TryGetProperty("config", out var config)
            || config.ValueKind == JsonValueKind.Null)
            return EmptyObject();

        if (config.ValueKind == JsonValueKind.Object)
            return config.Clone();

        // Models sometimes double-encode config as a JSON string.
        if (config.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var inner = JsonDocument.Parse(config.GetString() ?? "{}");
                return inner.RootElement.ValueKind == JsonValueKind.Object
                    ? inner.RootElement.Clone()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return null;
    }

    private static JsonElement EmptyObject() =>
        JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>Models still wrap JSON in markdown fences despite instructions.</summary>
    private static string StripCodeFences(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return trimmed;

        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence <= firstNewline)
            return trimmed;

        return trimmed[(firstNewline + 1)..lastFence].Trim();
    }
}
