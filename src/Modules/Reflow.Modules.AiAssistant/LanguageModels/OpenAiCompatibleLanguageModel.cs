using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Reflow.Modules.AiAssistant.Abstractions;
using Reflow.Modules.AiAssistant.Options;

namespace Reflow.Modules.AiAssistant.LanguageModels;

/// <summary>
/// Works with any OpenAI-compatible chat completions endpoint (Groq, Gemini's
/// compatibility layer, OpenRouter, Ollama). Provider choice is configuration,
/// not code.
/// </summary>
public sealed class OpenAiCompatibleLanguageModel(
    HttpClient http,
    IOptions<AiOptions> options) : ILanguageModel
{
    public async Task<string> CompleteJsonAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var settings = options.Value;
        var endpoint = $"{settings.BaseUrl.TrimEnd('/')}/chat/completions";

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
        if (!response.IsSuccessStatusCode)
        {
            // Status only: provider bodies can echo request content.
            throw new LanguageModelException(
                (int)response.StatusCode, "The AI provider rejected the request.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
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
