namespace Reflow.Modules.NodeTypes.Abstractions;

/// <summary>Editor-facing node metadata. Drives the palette (no hardcoded lists).</summary>
public sealed record NodeDefinition(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<string> InputPorts,
    IReadOnlyList<string> OutputPorts);
