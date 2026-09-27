using System.Text.Json;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.CsvRead;
using Reflow.Modules.NodeTypes.Registry;
using Xunit;

namespace Reflow.UnitTests;

public class CsvReadNodeTests
{
    private static JsonElement Config(string json) =>
        JsonDocument.Parse(json).RootElement;

    private CsvReadHandler Handler() =>
        new(new CsvCodec());

    [Fact]
    public void Type_IsStable()
    {
        Assert.Equal("csv.read", Handler().Type);
    }

    [Fact]
    public void Definition_SuitsEditorPalette()
    {
        var def = Handler().Definition;
        Assert.Equal("csv.read", def.Type);
        Assert.NotEmpty(def.DisplayName);
        Assert.NotEmpty(def.Category);
        Assert.Empty(def.InputPorts);
        Assert.NotEmpty(def.OutputPorts);
    }

    [Fact]
    public void ValidConfig_Passes()
    {
        Assert.Empty(Handler().ValidateConfig(Config("{\"csvText\":\"a\\n1\",\"delimiter\":\",\",\"hasHeader\":true}")));
    }

    [Fact]
    public void MissingCsvText_Fails()
    {
        Assert.Contains(Handler().ValidateConfig(Config("{}")),
            e => e.Contains("csvText", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BadDelimiter_Fails()
    {
        Assert.Contains(Handler().ValidateConfig(Config("{\"csvText\":\"a\",\"delimiter\":\"::\"}")),
            e => e.Contains("delimiter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Execute_ParsesCsv()
    {
        var frame = await Handler().ExecuteAsync(
            Array.Empty<Reflow.Modules.DataProcessing.Abstractions.Frame>(),
            Config("{\"csvText\":\"a,b\\n1,2\"}"),
            CancellationToken.None);
        Assert.Equal(new[] { "a", "b" }, frame.Columns);
        Assert.Equal("2", frame.Rows[0][1]);
    }

    [Fact]
    public async Task Execute_InvalidConfig_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler().ExecuteAsync(
                Array.Empty<Reflow.Modules.DataProcessing.Abstractions.Frame>(),
                Config("{}"),
                CancellationToken.None));
    }

    [Fact]
    public void Registry_ResolvesByType_AndMissesUnknown()
    {
        var registry = new NodeTypeRegistry();
        registry.Register(Handler());
        Assert.True(registry.TryGet("csv.read", out _));
        Assert.True(registry.TryGet("CSV.READ", out _));
        Assert.False(registry.TryGet("nope.missing", out _));
        Assert.Single(registry.Definitions());
    }
}
