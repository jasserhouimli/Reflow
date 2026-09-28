using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Join;

/// <summary>Two-input join compiled to DuckDB SQL. Right-side duplicate
/// columns are suffixed _right so downstream nodes see unique names.</summary>
public sealed class JoinHandler : INodeHandler
{
    public const string NodeType = "join";

    private readonly ISqlEngine _sql;

    public JoinHandler(ISqlEngine sql) => _sql = sql;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "Join",
        "Transform",
        "Combine two tables on key columns.",
        new[] { "left", "right" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "join config must be an object" };

        var hasOn = config.TryGetProperty("on", out var on)
            && on.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(on.GetString());
        var hasPair = config.TryGetProperty("leftOn", out var lo)
            && lo.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(lo.GetString())
            && config.TryGetProperty("rightOn", out var ro)
            && ro.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(ro.GetString());
        if (!hasOn && !hasPair)
            errors.Add("join needs 'on' or both 'leftOn' and 'rightOn'");

        if (config.TryGetProperty("how", out var h)
            && (h.ValueKind != JsonValueKind.String
                || (h.GetString() is not "inner" and not "left")))
            errors.Add("join 'how' must be inner or left");
        return errors;
    }

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<NodeInput> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid join config: " + string.Join("; ", errors));
        if (inputs.Count != 2)
            throw new InvalidOperationException("join needs exactly two inputs");

        var left = inputs[0];
        var right = inputs[1];
        var how = config.TryGetProperty("how", out var h)
            && h.ValueKind == JsonValueKind.String
            && string.Equals(h.GetString(), "left", StringComparison.OrdinalIgnoreCase)
            ? "LEFT JOIN" : "JOIN";

        string leftKey, rightKey;
        if (config.TryGetProperty("on", out var on)
            && on.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(on.GetString()))
        {
            leftKey = rightKey = on.GetString()!;
        }
        else
        {
            leftKey = config.GetProperty("leftOn").GetString()!;
            rightKey = config.GetProperty("rightOn").GetString()!;
        }

        var leftCols = left.Frame.Columns;
        var rightSeen = new HashSet<string>(leftCols, StringComparer.OrdinalIgnoreCase);
        var projection = leftCols
            .Select(c => $"{SqlPredicate.Column("a")}.{SqlPredicate.Column(c)}")
            .ToList();
        foreach (var col in right.Frame.Columns)
        {
            if (string.Equals(col, rightKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase))
                continue;
            projection.Add(rightSeen.Add(col)
                ? $"{SqlPredicate.Column("b")}.{SqlPredicate.Column(col)}"
                : $"{SqlPredicate.Column("b")}.{SqlPredicate.Column(col)} AS {SqlPredicate.Column(col + "_right")}");
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var leftTable = NodeTableName.For(left.NodeId, used);
        var rightTable = NodeTableName.For(right.NodeId, used);

        var sql = $"SELECT {string.Join(", ", projection)} " +
            $"FROM \"{leftTable}\" a {how} \"{rightTable}\" b " +
            $"ON {SqlPredicate.Column("a")}.{SqlPredicate.Column(leftKey)} = {SqlPredicate.Column("b")}.{SqlPredicate.Column(rightKey)}";
        return Task.FromResult(_sql.Query(
            new[] { (leftTable, left.Frame), (rightTable, right.Frame) }, sql));
    }
}
