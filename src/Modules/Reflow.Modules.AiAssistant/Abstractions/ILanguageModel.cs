namespace Reflow.Modules.AiAssistant.Abstractions;

public interface ILanguageModel
{
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

public sealed class LanguageModelException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
