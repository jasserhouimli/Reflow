using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.DataSql;

// Executes free-form DuckDB SQL queries over upstream tables named after node IDs.
public sealed class DataSqlHandler : INodeHandler, INodeConfigSchema
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

    public IReadOnlyList<NodeConfigField> ConfigSchema =>
    [
        new("query", "string", true,
            "Read-only analytical SQL over upstream tables named after their node ids (lowercased), plus 'input' when there is one upstream. Note: table columns are VARCHAR, so cast numbers explicitly e.g. SUM(CAST(amount AS DOUBLE))."),
    ];

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

    public Task<Frame> ExecuteAsync(NodeExecutionContext context)
    {
        var errors = ValidateConfig(context.Config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid data.sql config: " + string.Join("; ", errors));
        if (context.Inputs.Count > 8)
            throw new InvalidOperationException("data.sql accepts at most 8 inputs");

        var tables = NameTables(context.Inputs);
        return Task.FromResult(_sql.Query(tables, context.Config.GetProperty("query").GetString()!));
    }

    // Sanitizes node IDs into valid SQL table names.
    public static List<(string Name, Frame Frame)> NameTables(IReadOnlyList<NodeInput> inputs)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tables = new List<(string, Frame)>();
        foreach (var input in inputs)
            tables.Add((NodeTableName.For(input.NodeId, used), input.Frame));
        if (tables.Count == 1 && !string.Equals(tables[0].Item1, "input", StringComparison.OrdinalIgnoreCase))
            tables.Add(("input", inputs[0].Frame));
        return tables;
    }
}
