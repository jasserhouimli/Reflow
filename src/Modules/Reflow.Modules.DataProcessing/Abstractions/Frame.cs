namespace Reflow.Modules.DataProcessing.Abstractions;

/// <summary>
/// In-memory tabular frame. All cells are strings or null; analytical compute
/// runs in DuckDB behind ISqlEngine; ingestion codecs produce frames.
/// </summary>
public sealed class Frame
{
    public const int MaxRows = 50000;
    public const int MaxStoredChars = 500000;

    public List<string> Columns { get; init; } = new();
    public List<List<string?>> Rows { get; init; } = new();

    public int RowCount => Rows.Count;

    public int ColumnIndex(string column) =>
        Columns.FindIndex(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase));
}
