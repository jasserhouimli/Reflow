using System.Globalization;
using DuckDB.NET.Data;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.DuckDb;

public sealed class DuckDbSqlEngine : ISqlEngine
{
    private const int InsertBatchRows = 500;

    public Frame Query(IReadOnlyList<(string Name, Frame Frame)> tables, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("SQL query is required");
        if (tables.Count > 8)
            throw new InvalidOperationException("At most 8 input tables are allowed");

        try
        {
            // Materialize tables with inferred native types (BIGINT, DOUBLE, TIMESTAMP, VARCHAR)
            return ExecuteQuery(tables, sql, typed: true);
        }
        catch (DuckDBException ex) when (IsTypeConversionMismatch(ex))
        {
            // If a query explicitly compares numeric columns against non-numeric strings
            // (e.g. WHERE age = 'Ada'), fall back to untyped VARCHAR schema.
            return ExecuteQuery(tables, sql, typed: false);
        }
    }

    private static Frame ExecuteQuery(IReadOnlyList<(string Name, Frame Frame)> tables, string sql, bool typed)
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();

        using (var lockdown = connection.CreateCommand())
        {
            lockdown.CommandText = "SET enable_external_access=false;";
            lockdown.ExecuteNonQuery();
        }

        foreach (var (name, frame) in tables)
            LoadTable(connection, SanitizeName(name), frame, typed);

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

    private static bool IsTypeConversionMismatch(DuckDBException ex) =>
        ex.Message.Contains("Conversion Error", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Could not convert string", StringComparison.OrdinalIgnoreCase);

    private static void LoadTable(DuckDBConnection connection, string name, Frame frame, bool typed)
    {
        foreach (var column in frame.Columns)
            ValidateIdentifier(column);

        var columnTypes = typed ? InferColumnTypes(frame) : Enumerable.Repeat("VARCHAR", frame.Columns.Count).ToArray();

        using var create = connection.CreateCommand();
        var cols = string.Join(", ", frame.Columns.Select((c, i) => $"\"{c.Replace("\"", "\"\"")}\" {columnTypes[i]}"));
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
                    insert.Parameters.Add(new DuckDBParameter(ConvertValue(cell, columnTypes[c])));
                }
                sb.Append(')');
            }
            insert.CommandText = sb.ToString();
            insert.ExecuteNonQuery();
        }
    }

    private static IReadOnlyList<string> InferColumnTypes(Frame frame)
    {
        var types = new string[frame.Columns.Count];
        for (var col = 0; col < frame.Columns.Count; col++)
            types[col] = InferType(frame.Rows, col);
        return types;
    }

    private static string InferType(IReadOnlyList<List<string?>> rows, int colIndex)
    {
        var nonNullCount = 0;
        var isBool = true;
        var isLong = true;
        var isDouble = true;
        var isTimestamp = true;

        foreach (var row in rows)
        {
            if (colIndex >= row.Count)
                continue;

            var val = row[colIndex];
            if (string.IsNullOrWhiteSpace(val))
                continue;

            val = val.Trim();
            nonNullCount++;

            if (isBool && !bool.TryParse(val, out _))
                isBool = false;

            if (isLong && !long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                isLong = false;

            if (isDouble && !double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                isDouble = false;

            if (isTimestamp && !(LooksLikeDate(val) && DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                isTimestamp = false;

            if (!isBool && !isLong && !isDouble && !isTimestamp)
                return "VARCHAR";
        }

        if (nonNullCount == 0)
            return "VARCHAR";
        if (isBool)
            return "BOOLEAN";
        if (isLong)
            return "BIGINT";
        if (isDouble)
            return "DOUBLE";
        if (isTimestamp)
            return "TIMESTAMP";

        return "VARCHAR";
    }

    private static bool LooksLikeDate(string s) =>
        s.Length >= 8 && (s.Contains('-') || s.Contains('/') || s.Contains('T') || s.Contains(':'));

    private static object ConvertValue(string? cell, string duckDbType)
    {
        if (string.IsNullOrWhiteSpace(cell))
            return DBNull.Value;

        var trimmed = cell.Trim();
        return duckDbType switch
        {
            "BIGINT" when long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) => v,
            "DOUBLE" when double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) => v,
            "BOOLEAN" when bool.TryParse(trimmed, out var v) => v,
            "TIMESTAMP" when DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) => v,
            _ => duckDbType == "VARCHAR" ? cell : DBNull.Value,
        };
    }

    private static string SanitizeName(string name)
    {
        var clean = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        if (string.IsNullOrEmpty(clean))
            return "input";
        if (char.IsDigit(clean[0]))
            clean = "t_" + clean;
        return clean.ToLowerInvariant();
    }

    private static void ValidateIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Column name cannot be empty");
        if (name.Contains('\0'))
            throw new InvalidOperationException("Column name cannot contain null characters");
        if (name.Length > 128)
            throw new InvalidOperationException($"Column name '{name}' exceeds maximum length of 128 characters");
    }
}
