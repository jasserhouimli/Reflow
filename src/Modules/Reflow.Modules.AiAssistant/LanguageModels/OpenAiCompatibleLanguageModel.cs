using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Reflow.Modules.AiAssistant.Abstractions;
using Reflow.Modules.AiAssistant.Options;

namespace Reflow.Modules.AiAssistant.LanguageModels;

// Compatible with OpenAI-format completions endpoints (Groq, OpenRouter, local Ollama, etc.).
public sealed class OpenAiCompatibleLanguageModel(
    HttpClient http,
    IOptions<AiOptions> options) : ILanguageModel
{
    public Task<string> CompleteJsonAsync(
        string systemPrompt, string userPrompt, CancellationToken ct) =>
        CompleteJsonAsync(systemPrompt, userPrompt, null, ct);

    public async Task<string> CompleteJsonAsync(
        string systemPrompt, string userPrompt, string? apiKeyOverride, CancellationToken ct)
    {
        var settings = options.Value;
        var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKeyOverride) ? apiKeyOverride.Trim() : settings.ApiKey;

        if (effectiveApiKey == "dev-mock-key" || effectiveApiKey == "mock")
        {
            await Task.Delay(400, ct);
            return GenerateLocalMockResponse(userPrompt);
        }

        // Auto-detect Google Gemini API keys (starts with AIzaSy) and route to Gemini OpenAI endpoint
        var isGemini = effectiveApiKey.StartsWith("AIzaSy");
        var baseUrl = isGemini && (settings.BaseUrl.Contains("groq.com") || string.IsNullOrWhiteSpace(settings.BaseUrl))
            ? "https://generativelanguage.googleapis.com/v1beta/openai"
            : settings.BaseUrl;
        var model = isGemini && (settings.Model.Contains("llama") || string.IsNullOrWhiteSpace(settings.Model))
            ? "gemini-2.5-flash"
            : settings.Model;

        var endpoint = $"{baseUrl.TrimEnd('/')}/chat/completions";
        var maxAttempts = Math.Max(0, settings.MaxTransientRetries) + 1;

        for (var attempt = 1; ; attempt++)
        {
            // HttpRequestMessage cannot be resent, so build one per attempt.
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey);
            request.Content = JsonContent.Create(new
            {
                model = model,
                temperature = 0.1,
                max_tokens = settings.MaxOutputTokens,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt },
                },
            });

            using var response = await http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return ParseCompletion(await response.Content.ReadAsStringAsync(ct));

            // 503 is the provider saying "not now", not "never". Retry a bounded
            // number of times before surfacing the failure.
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable && attempt < maxAttempts)
            {
                await Task.Delay(settings.RetryDelayMilliseconds, ct);
                continue;
            }

            // Status only: provider bodies can echo request content.
            throw new LanguageModelException(
                (int)response.StatusCode, "The AI provider rejected the request.");
        }
    }

    private static string GenerateLocalMockResponse(string userPrompt)
    {
        var p = userPrompt.ToLowerInvariant();
        if (p.Contains("join") || p.Contains("customer"))
        {
            return """
            {
              "name": "Customer Orders Join",
              "explanation": "Loads customer data, reads orders, joins on customer_id, and calculates total purchases",
              "nodes": [
                { "nodeId": "customers", "nodeType": "json.read", "label": "Customers JSON", "config": { "jsonText": "[{\"customer_id\": 1, \"name\": \"Acme Corp\", \"tier\": \"enterprise\"}, {\"customer_id\": 2, \"name\": \"Globex\", \"tier\": \"standard\"}]" } },
                { "nodeId": "orders", "nodeType": "csv.read", "label": "Orders CSV", "config": { "csvText": "order_id,customer_id,amount\n101,1,250.00\n102,1,450.00\n103,2,120.00" } },
                { "nodeId": "join_report", "nodeType": "data.sql", "label": "Customer Spend Summary", "config": { "query": "SELECT c.name, c.tier, SUM(CAST(o.amount AS DOUBLE)) AS total_spent FROM customers c JOIN orders o ON c.customer_id = o.customer_id GROUP BY c.name, c.tier ORDER BY total_spent DESC" } },
                { "nodeId": "output", "nodeType": "data.output", "label": "Output Artifact", "config": { "format": "json" } }
              ],
              "edges": [
                { "sourceNodeId": "customers", "targetNodeId": "join_report" },
                { "sourceNodeId": "orders", "targetNodeId": "join_report" },
                { "sourceNodeId": "join_report", "targetNodeId": "output" }
              ],
              "warnings": ["Joined customers and orders on customer_id using DuckDB relational engine"]
            }
            """;
        }

        if (p.Contains("webhook"))
        {
            return """
            {
              "name": "Webhook Ingestion Flow",
              "explanation": "Receives inbound webhook events, filters valid records, and writes to an artifact sink",
              "nodes": [
                { "nodeId": "webhook_event", "nodeType": "trigger.payload", "label": "Webhook Inbound Payload", "config": {} },
                { "nodeId": "clean_event", "nodeType": "data.sql", "label": "Filter & Normalize Payload", "config": { "query": "SELECT event_id, event_type, payload FROM webhook_event WHERE event_type IS NOT NULL" } },
                { "nodeId": "output_sink", "nodeType": "data.output", "label": "Store Normalized Events", "config": { "format": "json" } }
              ],
              "edges": [
                { "sourceNodeId": "webhook_event", "targetNodeId": "clean_event" },
                { "sourceNodeId": "clean_event", "targetNodeId": "output_sink" }
              ],
              "warnings": ["Configured trigger payload extraction for incoming webhook POST requests"]
            }
            """;
        }

        return """
        {
          "name": "Sales & Revenue Analysis",
          "explanation": "Reads order transactions, filters regions, and calculates aggregated revenue per country",
          "nodes": [
            { "nodeId": "orders", "nodeType": "csv.read", "label": "Orders CSV", "config": { "csvText": "country,amount,date\nFR,120,2026-01-01\nDE,35,2026-01-02\nFR,200,2026-01-03\nUS,50,2026-01-03\nUK,180,2026-01-04" } },
            { "nodeId": "revenue_calc", "nodeType": "data.sql", "label": "Country Revenue Aggregation", "config": { "query": "SELECT country, COUNT(*) as order_count, SUM(CAST(amount AS DOUBLE)) AS total_revenue FROM orders GROUP BY country ORDER BY total_revenue DESC" } },
            { "nodeId": "revenue_report", "nodeType": "data.output", "label": "Revenue Output", "config": { "format": "csv" } }
          ],
          "edges": [
            { "sourceNodeId": "orders", "targetNodeId": "revenue_calc" },
            { "sourceNodeId": "revenue_calc", "targetNodeId": "revenue_report" }
          ],
          "warnings": ["Assumed standard comma-separated numeric amount column"]
        }
        """;
    }

    private static string ParseCompletion(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            throw new LanguageModelException(502, "The AI provider returned no choices.");
        }

        var content = choices[0]
            .TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var text)
            && text.ValueKind == JsonValueKind.String
                ? text.GetString()
                : null;

        if (string.IsNullOrWhiteSpace(content))
            throw new LanguageModelException(502, "The AI provider returned an empty completion.");

        return content;
    }
}
