using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.FileRead;

public sealed record FileReadConfig(
    Guid FileId, string Format, char Delimiter, bool HasHeader, string? RootPath);

public static class FileReadConfigParser
{
    private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        "csv", "json",
    };

    public static (FileReadConfig? Config, IReadOnlyList<string> Errors) Parse(JsonElement config)
    {
        var errors = new List<string>();

        if (config.ValueKind != JsonValueKind.Object)
            return (null, new[] { "file.read config must be an object" });

        var fileId = Guid.Empty;
        if (!config.TryGetProperty("fileId", out var idElement)
            || idElement.ValueKind != JsonValueKind.String
            || !Guid.TryParse(idElement.GetString(), out fileId))
        {
            errors.Add("file.read requires a valid 'fileId'");
        }

        var format = "csv";
        if (config.TryGetProperty("format", out var f) && f.ValueKind == JsonValueKind.String)
        {
            var value = f.GetString() ?? string.Empty;
            if (!Formats.Contains(value))
                errors.Add("file.read 'format' must be csv or json");
            else
                format = value.ToLowerInvariant();
        }
        else if (config.TryGetProperty("format", out var invalidFormat) && invalidFormat.ValueKind != JsonValueKind.Null)
        {
            errors.Add("file.read 'format' must be a string");
        }

        var delimiter = ',';
        if (config.TryGetProperty("delimiter", out var d) && d.ValueKind == JsonValueKind.String)
        {
            var value = d.GetString() ?? string.Empty;
            if (value.Length != 1)
                errors.Add("file.read 'delimiter' must be a single character");
            else
                delimiter = value[0];
        }

        var hasHeader = true;
        if (config.TryGetProperty("hasHeader", out var h))
        {
            if (h.ValueKind is JsonValueKind.True or JsonValueKind.False)
                hasHeader = h.GetBoolean();
            else
                errors.Add("file.read 'hasHeader' must be a boolean");
        }

        string? rootPath = null;
        if (config.TryGetProperty("rootPath", out var r) && r.ValueKind == JsonValueKind.String)
            rootPath = r.GetString();

        return errors.Count > 0
            ? (null, errors)
            : (new FileReadConfig(fileId, format, delimiter, hasHeader, rootPath), errors);
    }
}

/// <summary>
/// Reads an uploaded CSV/JSON source by id. Delegates parsing to the shared
/// data-plane reader so this node holds no parsing logic of its own.
/// </summary>
public sealed class FileReadHandler : INodeHandler, INodeConfigSchema
{
    public const string NodeType = "file.read";

    private readonly IDataReader _reader;
    private readonly IFileSourceReader _files;

    public FileReadHandler(IDataReader reader, IFileSourceReader files)
    {
        _reader = reader;
        _files = files;
    }

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "File Read",
        "Sources",
        "Read an uploaded CSV or JSON file into a table.",
        Array.Empty<string>(),
        new[] { "output" });

    public IReadOnlyList<NodeConfigField> ConfigSchema =>
    [
        new("fileId", "string", true, "Id of one of the user's uploaded files (from the available files list)"),
        new("format", "string", false, "Source format", ["csv", "json"], "csv"),
        new("delimiter", "string", false, "Single character delimiter (csv only)", null, ","),
        new("hasHeader", "boolean", false, "Whether the first row holds column names (csv only)", null, "true"),
        new("rootPath", "string", false, "Dot path to the array/object to parse (json only)"),
    ];

    public IReadOnlyList<string> ValidateConfig(JsonElement config) =>
        FileReadConfigParser.Parse(config).Errors;

    public async Task<Frame> ExecuteAsync(NodeExecutionContext context)
    {
        var (config, errors) = FileReadConfigParser.Parse(context.Config);
        if (config is null)
            throw new InvalidOperationException(
                "Invalid file.read config: " + string.Join("; ", errors));

        var file = await _files.FindAsync(config.FileId, context.CancellationToken);
        if (file is null)
            throw new InvalidOperationException($"Source file '{config.FileId}' was not found");

        return config.Format == "json"
            ? _reader.FromJson(file.Content, config.RootPath)
            : _reader.FromCsv(file.Content, config.Delimiter, config.HasHeader);
    }
}
