using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class AssistantTests
{
    private readonly ReflowApiFactory _factory;

    public AssistantTests(ReflowApiFactory factory) => _factory = factory;

    private const string SimpleDraft = """
        {
          "name": "Orders",
          "explanation": "Reads orders and writes an artifact",
          "nodes": [
            { "nodeId": "source", "nodeType": "csv.read",
              "config": { "csvText": "region,amount\nEU,120" } },
            { "nodeId": "output", "nodeType": "data.output",
              "config": { "format": "json" } }
          ],
          "edges": [ { "sourceNodeId": "source", "targetNodeId": "output" } ],
          "warnings": [ "Assumed comma delimiter" ]
        }
        """;

    private static async Task<Guid> UploadCsvAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/files", new
        {
            fileName = "orders.csv",
            content = "region,amount\nEU,120\nUS,80",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
        return body.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Generate_RequiresAuthentication()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = "read my csv" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("hi")]
    [InlineData("")]
    public async Task Generate_RejectsTooShortPrompts(string prompt)
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        _factory.LanguageModel.Reset();

        var res = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(0, _factory.LanguageModel.Calls);
    }

    [Fact]
    public async Task Generate_RejectsTooLongPrompts()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        _factory.LanguageModel.Reset();

        var res = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = new string('x', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(0, _factory.LanguageModel.Calls);
    }

    [Fact]
    public async Task Generate_ReturnsValidatedDraft()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        _factory.LanguageModel.Reset();
        _factory.LanguageModel.Returns(SimpleDraft);

        var res = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = "read orders and save json" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement;
        Assert.Equal("Orders", body.GetProperty("name").GetString());
        Assert.Equal(0, body.GetProperty("repairAttempts").GetInt32());

        var nodes = body.GetProperty("nodes");
        Assert.Equal(2, nodes.GetArrayLength());
        Assert.Equal("source", nodes[0].GetProperty("nodeId").GetString());
        Assert.Equal("csv.read", nodes[0].GetProperty("nodeType").GetString());
        Assert.Equal(JsonValueKind.Object, nodes[0].GetProperty("config").ValueKind);

        Assert.Equal(1, body.GetProperty("edges").GetArrayLength());
        Assert.Equal(1, body.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public async Task Generate_OnlyShowsTheCallerTheirOwnFiles()
    {
        var alice = await ApiHelpers.LoginNewUserAsync(_factory);
        var aliceFileId = await UploadCsvAsync(alice);

        var bob = await ApiHelpers.LoginNewUserAsync(_factory);
        _factory.LanguageModel.Reset();
        _factory.LanguageModel.Returns(SimpleDraft);

        var res = await bob.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = "use my file" });

        res.EnsureSuccessStatusCode();
        Assert.DoesNotContain(aliceFileId.ToString(), _factory.LanguageModel.LastSystemPrompt);
    }

    [Fact]
    public async Task Generate_ReferencesARealFileWithoutLeakingContent()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var fileId = await UploadCsvAsync(client);

        _factory.LanguageModel.Reset();
        _factory.LanguageModel.Returns($$"""
            {
              "name": "From file",
              "nodes": [
                { "nodeId": "orders", "nodeType": "file.read",
                  "config": { "fileId": "{{fileId}}", "format": "csv" } },
                { "nodeId": "output", "nodeType": "data.output", "config": {} }
              ],
              "edges": [ { "sourceNodeId": "orders", "targetNodeId": "output" } ]
            }
            """);

        var res = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = "read the uploaded orders" });

        res.EnsureSuccessStatusCode();
        Assert.Contains(fileId.ToString(), _factory.LanguageModel.LastSystemPrompt);
        Assert.Contains("orders.csv", _factory.LanguageModel.LastSystemPrompt);
        // Metadata only: the CSV body never reaches the model.
        Assert.DoesNotContain("US,80", _factory.LanguageModel.LastSystemPrompt);
    }

    [Fact]
    public async Task Draft_RoundTripsThroughThePipelineWritePath()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var fileId = await UploadCsvAsync(client);

        _factory.LanguageModel.Reset();
        _factory.LanguageModel.Returns($$"""
            {
              "name": "Orders by region",
              "explanation": "Reads the uploaded orders",
              "nodes": [
                { "nodeId": "orders", "nodeType": "file.read",
                  "config": { "fileId": "{{fileId}}", "format": "csv" } },
                { "nodeId": "output", "nodeType": "data.output",
                  "config": { "format": "json" } }
              ],
              "edges": [ { "sourceNodeId": "orders", "targetNodeId": "output" } ]
            }
            """);

        var generated = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = "read orders and save json" });
        generated.EnsureSuccessStatusCode();
        var draft = (await generated.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement;

        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "From AI" });
        var pipelineId = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/v1/pipelines/{pipelineId}", new
        {
            nodes = draft.GetProperty("nodes").EnumerateArray().Select((node, index) => new
            {
                nodeId = node.GetProperty("nodeId").GetString(),
                nodeType = node.GetProperty("nodeType").GetString(),
                configJson = node.GetProperty("config").GetRawText(),
                label = node.GetProperty("nodeId").GetString(),
                positionX = (double)index,
                positionY = 0.0,
            }),
            edges = draft.GetProperty("edges").EnumerateArray().Select(edge => new
            {
                sourceNodeId = edge.GetProperty("sourceNodeId").GetString(),
                targetNodeId = edge.GetProperty("targetNodeId").GetString(),
            }),
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var validate = await client.PostAsync($"/api/v1/pipelines/{pipelineId}/validate", null);
        var validation = (await validate.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.True(validation.RootElement.GetProperty("valid").GetBoolean());

        var get = await client.GetAsync($"/api/v1/pipelines/{pipelineId}");
        get.EnsureSuccessStatusCode();
        var saved = (await get.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement;
        Assert.Equal(2, saved.GetProperty("nodes").GetArrayLength());
        Assert.Contains(saved.GetProperty("nodes").EnumerateArray(),
            node => node.GetProperty("configJson").GetString()!.Contains(fileId.ToString()));
    }

    [Fact]
    public async Task Generate_MapsProviderRateLimit()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        _factory.LanguageModel.Reset();
        _factory.LanguageModel.Throws(429);

        var res = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = "read my csv" });

        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
    }

    [Fact]
    public async Task Generate_Returns422WhenTheModelNeverProducesAValidDraft()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        _factory.LanguageModel.Reset();
        // MaxRepairs defaults to 2: one attempt plus two repairs.
        for (var i = 0; i < 3; i++)
            _factory.LanguageModel.Returns("""{ "nodes": [] }""");

        var res = await client.PostAsJsonAsync(
            "/api/v1/assistant/workflows/generate", new { prompt = "read my csv" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement;
        Assert.Contains("could not produce a valid workflow", body.GetProperty("error").GetString());
        Assert.Equal(3, _factory.LanguageModel.Calls);
    }
}
