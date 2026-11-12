using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.DataOutput;

/// <summary>
/// Saves the input frame as a downloadable run artifact (JSON/CSV).
/// Passes the frame through so downstream nodes keep working.
/// </summary>
public sealed class DataOutputHandler : INodeHandler
{
    public const string NodeType = "data.output";

    private readonly IDataArtifactStore _artifacts;

    public DataOutputHandler(IDataArtifactStore artifacts) => _artifacts = artifacts;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "Output",
        "Sinks",
        "Save the table as a downloadable file artifact.",
        new[] { "input" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "data.output config must be an object" };
        if (config.TryGetProperty("format", out var f)
            && (f.ValueKind != JsonValueKind.String
                || (f.GetString() is not "json" and not "csv")))
            errors.Add("data.output 'format' must be json or csv");
        return errors;
    }

    public async Task<Frame> ExecuteAsync(NodeExecutionContext context)
    {
        var errors = ValidateConfig(context.Config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid data.output config: " + string.Join("; ", errors));

        var input = context.RequireSingleInput("data.output");

        var format = context.Config.TryGetProperty("format", out var f)
            && f.ValueKind == JsonValueKind.String ? f.GetString()! : "json";
        var fileName = context.Config.TryGetProperty("fileName", out var n)
            && n.ValueKind == JsonValueKind.String ? n.GetString() : null;

        await _artifacts.SaveAsync(context.RunId, context.NodeId, input, format, fileName, ct: context.CancellationToken);
        return input;
    }
}
