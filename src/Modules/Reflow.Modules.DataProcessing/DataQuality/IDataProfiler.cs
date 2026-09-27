using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.DataQuality;

public sealed record ColumnProfile(
    string Column,
    int Count,
    int NullCount,
    int DistinctCount,
    string? Min,
    string? Max,
    double? Mean);

/// <summary>Per-column stats; one row per column downstream.</summary>
public interface IDataProfiler
{
    IReadOnlyList<ColumnProfile> Profile(Frame frame, IReadOnlyList<string>? columns = null);
}
