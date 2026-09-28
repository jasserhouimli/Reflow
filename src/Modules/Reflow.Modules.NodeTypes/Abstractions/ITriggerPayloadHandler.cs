using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.NodeTypes.Abstractions;

/// <summary>
/// Nodes that consume the run's trigger payload instead of upstream frames
/// (e.g. trigger.payload). The engine supplies the payload; the slice owns
/// parsing and validation.
/// </summary>
public interface ITriggerPayloadHandler : INodeHandler
{
    Task<Frame> ExecuteWithPayload(
        JsonElement config,
        string? triggerPayloadJson,
        CancellationToken ct);
}
