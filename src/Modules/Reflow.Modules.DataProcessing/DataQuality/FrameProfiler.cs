using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.DataQuality;

public sealed class FrameProfiler : IDataProfiler
{
    public IReadOnlyList<ColumnProfile> Profile(Frame frame, IReadOnlyList<string>? columns = null)
    {
        var targets = columns is null || columns.Count == 0
            ? frame.Columns.ToList()
            : columns.ToList();

        var result = new List<ColumnProfile>();
        foreach (var col in targets)
        {
            var idx = frame.ColumnIndex(col);
            if (idx < 0)
                throw new InvalidOperationException($"Unknown column '{col}'");

            var cells = frame.Rows.Select(r => idx < r.Count ? r[idx] : null).ToList();
            var nonNull = cells.Where(c => c is not null).Cast<string>().ToList();
            var numbers = nonNull
                .Select(c => double.TryParse(c, out var d) ? d : (double?)null)
                .Where(d => d.HasValue).Select(d => d!.Value).ToList();

            result.Add(new ColumnProfile(
                col,
                cells.Count,
                cells.Count - nonNull.Count,
                new HashSet<string>(nonNull).Count,
                nonNull.Count == 0 ? null : nonNull.Min(),
                nonNull.Count == 0 ? null : nonNull.Max(),
                numbers.Count == 0 ? null : numbers.Average()));
        }
        return result;
    }
}
