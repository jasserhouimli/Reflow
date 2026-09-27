using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Reflow.IntegrationTests;

[Collection("api")]
public class NodeTypesTests
{
    private readonly ReflowApiFactory _factory;

    public NodeTypesTests(ReflowApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ListNodeTypes_ExposesCsvRead()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/node-types");
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<JsonDocument>())!;
        Assert.Contains(body.RootElement.EnumerateArray(),
            el => el.GetProperty("type").GetString() == "csv.read");
    }
}
