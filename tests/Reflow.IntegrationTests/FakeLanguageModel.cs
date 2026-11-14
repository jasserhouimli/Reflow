using Reflow.Modules.AiAssistant.Abstractions;

namespace Reflow.IntegrationTests;

/// <summary>
/// Scripted provider: the API is exercised for real, only the outbound model
/// call is replaced. Each test resets and queues its own responses.
/// </summary>
public sealed class FakeLanguageModel : ILanguageModel
{
    private readonly Queue<Func<string>> _responses = new();
    private readonly object _gate = new();

    public int Calls { get; private set; }
    public string? LastSystemPrompt { get; private set; }
    public string? LastUserPrompt { get; private set; }

    public void Reset()
    {
        lock (_gate)
        {
            _responses.Clear();
            Calls = 0;
            LastSystemPrompt = null;
            LastUserPrompt = null;
        }
    }

    public void Returns(string json)
    {
        lock (_gate)
            _responses.Enqueue(() => json);
    }

    public void Throws(int statusCode)
    {
        lock (_gate)
            _responses.Enqueue(() => throw new LanguageModelException(statusCode, "scripted failure"));
    }

    public Task<string> CompleteJsonAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Calls++;
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            if (_responses.Count == 0)
                throw new InvalidOperationException("the test queued no model response");
            return Task.FromResult(_responses.Dequeue()());
        }
    }
}
