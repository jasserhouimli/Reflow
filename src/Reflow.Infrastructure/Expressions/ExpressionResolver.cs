using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reflow.Infrastructure.Expressions;

/// <summary>
/// Values available to <c>{{ ... }}</c> expressions in node configs.
/// </summary>
/// <param name="TriggerKind">manual, schedule or webhook.</param>
/// <param name="TriggerName">Schedule/webhook name, if any.</param>
/// <param name="TriggerPayloadJson">Raw JSON payload delivered by the trigger, if any.</param>
/// <param name="RunId">Current run id.</param>
/// <param name="VersionNumber">Published workflow version being executed.</param>
public sealed record ExpressionContext(
    string TriggerKind,
    string? TriggerName,
    string? TriggerPayloadJson,
    Guid RunId,
    int VersionNumber);

/// <summary>
/// Resolves <c>{{ trigger.body.order.id }}</c> style expressions inside node
/// config strings. Supported roots: <c>trigger</c> (kind, name, body...) and
/// <c>run</c> (id, version). A string consisting of a single expression keeps
/// the value's JSON type (numbers stay numbers); expressions embedded in
/// larger text are stringified. <c>\{{</c> renders a literal <c>{{</c>.
/// </summary>
public static class ExpressionResolver
{
    private static readonly Regex Token = new(
        @"(?<!\\)\{\{\s*(.+?)\s*\}\}",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    public static bool ContainsExpressions(string? text) =>
        !string.IsNullOrEmpty(text) && Token.IsMatch(text);

    /// <summary>Resolves every string value in a config JSON document.</summary>
    /// <returns>(true, resolvedJson) or (false, error).</returns>
    public static (bool Ok, string ValueOrError) ResolveConfigJson(string? configJson, ExpressionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return (true, "{}");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(configJson);
        }
        catch (JsonException ex)
        {
            return (false, $"Invalid config JSON: {ex.Message}");
        }

        using (doc)
        {
            try
            {
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    WriteResolved(doc.RootElement, ctx, writer);
                }
                return (true, Encoding.UTF8.GetString(stream.ToArray()));
            }
            catch (ExpressionException ex)
            {
                return (false, ex.Message);
            }
        }
    }

    private static void WriteResolved(JsonElement element, ExpressionContext ctx, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    WriteResolved(prop.Value, ctx, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteResolved(item, ctx, writer);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                WriteResolvedString(element.GetString()!, ctx, writer);
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static void WriteResolvedString(string text, ExpressionContext ctx, Utf8JsonWriter writer)
    {
        var matches = Token.Matches(text);
        if (matches.Count == 0)
        {
            writer.WriteStringValue(Unescape(text));
            return;
        }

        // A lone expression preserves the value's JSON type.
        if (matches.Count == 1 && matches[0].Length == text.Trim().Length && text.Trim().StartsWith("{{"))
        {
            var value = Lookup(matches[0].Groups[1].Value, ctx);
            value.WriteTo(writer);
            return;
        }

        var sb = new StringBuilder(text);
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var value = Lookup(matches[i].Groups[1].Value, ctx);
            sb.Remove(matches[i].Index, matches[i].Length);
            sb.Insert(matches[i].Index, Stringify(value));
        }
        writer.WriteStringValue(Unescape(sb.ToString()));
    }

    private static JsonElement Lookup(string path, ExpressionContext ctx)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new ExpressionException("Empty expression {{ }}.");

        JsonElement body = default;
        var hasBody = false;

        JsonElement Body()
        {
            if (hasBody) return body;
            if (string.IsNullOrWhiteSpace(ctx.TriggerPayloadJson))
                throw new ExpressionException(
                    $"Expression '{{{{ {path} }}}}' needs a trigger payload, but this run was not started with one.");
            try
            {
                // Clone: the document is disposed at the end of this block.
                using var doc = JsonDocument.Parse(ctx.TriggerPayloadJson);
                body = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new ExpressionException("Trigger payload is not valid JSON.");
            }
            hasBody = true;
            return body;
        }

        switch (segments[0])
        {
            case "trigger":
                if (segments.Length < 2)
                    throw new ExpressionException("Expression '{{ trigger }}' needs a field: kind, name or body.");
                if (segments[1] == "kind") return ToElement(ctx.TriggerKind);
                if (segments[1] == "name") return ToElement(ctx.TriggerName ?? string.Empty);
                if (segments[1] == "body")
                {
                    var root = Body();
                    if (segments.Length == 2) return root;
                    return Navigate(root, segments[2..], path);
                }
                throw new ExpressionException(
                    $"Unknown trigger field '{segments[1]}'. Use trigger.kind, trigger.name or trigger.body.");
            case "run":
                if (segments.Length != 2)
                    throw new ExpressionException("Use run.id or run.version.");
                if (segments[1] == "id") return ToElement(ctx.RunId.ToString());
                if (segments[1] == "version") return ToElement(ctx.VersionNumber);
                throw new ExpressionException($"Unknown run field '{segments[1]}'. Use run.id or run.version.");
            default:
                throw new ExpressionException(
                    $"Unknown expression root '{segments[0]}'. Use trigger.* or run.*.");
        }
    }

    private static JsonElement Navigate(JsonElement root, string[] segments, string path)
    {
        var current = root;
        foreach (var segment in segments)
        {
            if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(segment, out var index) || index < 0 || index >= current.GetArrayLength())
                    throw new ExpressionException($"Expression '{{{{ {path} }}}}': array has no index '{segment}'.");
                current = current[index];
            }
            else if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out current))
                    throw new ExpressionException($"Expression '{{{{ {path} }}}}': object has no property '{segment}'.");
            }
            else
            {
                throw new ExpressionException($"Expression '{{{{ {path} }}}}': cannot descend into '{segment}'.");
            }
        }
        return current;
    }

    private static JsonElement ToElement(object? value) =>
        JsonSerializer.SerializeToElement(value);

    private static string Stringify(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => value.GetRawText()
    };

    private static string Unescape(string text) => text.Replace("\\{{", "{{");

    private sealed class ExpressionException(string message) : Exception(message);
}

