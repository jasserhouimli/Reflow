using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Reflow.Modules.AiAssistant.Abstractions;
using Reflow.Modules.AiAssistant.LanguageModels;
using Reflow.Modules.AiAssistant.Options;
using Xunit;

namespace Reflow.UnitTests;

public class OpenAiCompatibleLanguageModelTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static OpenAiCompatibleLanguageModel Create(
        StubHandler handler, string baseUrl = "https://api.example.com/openai/v1")
    {
        var options = Options.Create(new AiOptions
        {
            BaseUrl = baseUrl,
            Model = "test-model",
            ApiKey = "secret-key",
            MaxOutputTokens = 321,
            RetryDelayMilliseconds = 0,
        });
        return new OpenAiCompatibleLanguageModel(
            new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) }, options);
    }

    [Fact]
    public async Task SendsOpenAiCompatibleJsonModeRequest()
    {
        var handler = new StubHandler(_ => JsonResponse("""
            { "choices": [ { "message": { "content": "{\"nodes\":[]}" } } ] }
            """));
        var model = Create(handler);

        var content = await model.CompleteJsonAsync("system", "user", default);

        Assert.Equal("{\"nodes\":[]}", content);
        Assert.Equal("https://api.example.com/openai/v1/chat/completions",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-key", handler.Request.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal(321, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task NormalizesTrailingSlashInBaseUrl()
    {
        var handler = new StubHandler(_ => JsonResponse("""
            { "choices": [ { "message": { "content": "{}" } } ] }
            """));
        var model = Create(handler, "https://api.example.com/v1/");

        await model.CompleteJsonAsync("s", "u", default);

        Assert.Equal("https://api.example.com/v1/chat/completions",
            handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task PropagatesProviderStatusCode()
    {
        var handler = new StubHandler(_ => JsonResponse(
            """{ "error": { "message": "rate limited" } }""", HttpStatusCode.TooManyRequests));
        var model = Create(handler);

        var ex = await Assert.ThrowsAsync<LanguageModelException>(
            () => model.CompleteJsonAsync("s", "u", default));

        Assert.Equal(429, ex.StatusCode);
        // Provider bodies can echo prompt content; only the status is kept.
        Assert.DoesNotContain("rate limited", ex.Message);
    }

    [Theory]
    [InlineData("""{ "choices": [] }""")]
    [InlineData("""{ "other": 1 }""")]
    [InlineData("""{ "choices": [ { "message": { "content": "" } } ] }""")]
    public async Task RejectsIncompleteCompletions(string body)
    {
        var handler = new StubHandler(_ => JsonResponse(body));
        var model = Create(handler);

        var ex = await Assert.ThrowsAsync<LanguageModelException>(
            () => model.CompleteJsonAsync("s", "u", default));

        Assert.Equal(502, ex.StatusCode);
    }

    [Fact]
    public async Task RetriesOnceWhenProviderIsTemporarilyUnavailable()
    {
        var calls = 0;
        var handler = new StubHandler(_ => ++calls == 1
            ? JsonResponse("""{ "error": { "message": "high demand" } }""", HttpStatusCode.ServiceUnavailable)
            : JsonResponse("""{ "choices": [ { "message": { "content": "{}" } } ] }"""));
        var model = Create(handler);

        var content = await model.CompleteJsonAsync("s", "u", default);

        Assert.Equal("{}", content);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GivesUpAfterBoundedTransientRetries()
    {
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            return JsonResponse("""{ "error": { "message": "high demand" } }""",
                HttpStatusCode.ServiceUnavailable);
        });
        var model = Create(handler);

        var ex = await Assert.ThrowsAsync<LanguageModelException>(
            () => model.CompleteJsonAsync("s", "u", default));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal(2, calls); // first attempt plus one retry
    }

    [Fact]
    public async Task DoesNotRetryErrorsThatCannotRecover()
    {
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            calls++;
            return JsonResponse("""{ "error": { "message": "bad request" } }""",
                HttpStatusCode.BadRequest);
        });
        var model = Create(handler);

        await Assert.ThrowsAsync<LanguageModelException>(
            () => model.CompleteJsonAsync("s", "u", default));

        Assert.Equal(1, calls);
    }
}
