using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.DataSql;

/// <summary>
/// Analytical SQL over upstream tables named after their source nodes
/// ("Orders" → orders). With a single input the table is also available as
/// `input`. DuckDB in-memory, no file access.
/// </summary>
public sealed class DataSqlHandler : INodeHandler
{
    public const string NodeType = "data.sql";
    public const int MaxQueryChars = 10000;

    private readonly ISqlEngine _sql;

    public DataSqlHandler(ISqlEngine sql) => _sql = sql;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "SQL",
        "Analysis",
        "Run analytical SQL over upstream tables named after their nodes.",
        new[] { "input" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "data.sql config must be an object" };
        if (!config.TryGetProperty("query", out var q)
            || q.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(q.GetString()))
            errors.Add("data.sql requires 'query'");
        else if (q.GetString()!.Length > MaxQueryChars)
            errors.Add($"data.sql query is too long (max {MaxQueryChars} chars)");
        return errors;
    }

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<NodeInput> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid data.sql config: " + string.Join("; ", errors));
        if (inputs.Count > 8)
            throw new InvalidOperationException("data.sql accepts at most 8 inputs");

        var tables = NameTables(inputs);
        return Task.FromResult(_sql.Query(tables, config.GetProperty("query").GetString()!));
    }

    /// <summary>Node id → table name. Lowercased, sanitized, deduped.</summary>
    public static List<(string Name, Frame Frame)> NameTables(IReadOnlyList<NodeInput> inputs)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tables = new List<(string, Frame)>();
        foreach (var input in inputs)
        {
            var name = TableName(input.NodeId, used);
            used.Add(name);
            tables.Add((name, input.Frame));
        }
        if (tables.Count == 1 && !string.Equals(tables[0].Item1, "input", StringComparison.OrdinalIgnoreCase))
            tables.Add(("input", inputs[0].Frame));
        return tables;
    }

    internal static string TableName(string nodeId, HashSet<string> used)
    {
        var clean = new string(nodeId
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray()).Trim('_');
        if (string.IsNullOrEmpty(clean))
            clean = "input";
        if (char.IsDigit(clean[0]))
            clean = "t_" + clean;
        var name = clean;
        for (var i = 2; used.Contains(name); i++)
            name = $"{clean}_{i}";
        return name;
    }
}
