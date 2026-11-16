using System.Text.RegularExpressions;
using DuckDB.NET.Data;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.DuckDb;

public sealed class DuckDbSqlEngine : ISqlEngine
{
    private const int InsertBatchRows = 500;

    private static readonly Regex UncastAggregationRegex = new(
        @"\b(?<fn>sum|avg)\s*\(\s*(?!(?:cast|try_cast)\b)(?:(?<dist>distinct)\s+)?(?<col>[a-zA-Z_][a-zA-Z0-9_\.]*)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    public Frame Query(IReadOnlyList<(string Name, Frame Frame)> tables, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("SQL query is required");
        if (tables.Count > 8)
            throw new InvalidOperationException("At most 8 input tables are allowed");

        try
        {
            return ExecuteQuery(tables, sql);
        }
        catch (Exception ex) when (IsVarcharAggregationError(ex) && TryAutoCastVarcharAggregations(sql, out var rewrittenSql))
        {
            return ExecuteQuery(tables, rewrittenSql);
        }
    }

    private static Frame ExecuteQuery(IReadOnlyList<(string Name, Frame Frame)> tables, string sql)
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();

        using (var lockdown = connection.CreateCommand())
        {
            lockdown.CommandText = "SET enable_external_access=false;";
            lockdown.ExecuteNonQuery();
        }

        foreach (var (name, frame) in tables)
            LoadTable(connection, SanitizeName(name), frame);

        using var query = connection.CreateCommand();
        query.CommandText = sql;
        using var reader = query.ExecuteReader();

        var result = new Frame();
        for (var i = 0; i < reader.FieldCount; i++)
            result.Columns.Add(reader.GetName(i));

        while (reader.Read())
        {
            var row = new List<string?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString());
            result.Rows.Add(row);
            if (result.Rows.Count > Frame.MaxRows)
                throw new InvalidOperationException($"Query returned too many rows (max {Frame.MaxRows})");
        }
        return result;
    }

    private static bool IsVarcharAggregationError(Exception ex) =>
        ex.Message.Contains("No function matches the given name and argument types", StringComparison.OrdinalIgnoreCase)
        && (ex.Message.Contains("sum(VARCHAR)", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("avg(VARCHAR)", StringComparison.OrdinalIgnoreCase));

    private static bool TryAutoCastVarcharAggregations(string sql, out string rewrittenSql)
    {
        if (!UncastAggregationRegex.IsMatch(sql))
        {
            rewrittenSql = sql;
            return false;
        }

        rewrittenSql = UncastAggregationRegex.Replace(sql, match =>
        {
            var fn = match.Groups["fn"].Value;
            var dist = match.Groups["dist"].Success ? "DISTINCT " : "";
            var col = match.Groups["col"].Value;
            return $"{fn}({dist}TRY_CAST({col} AS DOUBLE))";
        });

        return !string.Equals(sql, rewrittenSql, StringComparison.Ordinal);
    }

    private static void LoadTable(DuckDBConnection connection, string name, Frame frame)
    {
        foreach (var column in frame.Columns)
            ValidateIdentifier(column);

        using var create = connection.CreateCommand();
        var cols = string.Join(", ", frame.Columns.Select(c => $"\"{c.Replace("\"", "\"\"")}\" VARCHAR"));
        create.CommandText = $"CREATE TABLE \"{name}\" ({cols})";
        create.ExecuteNonQuery();

        if (frame.Rows.Count > Frame.MaxRows)
            throw new InvalidOperationException($"Input has too many rows (max {Frame.MaxRows})");

        for (var batch = 0; batch * InsertBatchRows < frame.Rows.Count; batch++)
        {
            var rows = frame.Rows.Skip(batch * InsertBatchRows).Take(InsertBatchRows).ToList();
            using var insert = connection.CreateCommand();
            var sb = new System.Text.StringBuilder($"INSERT INTO \"{name}\" VALUES ");
            for (var r = 0; r < rows.Count; r++)
            {
                if (r > 0)
                    sb.Append(", ");
                sb.Append('(');
                for (var c = 0; c < frame.Columns.Count; c++)
                {
                    if (c > 0)
                        sb.Append(", ");
                    sb.Append('?');
                    var cell = c < rows[r].Count ? rows[r][c] : null;
                    insert.Parameters.Add(new DuckDBParameter((object?)cell ?? DBNull.Value));
                }
                sb.Append(')');
            }
            insert.CommandText = sb.ToString();
            insert.ExecuteNonQuery();
        }
    }

    private static string SanitizeName(string name)
    {
        var clean = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        if (string.IsNullOrEmpty(clean))
            throw new InvalidOperationException("Table name is required");
        if (char.IsDigit(clean[0]))
            clean = "t_" + clean;
        return clean;
    }

    private static void ValidateIdentifier(string column)
    {
        if (string.IsNullOrWhiteSpace(column))
            throw new InvalidOperationException("Column names must not be empty");
    }
}
