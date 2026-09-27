using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.NodeTypes.Abstractions;

namespace Reflow.Modules.NodeTypes.Transform;

/// <summary>Reshape columns: select, drop, rename. Names refer to input columns.</summary>
public sealed class TransformHandler : INodeHandler
{
    public const string NodeType = "transform";

    private readonly IDataQueryEngine _engine;

    public TransformHandler(IDataQueryEngine engine) => _engine = engine;

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

        if (config.TryGetProperty("select", out var s) && s.ValueKind == JsonValueKind.Array)
        {
            var cols = s.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
            if (cols.Count > 0)
                frame = _engine.Select(frame, cols);
        }

        if (config.TryGetProperty("dropColumns", out var d) && d.ValueKind == JsonValueKind.Array)
        {
            var drop = new HashSet<string>(
                d.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!),
                StringComparer.OrdinalIgnoreCase);
            var keep = frame.Columns.Where(c => !drop.Contains(c)).ToList();
            frame = _engine.Select(frame, keep);
        }

        if (config.TryGetProperty("renames", out var r) && r.ValueKind == JsonValueKind.Object)
        {
            var map = r.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? p.Name,
                    StringComparer.OrdinalIgnoreCase);
            frame = new Frame
            {
                Columns = frame.Columns.Select(c => map.TryGetValue(c, out var to) ? to : c).ToList(),
                Rows = frame.Rows.Select(row => row.ToList()).ToList(),
            };
        }

        return Task.FromResult(frame);
    }
}
