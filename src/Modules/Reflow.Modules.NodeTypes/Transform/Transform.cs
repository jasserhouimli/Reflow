using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Transform;

/// <summary>
/// Reshape columns: select, drop, rename. Compiles to one DuckDB SELECT
/// (EXCLUDE / RENAME). Names refer to input columns.
/// </summary>
public sealed class TransformHandler : INodeHandler
{
    public const string NodeType = "transform";

    private readonly ISqlEngine _sql;

    public TransformHandler(ISqlEngine sql) => _sql = sql;

    public string Type => NodeType;

    public NodeDefinition Definition => new(
        NodeType,
        "Transform",
        "Transform",
        "Select, drop, or rename columns.",
        new[] { "input" },
        new[] { "output" });

    public IReadOnlyList<string> ValidateConfig(JsonElement config)
    {
        var errors = new List<string>();
        if (config.ValueKind != JsonValueKind.Object)
            return new[] { "transform config must be an object" };

        var hasSelect = config.TryGetProperty("select", out var s)
            && s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0;
        var hasDrop = config.TryGetProperty("dropColumns", out var d)
            && d.ValueKind == JsonValueKind.Array && d.GetArrayLength() > 0;
        var hasRenames = config.TryGetProperty("renames", out var r)
            && r.ValueKind == JsonValueKind.Object && r.EnumerateObject().Any();

        if (!hasSelect && !hasDrop && !hasRenames)
            errors.Add("transform needs at least one of 'select', 'dropColumns', 'renames'");
        return errors;
    }

    public Task<Frame> ExecuteAsync(
        IReadOnlyList<Frame> inputs,
        JsonElement config,
        CancellationToken ct)
    {
        var errors = ValidateConfig(config);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid transform config: " + string.Join("; ", errors));
        if (inputs.Count != 1)
            throw new InvalidOperationException("transform needs exactly one input");

        var frame = inputs[0];

        var select = config.TryGetProperty("select", out var s) && s.ValueKind == JsonValueKind.Array
            ? s.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList()
            : new List<string>();
        var drop = config.TryGetProperty("dropColumns", out var d) && d.ValueKind == JsonValueKind.Array
            ? d.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList()
            : new List<string>();
        var renames = config.TryGetProperty("renames", out var r) && r.ValueKind == JsonValueKind.Object
            ? r.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!,
                    StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string projection;
        if (select.Count > 0)
        {
            var dropSet = new HashSet<string>(drop, StringComparer.OrdinalIgnoreCase);
            var keep = select.Where(c => !dropSet.Contains(c)).ToList();
            if (keep.Count == 0)
                throw new InvalidOperationException("transform select leaves no columns");
            projection = string.Join(", ", keep.Select(c =>
                renames.TryGetValue(c, out var to) && !string.Equals(to, c, StringComparison.Ordinal)
                    ? $"{SqlPredicate.Column(c)} AS {SqlPredicate.Column(to)}"
                    : SqlPredicate.Column(c)));
        }
        else
        {
            projection = "*";
            if (drop.Count > 0)
                projection += $" EXCLUDE ({string.Join(", ", drop.Select(SqlPredicate.Column))})";
            if (renames.Count > 0)
                projection += $" RENAME ({string.Join(", ",
                    renames.Select(kv => $"{SqlPredicate.Column(kv.Key)} AS {SqlPredicate.Column(kv.Value)}"))})";
        }

        var sql = $"SELECT {projection} FROM input";
        return Task.FromResult(_sql.Query(new[] { ("input", frame) }, sql));
    }
}
