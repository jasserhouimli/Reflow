using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.InMemory;

/// <summary>Frame serializers with size caps.</summary>
public sealed class FrameWriter : IDataWriter
{
    public string ToJson(Frame frame)
    {
        var rows = frame.Rows.Select(r =>
            frame.Columns.Select((c, i) => new { c, v = i < r.Count ? r[i] : null })
                .ToDictionary(x => x.c, x => x.v));
        var json = JsonSerializer.Serialize(rows);
        if (json.Length > Frame.MaxStoredChars)
            throw new InvalidOperationException($"Output too large (max {Frame.MaxStoredChars} chars)");
        return json;
    }

    public string ToCsv(Frame frame, char delimiter = ',', bool includeHeader = true)
    {
        var sb = new System.Text.StringBuilder();
        if (includeHeader)
            sb.AppendLine(string.Join(delimiter, frame.Columns.Select(c => Escape(c, delimiter))));
        foreach (var row in frame.Rows)
        {
            sb.AppendLine(string.Join(delimiter,
                frame.Columns.Select((_, i) => Escape(i < row.Count ? row[i] : null, delimiter))));
        }
        var csv = sb.ToString();
        if (csv.Length > Frame.MaxStoredChars)
            throw new InvalidOperationException($"Output too large (max {Frame.MaxStoredChars} chars)");
        return csv;
    }

    private static string Escape(string? value, char delimiter)
    {
        if (value is null)
            return string.Empty;
        if (value.Contains('"') || value.Contains(delimiter) || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
