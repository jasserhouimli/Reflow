using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class FileTests
{
    private readonly ReflowApiFactory _factory;

    public FileTests(ReflowApiFactory factory) => _factory = factory;

    private static async Task<Guid> UploadCsvAsync(HttpClient client, string name = "orders.csv")
    {
        var res = await client.PostAsJsonAsync("/api/v1/files", new
        {
            fileName = name,
            content = "region,amount\nEU,120\nUS,80",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
        return body.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Upload_List_Delete_RoundTrip()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await UploadCsvAsync(client);

        var list = await client.GetAsync("/api/v1/files");
        list.EnsureSuccessStatusCode();
        var files = (await list.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement;
        Assert.Contains(files.EnumerateArray(), f => f.GetProperty("id").GetGuid() == id);
        // Content must never be returned by the list endpoint.
        Assert.All(files.EnumerateArray(), f => Assert.False(f.TryGetProperty("content", out _)));

        var delete = await client.DeleteAsync($"/api/v1/files/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var after = (await (await client.GetAsync("/api/v1/files"))
            .Content.ReadFromJsonAsync<JsonDocument>())!.RootElement;
        Assert.DoesNotContain(after.EnumerateArray(), f => f.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Upload_IsIdempotent_ForSameContentAndName()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var first = await UploadCsvAsync(client, "dupe.csv");

        var res = await client.PostAsJsonAsync("/api/v1/files", new
        {
            fileName = "dupe.csv",
            content = "region,amount\nEU,120\nUS,80",
        });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Equal(first, body.RootElement.GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("data.txt", "hello")]
    [InlineData("data.csv", "")]
    public async Task Upload_RejectsInvalidFiles(string fileName, string content)
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var res = await client.PostAsJsonAsync("/api/v1/files", new { fileName, content });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_StripsPathFromFileName()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var res = await client.PostAsJsonAsync("/api/v1/files", new
        {
            fileName = "../../etc/orders.csv",
            content = "a\n1",
        });
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Equal("orders.csv", body.RootElement.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Delete_OtherUsersFile_Returns404()
    {
        var owner = await ApiHelpers.LoginNewUserAsync(_factory);
        var id = await UploadCsvAsync(owner);

        var other = await ApiHelpers.LoginNewUserAsync(_factory);
        var delete = await other.DeleteAsync($"/api/v1/files/{id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Anon_Listing_Returns401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/files");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Pipeline_WithFileReadSource_RunsToCompletion()
    {
        var client = await ApiHelpers.LoginNewUserAsync(_factory);
        var fileId = await UploadCsvAsync(client);

        var create = await client.PostAsJsonAsync("/api/v1/pipelines", new { name = "File pipeline" });
        create.EnsureSuccessStatusCode();
        var pipelineId = (await create.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var config = JsonSerializer.Serialize(new { fileId, format = "csv", hasHeader = true });
        var update = await client.PutAsJsonAsync($"/api/v1/pipelines/{pipelineId}", new
        {
            nodes = new[]
            {
                new
                {
                    nodeId = "file",
                    nodeType = "file.read",
                    configJson = config,
                    label = "orders.csv",
                    positionX = 0.0,
                    positionY = 0.0,
                },
            },
            edges = Array.Empty<object>(),
        });
        update.EnsureSuccessStatusCode();

        var publish = await client.PostAsync($"/api/v1/pipelines/{pipelineId}/publish", null);
        publish.EnsureSuccessStatusCode();

        var start = await client.PostAsync($"/api/v1/pipelines/{pipelineId}/runs", null);
        start.EnsureSuccessStatusCode();
        var runId = (await start.Content.ReadFromJsonAsync<JsonDocument>())!
            .RootElement.GetProperty("id").GetGuid();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (true)
        {
            var res = await client.GetAsync($"/api/v1/runs/{runId}");
            res.EnsureSuccessStatusCode();
            var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
            var status = body.RootElement.GetProperty("status").GetInt32();
            if (status is 2 or 3 or 4 || DateTime.UtcNow > deadline)
            {
                Assert.Equal(2, status);
                break;
            }
            await Task.Delay(1000);
        }
    }
}
