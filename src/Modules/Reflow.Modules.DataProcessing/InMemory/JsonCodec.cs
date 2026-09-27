using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;

namespace Reflow.Modules.DataProcessing.InMemory;

/// <summary>
/// Flattens JSON objects/arrays into string-cell frames. Scalars stringify;
/// nested values serialize back to compact JSON.
/// </summary>
public static class JsonCodec
{
    public static Frame FromJson(string json, string? rootPath)
    {
        using var doc = JsonDocument.Parse(json);
        var target = Navigate(doc.RootElement, rootPath);

        if (target.ValueKind == JsonValueKind.Array)
        {
            var frame = new Frame();
            var count = 0;
            foreach (var el in target.EnumerateArray())
            {
                AddElement(frame, el);
                if (++count > Frame.MaxRows)
                    throw new InvalidOperationException($"JSON has too many records (max {Frame.MaxRows})");
            }
            return frame;
        }

        var single = new Frame();
        AddElement(single, target);
        return single;
    }

    private static JsonElement Navigate(JsonElement root, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return root;

        var current = root;
        foreach (var part in rootPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(part.Trim('[', ']'), out var idx))
            {
                current = current.EnumerateArray().ElementAt(idx);
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                throw new InvalidOperationException($"JSON path '{rootPath}' not found at '{part}'");
            }
        }
        return current;
    }

    private static void AddElement(Frame frame, JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JSON array items must be objects");

        foreach (var prop in el.EnumerateObject())
        {
            if (!frame.Columns.Contains(prop.Name))
                frame.Columns.Add(prop.Name);
        }

        var row = frame.Columns.Select(c =>
            el.TryGetProperty(c, out var v) ? Scalar(v) : null).ToList();
        frame.Rows.Add(row);
    }

    private static string? Scalar(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => el.GetRawText(),
    };
}
