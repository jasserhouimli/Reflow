namespace Reflow.Modules.AiAssistant.Abstractions;

/// <summary>
/// Single capability the assistant needs from a model: return JSON text for a
/// prompt. No tools, no function calling, no autonomous execution.
/// </summary>
public interface ILanguageModel
{
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

public sealed class LanguageModelException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
