using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Registry;

/// <summary>
/// Abstraction for the registry mapping node type identifiers to their execution handlers.
/// </summary>
public interface INodeTypeRegistry
{
    void Register(INodeHandler handler);
    bool TryGet(string type, out INodeHandler? handler);
    IReadOnlyList<NodeDefinition> Definitions();
}
