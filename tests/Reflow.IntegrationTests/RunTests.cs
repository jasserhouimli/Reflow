using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class RunTests
{
    private readonly ReflowApiFactory _factory;

    public RunTests(ReflowApiFactory factory) => _factory = factory;

    private async Task<Guid> CreatePublishedPipelineAsync(HttpClient client, string name)
    {
        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/v1/pipelines/{id}", new
        {
            nodes = new[]
            {
                new
                {
                    nodeId = "src",
                    nodeType = "csv.read",
                    configJson = "{\"csvText\":\"name,age\\nAda,36\\nGrace,85\"}",
                    label = "src",
                    positionX = 0.0,
                    positionY = 0.0,
                },
            },
            edges = Array.Empty<object>(),
        });
        update.EnsureSuccessStatusCode();

        var publish = await client.PostAsync($"/api/v1/pipelines/{id}/publish", null);
        publish.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<JsonDocument> PollRunAsync(HttpClient client, Guid runId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (true)
        {
            var res = await client.GetAsync($"/api/v1/runs/{runId}");
            res.EnsureSuccessStatusCode();
            var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
            var status = body.RootElement.GetProperty("status").GetInt32();
            if (status is 2 or 3 or 4 || DateTime.UtcNow > deadline)
                return body;
            await Task.Delay(1000);
        }
    }

    [Fact]
    public async Task FullRun_CompletesWithOutput()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var pipelineId = await CreatePublishedPipelineAsync(client, "Run me");

        var start = await client.PostAsync($"/api/v1/pipelines/{pipelineId}/runs", null);
        start.EnsureSuccessStatusCode();
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var run = await PollRunAsync(client, runId);
        Assert.Equal(2, run.RootElement.GetProperty("status").GetInt32());

        var tasks = await client.GetAsync($"/api/v1/runs/{runId}/tasks");
        tasks.EnsureSuccessStatusCode();
        var taskList = (await tasks.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Single(taskList.RootElement.EnumerateArray());

        var taskId = taskList.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        var attempts = await client.GetAsync($"/api/v1/tasks/{taskId}/attempts");
        attempts.EnsureSuccessStatusCode();

        var logs = await client.GetAsync($"/api/v1/runs/{runId}/logs");
        logs.EnsureSuccessStatusCode();
        Assert.NotEmpty((await logs.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.EnumerateArray());
    }

    [Fact]
    public async Task StartRun_Unpublished_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "Draft" });
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var start = await client.PostAsync($"/api/v1/pipelines/{id}/runs", null);
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
    }

    [Fact]
    public async Task Cancel_CompletedRun_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var pipelineId = await CreatePublishedPipelineAsync(client, "Cancel me");

        var start = await client.PostAsync($"/api/v1/pipelines/{pipelineId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();
        await PollRunAsync(client, runId);

        var cancel = await client.PostAsync($"/api/v1/runs/{runId}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
    }

    [Fact]
    public async Task Retry_NonFailedTask_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var pipelineId = await CreatePublishedPipelineAsync(client, "Retry me");

        var start = await client.PostAsync($"/api/v1/pipelines/{pipelineId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();
        await PollRunAsync(client, runId);

        var tasks = (await (await client.GetAsync($"/api/v1/runs/{runId}/tasks"))
            .Content.ReadFromJsonAsync<JsonDocument>())!;
        var taskId = tasks.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();

        var retry = await client.PostAsync($"/api/v1/tasks/{taskId}/retry", null);
        Assert.Equal(HttpStatusCode.BadRequest, retry.StatusCode);
    }
}
