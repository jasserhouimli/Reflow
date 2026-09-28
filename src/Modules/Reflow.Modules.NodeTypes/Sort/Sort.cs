using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Sort;

/// <summary>ORDER BY compiled to DuckDB SQL.</summary>
public sealed class SortHandler : INodeHandler
{
    public const string NodeType = "sort";

    private readonly ISqlEngine _sql;

    public SortHandler(ISqlEngine sql) => _sql = sql;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "Sort",
        "Transform",
        "Order rows by one or more columns.",
        new[] { "input" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "sort config must be an object" };
        if (!config.TryGetProperty("orderBy", out var o)
            || o.ValueKind != JsonValueKind.Array
            || o.GetArrayLength() == 0)
        {
            errors.Add("sort requires a non-empty 'orderBy' array");
            return errors;
        }
        foreach (var item in o.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("column", out var c)
                || c.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(c.GetString()))
            {
                errors.Add("sort 'orderBy' entries need 'column'");
                continue;
            }
            if (item.TryGetProperty("direction", out var d)
                && (d.ValueKind != JsonValueKind.String
                    || (d.GetString() is not "asc" and not "desc"
                        and not "ASC" and not "DESC")))
                errors.Add($"sort direction for '{c.GetString()}' must be asc or desc");
        }
        return errors;
    }

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<NodeInput> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid sort config: " + string.Join("; ", errors));
        if (inputs.Count != 1)
            throw new InvalidOperationException("sort needs exactly one input");

        var keys = config.GetProperty("orderBy").EnumerateArray().Select(item =>
        {
            var dir = item.TryGetProperty("direction", out var d)
                && d.ValueKind == JsonValueKind.String
                && string.Equals(d.GetString(), "desc", StringComparison.OrdinalIgnoreCase)
                ? "DESC" : "ASC";
            return $"{SqlPredicate.Column(item.GetProperty("column").GetString()!)} {dir}";
        });
        var sql = $"SELECT * FROM input ORDER BY {string.Join(", ", keys)}";
        return Task.FromResult(_sql.Query(new[] { ("input", inputs[0].Frame) }, sql));
    }
}
