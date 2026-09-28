using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Aggregate;

/// <summary>GROUP BY summarization compiled to DuckDB SQL.</summary>
public sealed class AggregateHandler : INodeHandler
{
    public const string NodeType = "aggregate";

    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "count", "countDistinct", "sum", "avg", "min", "max",
    };

    private readonly ISqlEngine _sql;

    public AggregateHandler(ISqlEngine sql) => _sql = sql;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "Aggregate",
        "Transform",
        "Group rows and compute summaries.",
        new[] { "input" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "aggregate config must be an object" };
        if (config.TryGetProperty("groupBy", out var g)
            && (g.ValueKind != JsonValueKind.Array
                || g.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String)))
            errors.Add("aggregate 'groupBy' must be an array of column names");
        if (!config.TryGetProperty("operations", out var ops)
            || ops.ValueKind != JsonValueKind.Array
            || ops.GetArrayLength() == 0)
        {
            errors.Add("aggregate requires a non-empty 'operations' array");
            return errors;
        }
        foreach (var op in ops.EnumerateArray())
        {
            if (op.ValueKind != JsonValueKind.Object
                || !op.TryGetProperty("function", out var f)
                || f.ValueKind != JsonValueKind.String
                || !Functions.Contains(f.GetString() ?? string.Empty))
            {
                errors.Add($"aggregate function must be one of: {string.Join(", ", Functions)}");
                continue;
            }
            var fn = f.GetString()!;
            if (!string.Equals(fn, "count", StringComparison.OrdinalIgnoreCase)
                && (!op.TryGetProperty("column", out var c)
                    || c.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(c.GetString())))
                errors.Add($"aggregate '{fn}' requires 'column'");
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
            throw new InvalidOperationException("Invalid aggregate config: " + string.Join("; ", errors));
        if (inputs.Count != 1)
            throw new InvalidOperationException("aggregate needs exactly one input");

        var groups = config.TryGetProperty("groupBy", out var g)
            ? g.EnumerateArray().Select(e => e.GetString()!).ToList()
            : new List<string>();
        var select = groups.Select(SqlPredicate.Column).ToList();
        foreach (var op in config.GetProperty("operations").EnumerateArray())
        {
            var fn = op.GetProperty("function").GetString()!;
            var col = op.TryGetProperty("column", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()! : string.Empty;
            var alias = op.TryGetProperty("as", out var a) && a.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(a.GetString())
                ? a.GetString()!
                : DefaultAlias(fn, col);
            select.Add($"{AggregateSql(fn, col)} AS {SqlPredicate.Column(alias)}");
        }

        var sql = groups.Count > 0
            ? $"SELECT {string.Join(", ", select)} FROM input GROUP BY {string.Join(", ", groups.Select(SqlPredicate.Column))}"
            : $"SELECT {string.Join(", ", select)} FROM input";
        return Task.FromResult(_sql.Query(new[] { ("input", inputs[0].Frame) }, sql));
    }

    internal static string AggregateSql(string function, string column) =>
        function.ToLowerInvariant() switch
        {
            "count" => "COUNT(*)",
            "countdistinct" => $"COUNT(DISTINCT {SqlPredicate.Column(column)})",
            "sum" => $"SUM(CAST({SqlPredicate.Column(column)} AS DOUBLE))",
            "avg" => $"AVG(CAST({SqlPredicate.Column(column)} AS DOUBLE))",
            "min" => $"MIN({SqlPredicate.Column(column)})",
            "max" => $"MAX({SqlPredicate.Column(column)})",
            _ => throw new InvalidOperationException($"Unknown aggregate '{function}'"),
        };

    private static string DefaultAlias(string function, string column) =>
        string.Equals(function, "count", StringComparison.OrdinalIgnoreCase)
            ? "count"
            : $"{function.ToLowerInvariant()}_{column}";
}
