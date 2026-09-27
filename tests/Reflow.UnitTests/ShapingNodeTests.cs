using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.Filter;
using Reflow.Modules.NodeTypes.JsonRead;
using Reflow.Modules.NodeTypes.Transform;
using Xunit;

namespace Reflow.UnitTests;

public class ShapingNodeTests
{
    private static JsonElement Config(string json) =>
        JsonDocument.Parse(json).RootElement;

    private static Frame Csv(string text)
    {
        var codec = new CsvCodec();
        return codec.FromCsv(text, ',', true);
    }

    [Fact]
    public async Task JsonRead_TextMode_ParsesArray()
    {
        var handler = new JsonReadHandler();
        var frame = await handler.ExecuteAsync(
            Array.Empty<Frame>(),
            Config("{\"jsonText\":\"[{\\\"a\\\":1},{\\\"a\\\":2}]\"}"),
            CancellationToken.None);
        Assert.Equal(2, frame.RowCount);
        Assert.Equal("2", frame.Rows[1][0]);
    }

    [Fact]
    public async Task JsonRead_ColumnMode_UnpacksObjects()
    {
        var handler = new JsonReadHandler();
        var input = Csv("id,payload\n1,\"{\"\"city\"\":\"\"Paris\"\"}\"\n2,\"{\"\"city\"\":\"\"Lyon\"\"}\"");
        var frame = await handler.ExecuteAsync(
            new[] { input }, Config("{\"column\":\"payload\"}"), CancellationToken.None);
        Assert.Equal(2, frame.RowCount);
        Assert.Contains("city", frame.Columns);
        Assert.Contains("id", frame.Columns);
    }

    [Fact]
    public void JsonRead_BothModes_Fails()
    {
        var handler = new JsonReadHandler();
        Assert.NotEmpty(handler.ValidateConfig(Config("{\"jsonText\":\"{}\",\"column\":\"c\"}")));
        Assert.NotEmpty(handler.ValidateConfig(Config("{}")));
    }

    [Fact]
    public async Task Filter_KeepsMatchingRows()
    {
        var handler = new FilterHandler(new InMemoryQueryEngine());
        var frame = await handler.ExecuteAsync(
            new[] { Csv("name,age\nAda,36\nGrace,85") },
            Config("{\"column\":\"age\",\"operator\":\"greaterThan\",\"value\":\"40\"}"),
            CancellationToken.None);
        Assert.Single(frame.Rows);
        Assert.Equal("Grace", frame.Rows[0][0]);
    }

    [Fact]
    public void Filter_BadOperator_Fails()
    {
        var handler = new FilterHandler(new InMemoryQueryEngine());
        Assert.Contains(handler.ValidateConfig(Config("{\"column\":\"a\",\"operator\":\"fuzzy\"}")),
            e => e.Contains("operator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Transform_SelectDropRename()
    {
        var handler = new TransformHandler(new InMemoryQueryEngine());
        var input = Csv("a,b,c\n1,2,3");

        var selected = await handler.ExecuteAsync(
            new[] { input }, Config("{\"select\":[\"c\",\"a\"]}"), CancellationToken.None);
        Assert.Equal(new[] { "c", "a" }, selected.Columns);

        var dropped = await handler.ExecuteAsync(
            new[] { input }, Config("{\"dropColumns\":[\"b\"]}"), CancellationToken.None);
        Assert.Equal(new[] { "a", "c" }, dropped.Columns);

        var renamed = await handler.ExecuteAsync(
            new[] { input }, Config("{\"renames\":{\"a\":\"alpha\"}}"), CancellationToken.None);
        Assert.Equal(new[] { "alpha", "b", "c" }, renamed.Columns);
        Assert.Equal("1", renamed.Rows[0][0]);
    }

    [Fact]
    public void Transform_EmptyConfig_Fails()
    {
        var handler = new TransformHandler(new InMemoryQueryEngine());
        Assert.NotEmpty(handler.ValidateConfig(Config("{}")));
    }
}
