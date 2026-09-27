using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.CsvRead;

public sealed record CsvReadConfig(string CsvText, char Delimiter, bool HasHeader);

public static class CsvReadValidator
{
    public static (CsvReadConfig? Config, IReadOnlyList<string> Errors) Parse(JsonElement config)
    {
        var errors = new List<string>();

        if (config.ValueKind != JsonValueKind.Object)
            return (null, new[] { "csv.read config must be an object" });

        var csvText = config.TryGetProperty("csvText", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrEmpty(csvText))
            errors.Add("csv.read requires 'csvText'");

        var delimiter = ',';
        if (config.TryGetProperty("delimiter", out var d) && d.ValueKind == JsonValueKind.String)
        {
            var s = d.GetString() ?? string.Empty;
            if (s.Length != 1)
                errors.Add("csv.read 'delimiter' must be a single character");
            else
                delimiter = s[0];
        }

        var hasHeader = true;
        if (config.TryGetProperty("hasHeader", out var h))
        {
            if (h.ValueKind is JsonValueKind.True or JsonValueKind.False)
                hasHeader = h.GetBoolean();
            else
                errors.Add("csv.read 'hasHeader' must be a boolean");
        }

        return errors.Count > 0
            ? (null, errors)
            : (new CsvReadConfig(csvText, delimiter, hasHeader), errors);
    }
}

public sealed class CsvReadHandler : INodeHandler
{
    public const string NodeType = "csv.read";

    private readonly IDataReader _reader;

    public CsvReadHandler(IDataReader reader) => _reader = reader;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "CSV Read",
        "Sources",
        "Parse CSV text into a table.",
        Array.Empty<string>(),
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config) =>
        CsvReadValidator.Parse(config).Errors;

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<Frame> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var (parsed, errors) = CsvReadValidator.Parse(config);
        if (parsed is null)
            throw new InvalidOperationException("Invalid csv.read config: " + string.Join("; ", errors));
        return Task.FromResult(_reader.FromCsv(parsed.CsvText, parsed.Delimiter, parsed.HasHeader));
    }
}
