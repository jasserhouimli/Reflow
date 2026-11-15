using System.Text.Json;

namespace Reflow.Modules.NodeTypes.Abstractions;

// Configuration property metadata exposed by node types for dynamic prompt generation.
public sealed record NodeConfigField(
    string Name,
    string Type,
    bool Required = false,
    string? Description = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? Example = null);

// Implemented by node types that provide self-describing config schemas.
public interface INodeConfigSchema
{
    IReadOnlyList<NodeConfigField> ConfigSchema { get; }
}
