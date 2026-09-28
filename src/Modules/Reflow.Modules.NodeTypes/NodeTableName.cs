namespace Reflow.Modules.NodeTypes;

/// <summary>Source node id → DuckDB table name. Lowercased, sanitized, deduped.</summary>
public static class NodeTableName
{
    public static string For(string nodeId, HashSet<string>? used = null)
    {
        var clean = new string(nodeId
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray()).Trim('_');
        if (string.IsNullOrEmpty(clean))
            clean = "input";
        if (char.IsDigit(clean[0]))
            clean = "t_" + clean;
        if (used is null)
            return clean;
        var name = clean;
        for (var i = 2; used.Contains(name); i++)
            name = $"{clean}_{i}";
        used.Add(name);
        return name;
    }
}
