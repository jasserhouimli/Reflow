using System.Text.Json;

namespace Reflow.Modules.NodeTypes.Abstractions;

/// <summary>One config property of a node type. Prompt-facing metadata only:
/// runtime validation stays in <see cref="INodeHandler.ValidateConfig"/>.</summary>
public sealed record NodeConfigField(
    string Name,
    string Type,
    bool Required = false,
    string? Description = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? Example = null);

/// <summary>
/// Optional contract: a node slice describes its own configuration so the AI
/// catalog and the repair feedback come from the same place the node validates.
/// </summary>
public interface INodeConfigSchema
{
    IReadOnlyList<NodeConfigField> ConfigSchema { get; }
}
