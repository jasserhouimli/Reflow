namespace Reflow.Modules.DataProcessing.Abstractions;

/// <summary>
/// Row operations over frames. In-memory now; DuckDB implements this later
/// without caller changes.
/// </summary>
public interface IDataQueryEngine
{
    Frame Filter(Frame input, string column, string op, string? value);
    Frame Select(Frame input, IReadOnlyList<string> columns);
    Frame Limit(Frame input, int count, int offset = 0);
}
