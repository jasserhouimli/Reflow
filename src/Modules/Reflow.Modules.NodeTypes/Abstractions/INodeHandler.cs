using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.NodeTypes.Abstractions;

/// <summary>
/// One node type's vertical slice contract: stable id, editor definition,
/// config validation, execution. The engine resolves handlers through the
/// registry and never switches on node types (RULES §4).
/// </summary>
public interface INodeHandler
{
    string Type { get; }
    NodeDefinition Definition { get; }

    IReadOnlyList<string> ValidateConfig(JsonElement config);

    Task<Frame> ExecuteAsync(
        IReadOnlyList<Frame> inputs,
        JsonElement config,
        CancellationToken ct);
}
