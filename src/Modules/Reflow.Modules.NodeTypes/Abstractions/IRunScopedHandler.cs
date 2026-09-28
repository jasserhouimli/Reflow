using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.NodeTypes.Abstractions;

/// <summary>
/// Nodes that need run identity (e.g. data.output saving artifacts).
/// The engine supplies run/node ids; the slice owns the side effect.
/// </summary>
public interface IRunScopedHandler : INodeHandler
{
    Task<Frame> ExecuteInRunAsync(
        IReadOnlyList<NodeInput> inputs,
        JsonElement config,
        Guid runId,
        string nodeId,
        CancellationToken ct);
}
