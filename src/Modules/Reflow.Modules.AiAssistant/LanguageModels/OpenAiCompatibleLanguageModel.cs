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
    public async Task<string> CompleteJsonAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var settings = options.Value;
        var endpoint = $"{settings.BaseUrl.TrimEnd('/')}/chat/completions";
        var maxAttempts = Math.Max(0, settings.MaxTransientRetries) + 1;

        for (var attempt = 1; ; attempt++)
        {
            // HttpRequestMessage cannot be resent, so build one per attempt.
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            request.Content = JsonContent.Create(new
            {
                model = settings.Model,
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
