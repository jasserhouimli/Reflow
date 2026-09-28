using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.NodeTypes.Abstractions;
using Reflow.Modules.NodeTypes.DataSql;
using Xunit;

namespace Reflow.UnitTests;

public class SqlNodeTests
{
    private static JsonElement Config(string json) =>
        JsonDocument.Parse(json).RootElement;

    private static Frame Table()
    {
        return new Frame
        {
            Columns = new List<string> { "country", "amount" },
            Rows = new List<List<string?>>
            {
                new() { "FR", "10" }, new() { "FR", "20" }, new() { "DE", "5" },
            },
        };
    }

    [Fact]
    public void Engine_AggregatesWithGroupBy()
    {
        ISqlEngine engine = new DuckDbSqlEngine();
        var result = engine.Query(
            new[] { ("input", Table()) },
            "SELECT country, SUM(CAST(amount AS INTEGER)) AS total FROM input GROUP BY country ORDER BY country");
        Assert.Equal(new[] { "country", "total" }, result.Columns);
        Assert.Equal(2, result.RowCount);
        Assert.Equal("30", result.Rows[1][1]);
    }

    [Fact]
    public void Engine_JoinsTwoTables()
    {
        ISqlEngine engine = new DuckDbSqlEngine();
        var left = Table();
        var right = new Frame
        {
            Columns = new List<string> { "country", "currency" },
            Rows = new List<List<string?>> { new() { "FR", "EUR" } },
        };
        var result = engine.Query(
            new[] { ("a", left), ("b", right) },
            "SELECT a.country, b.currency FROM a JOIN b ON a.country = b.country");
        Assert.Equal(2, result.RowCount);
        Assert.Equal("EUR", result.Rows[0][1]);
    }

    [Fact]
    public void Engine_BlocksFileAccess()
    {
        ISqlEngine engine = new DuckDbSqlEngine();
        Assert.ThrowsAny<Exception>(() => engine.Query(
            Array.Empty<(string, Frame)>(),
            "SELECT * FROM read_csv('/etc/passwd')"));
    }

    [Fact]
    public void Engine_BadSql_Throws()
    {
        ISqlEngine engine = new DuckDbSqlEngine();
        Assert.ThrowsAny<Exception>(() => engine.Query(
            new[] { ("input", Table()) }, "SELECT nope FROM input"));
    }

    [Fact]
    public async Task Slice_ValidatesAndExecutes()
    {
        var handler = new DataSqlHandler(new DuckDbSqlEngine());
        Assert.Equal("data.sql", handler.Type);
        Assert.NotEmpty(handler.ValidateConfig(Config("{}")));
        var frame = await handler.ExecuteAsync(
            new[] { new NodeInput("orders", Table()) },
            Config("{\"query\":\"SELECT COUNT(*) AS n FROM orders\"}"),
            CancellationToken.None);
        Assert.Equal("3", frame.Rows[0][0]);
    }

    [Fact]
    public void NameTables_SanitizesAndDedupes()
    {
        var tables = DataSqlHandler.NameTables(new[]
        {
            new NodeInput("Orders!", Table()),
            new NodeInput("orders", Table()),
        });
        Assert.Equal("orders", tables[0].Name);
        Assert.Equal("orders_2", tables[1].Name);
    }
}
