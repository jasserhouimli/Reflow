using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.JsonRead;

/// <summary>
/// Parses standalone JSON text, or unpacks a JSON column from one upstream
/// frame (objects merge into the row, arrays explode into rows).
/// </summary>
public sealed class JsonReadHandler : INodeHandler
{
    public const string NodeType = "json.read";

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "JSON Read",
        "Sources",
        "Parse JSON text or unpack a JSON column from upstream.",
        new[] { "input" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "json.read config must be an object" };

        var hasText = config.TryGetProperty("jsonText", out var t)
            && t.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(t.GetString());
        var hasColumn = config.TryGetProperty("column", out var c)
            && c.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(c.GetString());

        if (hasText == hasColumn)
            errors.Add("json.read needs exactly one of 'jsonText' or 'column'");
        return errors;
    }

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<Frame> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid json.read config: " + string.Join("; ", errors));

        if (config.TryGetProperty("column", out var c)
            && c.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(c.GetString()))
        {
            if (inputs.Count != 1)
                throw new InvalidOperationException("json.read column mode needs exactly one input");
            return Task.FromResult(Unpack(inputs[0], c.GetString()!));
        }

        var jsonText = config.GetProperty("jsonText").GetString()!;
        var rootPath = config.TryGetProperty("rootPath", out var r)
            && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        return Task.FromResult(JsonCodec.FromJson(jsonText, rootPath));
    }

    internal static Frame Unpack(Frame input, string column)
    {
        var idx = input.ColumnIndex(column);
        if (idx < 0)
            throw new InvalidOperationException($"Unknown column '{column}'");

        var output = new Frame();
        foreach (var row in input.Rows)
        {
            var cell = idx < row.Count ? row[idx] : null;
            if (string.IsNullOrWhiteSpace(cell))
                continue;
            using var doc = JsonDocument.Parse(cell!);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                    AddMerged(output, input, row, el);
            }
            else
            {
                AddMerged(output, input, row, doc.RootElement);
            }
        }
        return output;
    }

    private static void AddMerged(Frame output, Frame input, List<string?> row, JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("json.read can only unpack objects and arrays of objects");

        foreach (var prop in el.EnumerateObject())
        {
            if (!output.Columns.Contains(prop.Name))
                output.Columns.Add(prop.Name);
        }
        foreach (var col in input.Columns)
        {
            if (!output.Columns.Contains(col))
                output.Columns.Add(col);
        }

        var merged = output.Columns.Select(col =>
        {
            if (el.TryGetProperty(col, out var v))
                return v.ValueKind == JsonValueKind.Null ? null : v.ToString();
            var i = input.ColumnIndex(col);
            return i >= 0 && i < row.Count ? row[i] : null;
        }).ToList();
        output.Rows.Add(merged);
        if (output.Rows.Count > Frame.MaxRows)
            throw new InvalidOperationException($"Too many records (max {Frame.MaxRows})");
    }
}
