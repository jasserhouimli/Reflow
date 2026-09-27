using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class PipelineTests
{
    private readonly ReflowApiFactory _factory;

    public PipelineTests(ReflowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateListGet_Roundtrip()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);

        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "Sales" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/v1/pipelines");
        list.EnsureSuccessStatusCode();

        var get = await client.GetAsync($"/api/v1/pipelines/{id}");
        get.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task FullGraph_PublishCreatesVersion()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "ETL" });
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/v1/pipelines/{id}", new
        {
            nodes = new[]
            {
                new { nodeId = "a", nodeType = "csv.read", configJson = "{}", label = "a", positionX = 0.0, positionY = 0.0 },
                new { nodeId = "b", nodeType = "filter", configJson = "{}", label = "b", positionX = 1.0, positionY = 0.0 },
            },
            edges = new[] { new { sourceNodeId = "a", targetNodeId = "b" } },
        });
        update.EnsureSuccessStatusCode();

        var validate = await client.PostAsync($"/api/v1/pipelines/{id}/validate", null);
        var validation = (await validate.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.True(validation.RootElement.GetProperty("valid").GetBoolean());

        var publish = await client.PostAsync($"/api/v1/pipelines/{id}/publish", null);
        publish.EnsureSuccessStatusCode();

        var versions = await client.GetAsync($"/api/v1/pipelines/{id}/versions");
        versions.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Publish_InvalidGraph_Returns400()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "Bad" });
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var publish = await client.PostAsync($"/api/v1/pipelines/{id}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
    }

    [Fact]
    public async Task Archive_BlocksEdit()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "Old" });
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var archive = await client.PostAsync($"/api/v1/pipelines/{id}/archive", null);
        archive.EnsureSuccessStatusCode();

        var update = await client.PutAsJsonAsync($"/api/v1/pipelines/{id}", new { name = "New" });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task User_CannotSeeAnotherUsersPipeline()
    {
        var alice = await ApiHelpers.LoginNewUserAsync(_factory);
        var bob = await ApiHelpers.LoginNewUserAsync(_factory);

        var create = await alice.PostAsJsonAsync("/api/v1/pipelines", new { name = "Secret" });
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var res = await bob.GetAsync($"/api/v1/pipelines/{id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
