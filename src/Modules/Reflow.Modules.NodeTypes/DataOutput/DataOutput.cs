using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.DataOutput;

/// <summary>Saves the input frame as a downloadable run artifact (JSON/CSV).
/// Passes the frame through so downstream nodes keep working.</summary>
public sealed class DataOutputHandler : IRunScopedHandler
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

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<NodeInput> inputs,
        JsonElement config,
        CancellationToken ct) =>
        throw new InvalidOperationException("data.output runs only inside a pipeline run");

    public async Task<Frame> ExecuteInRunAsync(
        IReadOnlyList<NodeInput> inputs,
        JsonElement config,
        Guid runId,
        string nodeId,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid data.output config: " + string.Join("; ", errors));
        if (inputs.Count != 1)
            throw new InvalidOperationException("data.output needs exactly one input");

        var format = config.TryGetProperty("format", out var f)
            && f.ValueKind == JsonValueKind.String ? f.GetString()! : "json";
        var fileName = config.TryGetProperty("fileName", out var n)
            && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        await _artifacts.SaveAsync(runId, nodeId, inputs[0].Frame, format, fileName, ct: ct);
        return inputs[0].Frame;
    }
}
