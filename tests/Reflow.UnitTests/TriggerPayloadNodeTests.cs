using System.Text.Json;
using Reflow.Modules.NodeTypes.TriggerPayload;
using Xunit;

namespace Reflow.UnitTests;

public class TriggerPayloadNodeTests
{
    private static JsonElement Config(string json) =>
        JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Definition_IsSourceWithNoInputs()
    {
        var handler = new TriggerPayloadHandler();
        Assert.Equal("trigger.payload", handler.Type);
        Assert.Empty(handler.Definition.InputPorts);
        Assert.NotEmpty(handler.Definition.OutputPorts);
    }

    [Fact]
    public async Task ParsesPayloadWithRootPath()
    {
        var handler = new TriggerPayloadHandler();
        var frame = await handler.ExecuteWithPayload(
            Config("{\"rootPath\":\"order\"}"),
            "{\"order\":{\"id\":9,\"total\":12.5}}",
            CancellationToken.None);
        Assert.Equal("9", frame.Rows[0][frame.Columns.IndexOf("id")]);
    }

    [Fact]
    public async Task MissingPayload_FailsPermanently()
    {
        var handler = new TriggerPayloadHandler();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteWithPayload(Config("{}"), null, CancellationToken.None));
        Assert.StartsWith("Invalid trigger payload", ex.Message);
    }

    [Fact]
    public async Task PlainExecute_Throws()
    {
        var handler = new TriggerPayloadHandler();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(
                Array.Empty<Reflow.Modules.DataProcessing.Abstractions.Frame>(),
                Config("{}"), CancellationToken.None));
    }
}
