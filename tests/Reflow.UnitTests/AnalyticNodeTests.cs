using System.Text.Json;
using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.Abstractions;
using Reflow.Modules.NodeTypes.Aggregate;
using Reflow.Modules.NodeTypes.DataOutput;
using Reflow.Modules.NodeTypes.Join;
using Reflow.Modules.NodeTypes.Sort;
using Xunit;

namespace Reflow.UnitTests;

public class AnalyticNodeTests
{
    private static JsonElement Config(string json) =>
        JsonDocument.Parse(json).RootElement;

    private static NodeInput Table(string id, string csv)
    {
        var codec = new CsvCodec();
        return new NodeInput(id, codec.FromCsv(csv, ',', true));
    }

    [Fact]
    public async Task Aggregate_GroupsAndSums()
    {
        var handler = new AggregateHandler(new DuckDbSqlEngine());
        var frame = await handler.ExecuteAsync(
            new[] { Table("orders", "country,amount\nFR,10\nFR,20\nDE,5") },
            Config("{\"groupBy\":[\"country\"],\"operations\":[{\"function\":\"sum\",\"column\":\"amount\",\"as\":\"total\"},{\"function\":\"count\",\"as\":\"n\"}]}"),
            CancellationToken.None);
        Assert.Equal(2, frame.RowCount);
        Assert.Contains("total", frame.Columns);
        var fr = frame.Rows.First(r => r[0] == "FR");
        Assert.Equal("30", fr[frame.Columns.IndexOf("total")]);
    }

    [Fact]
    public void Aggregate_EmptyOperations_Fails()
    {
        var handler = new AggregateHandler(new DuckDbSqlEngine());
        Assert.NotEmpty(handler.ValidateConfig(Config("{\"groupBy\":[\"a\"]}")));
    }

    [Fact]
    public async Task Sort_OrdersDescending()
    {
        var handler = new SortHandler(new DuckDbSqlEngine());
        var frame = await handler.ExecuteAsync(
            new[] { Table("t", "name\nBob\nAda\nClara") },
            Config("{\"orderBy\":[{\"column\":\"name\",\"direction\":\"desc\"}]}"),
            CancellationToken.None);
        Assert.Equal("Clara", frame.Rows[0][0]);
        Assert.Equal("Ada", frame.Rows[2][0]);
    }

    [Fact]
    public void Sort_BadDirection_Fails()
    {
        var handler = new SortHandler(new DuckDbSqlEngine());
        Assert.Contains(
            handler.ValidateConfig(Config("{\"orderBy\":[{\"column\":\"a\",\"direction\":\"sideways\"}]}")),
            e => e.Contains("direction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Join_CombinesOnKey()
    {
        var handler = new JoinHandler(new DuckDbSqlEngine());
        var frame = await handler.ExecuteAsync(
            new[]
            {
                Table("orders", "id,customer\n1,Alice\n2,Bob"),
                Table("tiers", "customer,tier\nAlice,gold"),
            },
            Config("{\"on\":\"customer\",\"how\":\"inner\"}"),
            CancellationToken.None);
        Assert.Single(frame.Rows);
        Assert.Contains("tier", frame.Columns);
        Assert.DoesNotContain("customer_right", frame.Columns);
    }

    [Fact]
    public void Join_MissingKeys_Fails()
    {
        var handler = new JoinHandler(new DuckDbSqlEngine());
        Assert.NotEmpty(handler.ValidateConfig(Config("{\"how\":\"inner\"}")));
    }

    [Fact]
    public async Task DataOutput_SavesArtifactAndPassesThrough()
    {
        var root = Path.Combine(Path.GetTempPath(), "reflow-output-" + Guid.NewGuid().ToString("N"));
        var handler = new DataOutputHandler(new LocalArtifactStore(new FrameWriter(), root));
        var runId = Guid.NewGuid();
        var frame = await handler.ExecuteInRunAsync(
            new[] { Table("src", "a\n1\n2") },
            Config("{\"format\":\"csv\"}"),
            runId, "out", CancellationToken.None);
        Assert.Equal(2, frame.RowCount);

        var store = new LocalArtifactStore(new FrameWriter(), root);
        var loaded = await store.LoadAsync(runId, "out");
        Assert.NotNull(loaded);
        Assert.Contains("a", loaded.Value.Content);
    }
}
