using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.Abstractions;
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
        var frame = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            config: Config("{\"jsonText\":\"[{\\\"a\\\":1},{\\\"a\\\":2}]\"}")));
        Assert.Equal(2, frame.RowCount);
        Assert.Equal("2", frame.Rows[1][0]);
    }

    [Fact]
    public async Task JsonRead_ColumnMode_UnpacksObjects()
    {
        var handler = new JsonReadHandler();
        var input = Csv("id,payload\n1,\"{\"\"city\"\":\"\"Paris\"\"}\"\n2,\"{\"\"city\"\":\"\"Lyon\"\"}\"");
        var frame = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            inputs: new[] { new NodeInput("in", input) },
            config: Config("{\"column\":\"payload\"}")));
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
        var handler = new FilterHandler(new DuckDbSqlEngine());
        var frame = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            inputs: new[] { new NodeInput("in", Csv("name,age\nAda,36\nGrace,85")) },
            config: Config("{\"column\":\"age\",\"operator\":\"greaterThan\",\"value\":\"40\"}")));
        Assert.Single(frame.Rows);
        Assert.Equal("Grace", frame.Rows[0][0]);
    }

    [Fact]
    public async Task Filter_ContainsIsCaseInsensitive()
    {
        var handler = new FilterHandler(new DuckDbSqlEngine());
        var frame = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            inputs: new[] { new NodeInput("in", Csv("name\nAda\nBob")) },
            config: Config("{\"column\":\"name\",\"operator\":\"contains\",\"value\":\"AD\"}")));
        Assert.Single(frame.Rows);
    }

    [Fact]
    public void Filter_BadOperator_Fails()
    {
        var handler = new FilterHandler(new DuckDbSqlEngine());
        Assert.Contains(handler.ValidateConfig(Config("{\"column\":\"a\",\"operator\":\"fuzzy\"}")),
            e => e.Contains("operator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Transform_SelectDropRename()
    {
        var handler = new TransformHandler(new DuckDbSqlEngine());
        var input = Csv("a,b,c\n1,2,3");

        var selected = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            inputs: new[] { new NodeInput("in", input) },
            config: Config("{\"select\":[\"c\",\"a\"]}")));
        Assert.Equal(new[] { "c", "a" }, selected.Columns);

        var dropped = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            inputs: new[] { new NodeInput("in", input) },
            config: Config("{\"dropColumns\":[\"b\"]}")));
        Assert.Equal(new[] { "a", "c" }, dropped.Columns);

        var renamed = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            inputs: new[] { new NodeInput("in", input) },
            config: Config("{\"renames\":{\"a\":\"alpha\"}}")));
        Assert.Equal(new[] { "alpha", "b", "c" }, renamed.Columns);
        Assert.Equal("1", renamed.Rows[0][0]);

        var combined = await handler.ExecuteAsync(NodeExecutionContext.ForTest(
            inputs: new[] { new NodeInput("in", input) },
            config: Config("{\"select\":[\"a\",\"b\"],\"renames\":{\"b\":\"beta\"}}")));
        Assert.Equal(new[] { "a", "beta" }, combined.Columns);
    }

    [Fact]
    public void Transform_EmptyConfig_Fails()
    {
        var handler = new TransformHandler(new DuckDbSqlEngine());
        Assert.NotEmpty(handler.ValidateConfig(Config("{}")));
    }
}
