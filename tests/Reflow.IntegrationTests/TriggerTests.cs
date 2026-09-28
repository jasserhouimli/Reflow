using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class TriggerTests
{
    private readonly ReflowApiFactory _factory;

    public TriggerTests(ReflowApiFactory factory) => _factory = factory;

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
                    configJson = "{\"csvText\":\"a\\n1\"}",
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

    [Fact]
    public async Task ScheduleCrud_Works()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var pipelineId = await CreatePublishedPipelineAsync(client, "Scheduled");

        var create = await client.PostAsJsonAsync($"/api/v1/pipelines/{pipelineId}/triggers/schedules",
            new { name = "Nightly", cron = "0 2 * * *", timezone = "UTC", overlap = 0 });
        create.EnsureSuccessStatusCode();
        var triggerId = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync($"/api/v1/pipelines/{pipelineId}/triggers");
        list.EnsureSuccessStatusCode();

        var update = await client.PutAsJsonAsync(
            $"/api/v1/pipelines/{pipelineId}/triggers/schedules/{triggerId}",
            new { cron = "30 3 * * *" });
        update.EnsureSuccessStatusCode();

        var delete = await client.DeleteAsync($"/api/v1/pipelines/{pipelineId}/triggers/{triggerId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Schedule_BadCron_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var pipelineId = await CreatePublishedPipelineAsync(client, "Bad cron");

        var create = await client.PostAsJsonAsync($"/api/v1/pipelines/{pipelineId}/triggers/schedules",
            new { name = "Bad", cron = "nope", timezone = "UTC", overlap = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Webhook_StartsRunWithPayload()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var pipelineId = await CreatePublishedPipelineAsync(client, "Hooked");

        var create = await client.PostAsJsonAsync($"/api/v1/pipelines/{pipelineId}/triggers/webhooks",
            new { name = "Incoming" });
        create.EnsureSuccessStatusCode();
        var token = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("token").GetString()!;

        var anon = _factory.CreateClient();
        var hook = await anon.PostAsync($"/api/v1/hooks/{token}",
            new StringContent("{\"hello\":\"world\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Accepted, hook.StatusCode);
    }

    [Fact]
    public async Task Webhook_DuplicateEventId_StartsSingleRun()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var pipelineId = await CreatePublishedPipelineAsync(client, "Idempotent");

        var create = await client.PostAsJsonAsync($"/api/v1/pipelines/{pipelineId}/triggers/webhooks",
            new { name = "Incoming" });
        var token = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("token").GetString()!;

        // Wait for the first run to finish so the queue guard does not interfere.
        var anon = _factory.CreateClient();
        var first = await anon.PostAsync($"/api/v1/hooks/{token}",
            new StringContent("{\"eventId\":\"evt-1\"}", Encoding.UTF8, "application/json"));
        first.EnsureSuccessStatusCode();
        var runId = (await first.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (true)
        {
            var res = await client.GetAsync($"/api/v1/runs/{runId}");
            var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
            if (body.RootElement.GetProperty("status").GetInt32() is 2 or 3 or 4
                || DateTime.UtcNow > deadline)
                break;
            await Task.Delay(1000);
        }

        var second = await anon.PostAsync($"/api/v1/hooks/{token}",
            new StringContent("{\"eventId\":\"evt-1\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = (await second.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Equal(runId, secondBody.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Webhook_BadToken_Returns404()
    {
        var anon = _factory.CreateClient();
        var res = await anon.PostAsync("/api/v1/hooks/does-not-exist",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task WebhookPayload_ReachesTriggerPayloadNode()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "Payload flow" });
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/v1/pipelines/{id}", new
        {
            nodes = new[]
            {
                new
                {
                    nodeId = "in",
                    nodeType = "trigger.payload",
                    configJson = "{\"rootPath\":\"order\"}",
                    label = "in",
                    positionX = 0.0,
                    positionY = 0.0,
                },
            },
            edges = Array.Empty<object>(),
        });
        update.EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/v1/pipelines/{id}/publish", null)).EnsureSuccessStatusCode();

        var hook = await client.PostAsJsonAsync($"/api/v1/pipelines/{id}/triggers/webhooks",
            new { name = "Incoming" });
        var token = (await hook.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("token").GetString()!;

        var anon = _factory.CreateClient();
        var fire = await anon.PostAsync($"/api/v1/hooks/{token}",
            new StringContent("{\"order\":{\"id\":42}}", Encoding.UTF8, "application/json"));
        fire.EnsureSuccessStatusCode();
        var runId = (await fire.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (true)
        {
            var res = await client.GetAsync($"/api/v1/runs/{runId}");
            var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
            if (body.RootElement.GetProperty("status").GetInt32() is 2 or 3 or 4
                || DateTime.UtcNow > deadline)
                break;
            await Task.Delay(1000);
        }

        var tasks = (await (await client.GetAsync($"/api/v1/runs/{runId}/tasks"))
            .Content.ReadFromJsonAsync<JsonDocument>())!;
        var taskId = tasks.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        var task = (await (await client.GetAsync($"/api/v1/tasks/{taskId}"))
            .Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Contains("42", task.RootElement.GetProperty("outputJson").GetString());
    }
}
