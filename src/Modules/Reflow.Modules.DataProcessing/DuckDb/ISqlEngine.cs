using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.DuckDb;

// In-memory SQL execution over tabular frames.
public interface ISqlEngine
{
    Frame Query(IReadOnlyList<(string Name, Frame Frame)> tables, string sql);
}
