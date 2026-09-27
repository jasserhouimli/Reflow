using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.DataSql;

/// <summary>
/// Analytical SQL over upstream tables. With one input the table is `input`;
/// with several they are `input1..N`. DuckDB in-memory, no file access.
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
        "Run analytical SQL over upstream tables with DuckDB.",
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
        IReadOnlyList<Frame> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid data.sql config: " + string.Join("; ", errors));
        if (inputs.Count > 8)
            throw new InvalidOperationException("data.sql accepts at most 8 inputs");

        var tables = inputs.Select((f, i) => (
            Name: inputs.Count == 1 ? "input" : $"input{i + 1}", Frame: f)).ToList();
        return Task.FromResult(_sql.Query(tables, config.GetProperty("query").GetString()!));
    }
}
