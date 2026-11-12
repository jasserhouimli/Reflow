using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.NodeTypes.Abstractions;

/// <summary>One upstream table: source node id plus its frame.</summary>
public sealed record NodeInput(string NodeId, Frame Frame);

/// <summary>
/// One node type's vertical slice contract: stable id, editor definition,
/// config validation, and unified execution. The engine resolves handlers through the
/// registry and never switches on node types.
/// </summary>
public interface INodeHandler
{
    string Type { get; }
    NodeDefinition Definition { get; }

    IReadOnlyList<string> ValidateConfig(JsonElement config);

    Task<Frame> ExecuteAsync(NodeExecutionContext context);
}
