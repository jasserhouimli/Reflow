using System.Text.RegularExpressions;
using Reflow.Infrastructure.Files;
using Reflow.Infrastructure.Graphs;
using Reflow.Modules.NodeTypes.Registry;

namespace Reflow.Modules.AiAssistant.Features.GenerateWorkflow;

// Validates untrusted LLM drafts against node types, config schemas, and acyclicity rules.
public sealed class WorkflowDraftValidator(INodeTypeRegistry registry)
{
    public const int MaxNodeIdLength = 40;

    // Node ids become SQL table names in data.sql, so they are restricted to a
    // conservative slug.
    private static readonly Regex NodeIdPattern = new(
        "^[a-z][a-z0-9_]*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public IReadOnlyList<string> Validate(
        WorkflowDraft draft, int maxNodes, IReadOnlyList<SourceFileRef>? availableFiles = null)
    {
        var errors = new List<string>();

        if (draft.Nodes.Count == 0)
            return ["the workflow has no nodes"];

        if (draft.Nodes.Count > maxNodes)
            errors.Add($"the workflow has {draft.Nodes.Count} nodes, the maximum is {maxNodes}");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in draft.Nodes)
        {
            if (node.NodeId.Length > MaxNodeIdLength || !NodeIdPattern.IsMatch(node.NodeId))
                errors.Add(
                    $"node id '{node.NodeId}' must be a lowercase slug (letters, digits, underscore, max {MaxNodeIdLength})");

            if (!seen.Add(node.NodeId))
                errors.Add($"node id '{node.NodeId}' is used more than once");

            if (!registry.TryGet(node.NodeType, out var handler) || handler is null)
            {
                errors.Add($"unknown node type '{node.NodeType}'");
                continue;
            }

            if (node.Config.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                errors.Add($"node '{node.NodeId}' config must be a JSON object");
                continue;
            }

            foreach (var error in handler.ValidateConfig(node.Config))
                errors.Add($"node '{node.NodeId}' ({node.NodeType}): {error}");

            if (string.Equals(node.NodeType, "file.read", StringComparison.OrdinalIgnoreCase) && availableFiles is not null)
            {
                if (availableFiles.Count == 0)
                {
                    errors.Add(
                        $"node '{node.NodeId}' (file.read): Cannot use 'file.read' because AVAILABLE FILES is empty. Use 'csv.read' with inline sample data ('csvText') or 'trigger.payload' instead.");
                }
                else if (node.Config.TryGetProperty("fileId", out var idElement)
                    && idElement.ValueKind == System.Text.Json.JsonValueKind.String
                    && Guid.TryParse(idElement.GetString(), out var requestedFileId))
                {
                    if (!availableFiles.Any(f => f.Id == requestedFileId))
                    {
                        var validList = string.Join(", ", availableFiles.Select(f => $"{f.Id} (\"{f.FileName}\")"));
                        errors.Add(
                            $"node '{node.NodeId}' (file.read): fileId '{requestedFileId}' is not in AVAILABLE FILES. Valid files: [{validList}], or use 'csv.read' with inline sample data ('csvText') instead.");
                    }
                }
            }
        }

        var graphNodes = draft.Nodes
            .Select(n => new GraphNode(n.NodeId, n.NodeType, n.Config.GetRawText()))
            .ToList();
        var graphEdges = draft.Edges
            .Select(e => new GraphEdge(e.SourceNodeId, e.TargetNodeId))
            .ToList();

        foreach (var error in PipelineGraphValidator.Validate(graphNodes, graphEdges))
            errors.Add(error);

        return errors.Distinct(StringComparer.Ordinal).ToList();
    }
}
