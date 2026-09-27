using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.InMemory;

/// <summary>Quote-aware CSV codec with row caps. No workflow knowledge.</summary>
public sealed class CsvCodec : IDataReader
{
    public Frame FromCsv(string text, char delimiter = ',', bool hasHeader = true)
    {
        var rows = SplitRows(text, delimiter);
        var columns = new List<string>();
        var data = new List<List<string?>>();

        int start = 0;
        if (hasHeader)
        {
            if (rows.Count == 0)
                return new Frame();
            columns.AddRange(rows[0].Select(c => c ?? string.Empty));
            EnsureUnique(columns);
            start = 1;
        }
        else if (rows.Count > 0)
        {
            for (var i = 0; i < rows[0].Count; i++)
                columns.Add($"col{i + 1}");
        }

        foreach (var row in rows.Skip(start))
        {
            if (row.All(string.IsNullOrEmpty))
                continue;
            var padded = row.Concat(Enumerable.Repeat<string?>(null, Math.Max(0, columns.Count - row.Count)))
                .Take(columns.Count).ToList();
            data.Add(padded);
            if (data.Count > Frame.MaxRows)
                throw new InvalidOperationException($"CSV has too many rows (max {Frame.MaxRows})");
        }

        return new Frame { Columns = columns, Rows = data };
    }

    public Frame FromJson(string json, string? rootPath = null) =>
        JsonCodec.FromJson(json, rootPath);

    private static void EnsureUnique(List<string> columns)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columns)
        {
            if (!seen.Add(col))
                throw new InvalidOperationException($"Duplicate column '{col}'");
        }
    }

    private static List<List<string?>> SplitRows(string text, char delimiter)
    {
        var rows = new List<List<string?>>();
        if (string.IsNullOrEmpty(text))
            return rows;

        var current = new List<string?>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                current.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r')
            {
                continue;
            }
            else if (ch == '\n')
            {
                current.Add(field.ToString());
                field.Clear();
                rows.Add(current);
                current = new List<string?>();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (inQuotes)
            throw new InvalidOperationException("Unterminated quoted field in CSV");

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString());
            rows.Add(current);
        }

        return rows;
    }
}
