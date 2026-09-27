using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.DataQuality;

public sealed record QualityResult(
    int InputCount,
    int ValidCount,
    int RejectedCount,
    IReadOnlyDictionary<string, int> FailuresByRule);

/// <summary>
/// Machine-readable validation. Results feed run + dataset metadata (spec §29).
/// </summary>
public static class QualityEvaluator
{
    public static QualityResult Evaluate(
        Frame frame,
        IReadOnlyList<string> requiredColumns,
        IReadOnlyList<string> uniqueColumns)
    {
        var failures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rejected = 0;

        var required = requiredColumns.Select(c =>
        {
            var i = frame.ColumnIndex(c);
            if (i < 0)
                throw new InvalidOperationException($"Unknown column '{c}'");
            return (Name: c, Index: i);
        }).ToList();

        var unique = uniqueColumns.Select(c =>
        {
            var i = frame.ColumnIndex(c);
            if (i < 0)
                throw new InvalidOperationException($"Unknown column '{c}'");
            return (Name: c, Index: i);
        }).ToList();

        var seen = unique.ToDictionary(u => u.Name, _ => new HashSet<string?>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in frame.Rows)
        {
            var bad = false;
            foreach (var (name, i) in required)
            {
                var cell = i < row.Count ? row[i] : null;
                if (string.IsNullOrEmpty(cell))
                {
                    Bump(failures, $"required:{name}");
                    bad = true;
                }
            }
            foreach (var (name, i) in unique)
            {
                var cell = i < row.Count ? row[i] : null;
                if (!seen[name].Add(cell))
                {
                    Bump(failures, $"unique:{name}");
                    bad = true;
                }
            }
            if (bad)
                rejected++;
        }

        return new QualityResult(frame.RowCount, frame.RowCount - rejected, rejected, failures);
    }

    private static void Bump(Dictionary<string, int> failures, string rule) =>
        failures[rule] = failures.TryGetValue(rule, out var n) ? n + 1 : 1;
}
