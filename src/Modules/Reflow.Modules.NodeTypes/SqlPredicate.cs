namespace Reflow.Modules.NodeTypes;

/// <summary>
/// Operator → DuckDB SQL predicate. Identifiers quoted, literals escaped;
/// LIKE wildcards in user values are escaped. Shared by SQL-compiling slices.
/// </summary>
public static class SqlPredicate
{
    public static string Column(string name) =>
        "\"" + name.Replace("\"", "\"\"") + "\"";

    public static string Literal(string? value) =>
        value is null ? "NULL" : "'" + value.Replace("'", "''") + "'";

    private static string LikeEscape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    public static string Build(string column, string op, string? value)
    {
        var c = Column(column);
        return op switch
        {
            "equals" => $"{c} IS NOT DISTINCT FROM {Literal(value)}",
            "notEquals" => $"{c} IS DISTINCT FROM {Literal(value)}",
            "contains" => $"{c} ILIKE '%' || {Literal(LikeEscape(value ?? string.Empty))} || '%' ESCAPE '\\'",
            "startsWith" => $"{c} ILIKE {Literal(LikeEscape(value ?? string.Empty))} || '%' ESCAPE '\\'",
            "greaterThan" => Compare(c, value, ">"),
            "lessThan" => Compare(c, value, "<"),
            "isEmpty" => $"({c} IS NULL OR {c} = '')",
            "isNotEmpty" => $"({c} IS NOT NULL AND {c} <> '')",
            _ => throw new InvalidOperationException($"Unknown filter op '{op}'"),
        };
    }

    private static string Compare(string column, string? value, string sqlOp) =>
        double.TryParse(value, out _)
            ? $"TRY_CAST({column} AS DOUBLE) {sqlOp} TRY_CAST({Literal(value)} AS DOUBLE)"
            : $"{column} {sqlOp} {Literal(value)}";
}
