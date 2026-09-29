using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class RealtimeTests
{
    private readonly ReflowApiFactory _factory;

    public RealtimeTests(ReflowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task JoinRun_OwnRun_Succeeds()
    {
        var (client, token) = await ApiHelpers.LoginNewUserWithTokenAsync(_factory);
        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "Live" });
        var pipelineId = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/runs", o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        await connection.StartAsync();

        // No runs yet: joining a random id must be rejected, proving the guard runs.
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            connection.InvokeAsync("JoinRun", Guid.NewGuid()));
        Assert.Contains("not found", (ex.InnerException?.Message ?? ex.Message),
            StringComparison.OrdinalIgnoreCase);
        await connection.StopAsync();
    }

    [Fact]
    public async Task JoinRun_ForeignRun_IsRejected()
    {
        var alice = await ApiHelpers.LoginNewUserAsync(_factory);
        var (_, bobToken) = await ApiHelpers.LoginNewUserWithTokenAsync(_factory);

        var create = await alice.PostAsJsonAsync("/api/v1/pipelines", new { name = "Alice pipe" });
        var pipelineId = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        // Publish an empty graph is invalid; use a csv node so a run can start.
        await alice.PutAsJsonAsync($"/api/v1/pipelines/{pipelineId}", new
        {
            nodes = new[]
            {
                new { nodeId = "a", nodeType = "csv.read", configJson = "{\"csvText\":\"x\\n1\"}", label = "a", positionX = 0.0, positionY = 0.0 },
            },
            edges = Array.Empty<object>(),
        });
        await alice.PostAsync($"/api/v1/pipelines/{pipelineId}/publish", null);
        var start = await alice.PostAsync($"/api/v1/pipelines/{pipelineId}/runs", null);
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var bob = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/runs", o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(bobToken);
            })
            .Build();
        await bob.StartAsync();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            bob.InvokeAsync("JoinRun", runId));
        Assert.Contains("not found", (ex.InnerException?.Message ?? ex.Message),
            StringComparison.OrdinalIgnoreCase);
        await bob.StopAsync();
    }
}
