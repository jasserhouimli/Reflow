namespace Reflow.Modules.DataProcessing.Datasets;

/// <summary>
/// Dataset maturity layer. Tags datasets, never execution state (spec §12).
/// Bronze = raw/source-close; Silver = cleaned/standardized; Gold = analytics-ready.
/// </summary>
public enum DatasetLayer
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
}

public sealed record DatasetMetadata(
    Guid Id,
    string Name,
    DatasetLayer Layer,
    string Format,
    IReadOnlyList<string> Columns,
    int RowCount,
    long SizeBytes,
    string Location,
    DateTime CreatedAt,
    Guid? ProducingRunId,
    string? QualitySummary);