/// <summary>Design-time syntax check for expressions (no values needed).</summary>
public static class ExpressionValidator
{
    private static readonly Regex AnyBraces = new(@"(?<!\\)\{\{", RegexOptions.Compiled);

    public static IEnumerable<string> ValidateConfig(string nodeId, string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            yield break;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(configJson);
        }
        catch (JsonException)
        {
            yield break; // Malformed JSON is reported by the per-type validator.
        }

        using (doc)
        {
            foreach (var text in Strings(doc.RootElement))
            {
                foreach (var error in ValidateText(nodeId, text))
                    yield return error;
            }
        }
    }

    private static IEnumerable<string> Strings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    foreach (var s in Strings(prop.Value))
                        yield return s;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    foreach (var s in Strings(item))
                        yield return s;
                break;
            case JsonValueKind.String:
                yield return element.GetString()!;
                break;
        }
    }

    private static IEnumerable<string> ValidateText(string nodeId, string text)
    {
        var matches = Token.Matches(text);
        foreach (Match m in matches)
        {
            var path = m.Groups[1].Value.Trim();
            var root = path.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(root))
                yield return $"Node '{nodeId}': empty expression {{{{ }}}} is not allowed.";
            else if (root != "trigger" && root != "run")
                yield return $"Node '{nodeId}': unknown expression root '{root}' in '{{{{ {path} }}}}'. Use trigger.* or run.*.";
        }

        // An opener that never matched a token is unbalanced (a stray closer
        // alone is harmless and stays literal).
        var stripped = Token.Replace(text, string.Empty).Replace("\\{{", string.Empty);
        if (AnyBraces.IsMatch(stripped))
            yield return $"Node '{nodeId}': unbalanced '{{{{ }}}}' in '{text}'. Escape a literal with \\{{{{.";
    }

    // Same pattern as the resolver; kept local so validation never depends on resolution.
    private static readonly Regex Token =
        new(@"(?<!\\)\{\{\s*(.+?)\s*\}\}", RegexOptions.Compiled);
}
