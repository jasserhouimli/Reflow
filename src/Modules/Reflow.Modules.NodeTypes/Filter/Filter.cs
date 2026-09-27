using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Filter;

/// <summary>
/// Compiles the predicate to DuckDB SQL. No C# row logic: the engine filters.
/// </summary>
public sealed class FilterHandler : INodeHandler
{
    public const string NodeType = "filter";

    private static readonly HashSet<string> Ops = new(StringComparer.Ordinal)
    {
        "equals", "notEquals", "contains", "startsWith",
        "greaterThan", "lessThan", "isEmpty", "isNotEmpty",
    };

    private readonly ISqlEngine _sql;

    public FilterHandler(ISqlEngine sql) => _sql = sql;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "Filter",
        "Transform",
        "Keep rows matching a column predicate.",
        new[] { "input" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "filter config must be an object" };

        if (!config.TryGetProperty("column", out var c)
            || c.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(c.GetString()))
            errors.Add("filter requires 'column'");

        if (!config.TryGetProperty("operator", out var o)
            || o.ValueKind != JsonValueKind.String
            || !Ops.Contains(o.GetString() ?? string.Empty))
            errors.Add($"filter 'operator' must be one of: {string.Join(", ", Ops)}");
        return errors;
    }

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<Frame> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid filter config: " + string.Join("; ", errors));
        if (inputs.Count != 1)
            throw new InvalidOperationException("filter needs exactly one input");

        var value = config.TryGetProperty("value", out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()
            : null;
        var sql = $"SELECT * FROM input WHERE {SqlPredicate.Build(
            config.GetProperty("column").GetString()!,
            config.GetProperty("operator").GetString()!,
            value)}";
        return Task.FromResult(_sql.Query(new[] { ("input", inputs[0]) }, sql));
    }
}
