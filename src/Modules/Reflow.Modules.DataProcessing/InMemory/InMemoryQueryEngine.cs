using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.InMemory;

/// <summary>In-memory filter/project/limit. No SQL, no I/O.</summary>
public sealed class InMemoryQueryEngine : IDataQueryEngine
{
    public Frame Filter(Frame input, string column, string op, string? value)
    {
        var idx = input.ColumnIndex(column);
        if (idx < 0)
            throw new InvalidOperationException($"Unknown column '{column}'");

        var rows = input.Rows.Where(r => Match(idx < r.Count ? r[idx] : null, op, value)).ToList();
        return new Frame { Columns = new List<string>(input.Columns), Rows = rows };
    }

    public Frame Select(Frame input, IReadOnlyList<string> columns)
    {
        var indexes = columns.Select(c =>
        {
            var i = input.ColumnIndex(c);
            if (i < 0)
                throw new InvalidOperationException($"Unknown column '{c}'");
            return i;
        }).ToList();

        return new Frame
        {
            Columns = columns.ToList(),
            Rows = input.Rows.Select(r => indexes.Select(i => i < r.Count ? r[i] : null).ToList()).ToList(),
        };
    }

    public Frame Limit(Frame input, int count, int offset = 0)
    {
        if (count < 0 || offset < 0)
            throw new InvalidOperationException("count/offset must be >= 0");
        return new Frame
        {
            Columns = new List<string>(input.Columns),
            Rows = input.Rows.Skip(offset).Take(count).ToList(),
        };
    }

    private static bool Match(string? cell, string op, string? value) => op switch
    {
        "equals" => string.Equals(cell, value, StringComparison.Ordinal),
        "notEquals" => !string.Equals(cell, value, StringComparison.Ordinal),
        "contains" => cell is not null && value is not null && cell.Contains(value, StringComparison.OrdinalIgnoreCase),
        "startsWith" => cell is not null && value is not null && cell.StartsWith(value, StringComparison.OrdinalIgnoreCase),
        "greaterThan" => Compare(cell, value) > 0,
        "lessThan" => Compare(cell, value) < 0,
        "isEmpty" => string.IsNullOrEmpty(cell),
        "isNotEmpty" => !string.IsNullOrEmpty(cell),
        _ => throw new InvalidOperationException($"Unknown filter op '{op}'"),
    };

    private static int Compare(string? cell, string? value)
    {
        if (cell is null || value is null)
            return -1;
        if (double.TryParse(cell, out var a) && double.TryParse(value, out var b))
            return a.CompareTo(b);
        return string.Compare(cell, value, StringComparison.OrdinalIgnoreCase);
    }
}
