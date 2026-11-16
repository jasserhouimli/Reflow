namespace Reflow.Modules.AiAssistant.Abstractions;

public interface ILanguageModel
{
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct);

    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, string? apiKeyOverride, CancellationToken ct) =>
        CompleteJsonAsync(systemPrompt, userPrompt, ct);
}

public sealed class LanguageModelException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
