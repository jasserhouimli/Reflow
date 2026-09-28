using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.TriggerPayload;

/// <summary>Parses the payload that started the run (webhook body / schedule tick).</summary>
public sealed class TriggerPayloadHandler : ITriggerPayloadHandler
{
    public const string NodeType = "trigger.payload";

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "Trigger Payload",
        "Sources",
        "Parse the JSON payload that started this run.",
        Array.Empty<string>(),
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "trigger.payload config must be an object" };
        if (config.TryGetProperty("rootPath", out var r)
            && r.ValueKind != JsonValueKind.String)
            return new[] { "trigger.payload 'rootPath' must be a string" };
        return Array.Empty<string>();
    }

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<Frame> inputs,
        JsonElement config,
        CancellationToken ct) =>
        throw new InvalidOperationException("trigger.payload runs only with a trigger payload");

    public Task<Frame> ExecuteWithPayload(
        JsonElement config,
        string? triggerPayloadJson,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid trigger.payload config: " + string.Join("; ", errors));
        if (string.IsNullOrWhiteSpace(triggerPayloadJson))
            throw new InvalidOperationException(
                "Invalid trigger payload: this run was started without a payload");

        var rootPath = config.TryGetProperty("rootPath", out var r)
            && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        try
        {
            return Task.FromResult(JsonCodec.FromJson(triggerPayloadJson, rootPath));
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Invalid trigger payload: " + ex.Message, ex);
        }
    }
}
