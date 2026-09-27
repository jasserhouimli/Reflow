using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.DuckDb;

/// <summary>
/// Analytical SQL over frames. DuckDB runs in-memory per call with external
/// file access disabled; callers never touch DuckDB types (swappable).
/// </summary>
public interface ISqlEngine
{
    Frame Query(IReadOnlyList<(string Name, Frame Frame)> tables, string sql);
}
