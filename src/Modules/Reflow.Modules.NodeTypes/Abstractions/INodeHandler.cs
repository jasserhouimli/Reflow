using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.NodeTypes.Abstractions;

// Upstream input table containing source node id and its output frame.
public sealed record NodeInput(string NodeId, Frame Frame);

// Contract for node type handlers: metadata, config validation, and execution.
public interface INodeHandler
{
    string Type { get; }
    NodeDefinition Definition { get; }

    IReadOnlyList<string> ValidateConfig(JsonElement config);

    Task<Frame> ExecuteAsync(NodeExecutionContext context);
}
