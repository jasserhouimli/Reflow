using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Registry;

/// <summary>Type → handler map. Slices register themselves; engine resolves.</summary>
public sealed class NodeTypeRegistry
{
    private readonly Dictionary<string, INodeHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(INodeHandler handler) => _handlers[handler.Type] = handler;

    public bool TryGet(string type, out INodeHandler? handler) =>
        _handlers.TryGetValue(type, out handler);

    public IReadOnlyList<Abstractions.NodeDefinition> Definitions() =>
        _handlers.Values.Select(h => h.Definition).OrderBy(d => d.Type).ToList();
}
