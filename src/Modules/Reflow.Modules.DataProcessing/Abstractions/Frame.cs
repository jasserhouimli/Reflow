namespace Reflow.Modules.DataProcessing.Abstractions;

/// <summary>
/// In-memory tabular frame. All cells are strings or null; analytical compute
/// runs in DuckDB behind ISqlEngine; ingestion codecs produce frames.
/// </summary>
public sealed class Frame
{
    public const int MaxRows = 50_000;
    public const int MaxStoredChars = 500_000;

    public List<string> Columns { get; init; } = new();
    public List<List<string?>> Rows { get; init; } = new();

    public Frame()
    {
    }

    public Frame(IEnumerable<string> columns, IEnumerable<List<string?>> rows)
    {
        Columns = new List<string>(columns);
        Rows = new List<List<string?>>(rows);
    }

    public static Frame Empty => new();

    public int RowCount => Rows.Count;
    public int ColumnCount => Columns.Count;

    public int ColumnIndex(string column) =>
        Columns.FindIndex(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase));

    public bool HasColumn(string column) => ColumnIndex(column) >= 0;
}
