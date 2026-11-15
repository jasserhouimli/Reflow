namespace Reflow.Modules.NodeTypes.Abstractions;

// Metadata used to render the node in the canvas palette.
public sealed record NodeDefinition(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<string> InputPorts,
    IReadOnlyList<string> OutputPorts);
