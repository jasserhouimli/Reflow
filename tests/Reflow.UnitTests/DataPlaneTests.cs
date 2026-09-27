using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.DataProcessing.DataQuality;
using Reflow.Modules.DataProcessing.Datasets;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.DataProcessing.Lineage;
using Xunit;

namespace Reflow.UnitTests;

public class DataPlaneTests
{
    [Fact]
    public async Task Artifact_SaveAndLoad_JsonRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "reflow-artifacts-" + Guid.NewGuid().ToString("N"));
        var store = new LocalArtifactStore(new FrameWriter(), root);
        var runId = Guid.NewGuid();
        var frame = new Frame
        {
            Columns = new List<string> { "a" },
            Rows = new List<List<string?>> { new() { "1" } },
        };

        var info = await store.SaveAsync(runId, "node.1", frame, "json");
        Assert.Equal(1, info.RowCount);
        Assert.NotEmpty(info.ChecksumSha256);

        var loaded = await store.LoadAsync(runId, "node.1");
        Assert.NotNull(loaded);
        Assert.Contains("\"a\"", loaded.Value.Content);
    }

    [Fact]
    public async Task Artifact_MaliciousNodeId_StaysInsideRunDir()
    {
        var root = Path.Combine(Path.GetTempPath(), "reflow-artifacts-" + Guid.NewGuid().ToString("N"));
        var store = new LocalArtifactStore(new FrameWriter(), root);
        var runId = Guid.NewGuid();
        var frame = new Frame { Columns = new List<string> { "a" }, Rows = new() };

        var info = await store.SaveAsync(runId, "../../evil", frame, "json");
        Assert.DoesNotContain("..", info.FileName);
        Assert.True(File.Exists(Path.Combine(root, runId.ToString("N"), info.FileName)));
    }

    [Fact]
    public async Task Catalog_RegisterList_ByLayer()
    {
        IDatasetCatalog catalog = new InMemoryDatasetCatalog();
        var bronze = new DatasetMetadata(Guid.NewGuid(), "orders.raw", DatasetLayer.Bronze,
            "parquet", new[] { "id" }, 10, 128, "bronze/orders", DateTime.UtcNow, null, null);
        var gold = bronze with { Id = Guid.NewGuid(), Name = "orders.mart", Layer = DatasetLayer.Gold };

        await catalog.RegisterAsync(bronze);
        await catalog.RegisterAsync(gold);

        Assert.Equal(2, (await catalog.ListAsync()).Count);
        Assert.Single(await catalog.ListAsync(DatasetLayer.Gold));
        Assert.Equal("orders.raw", (await catalog.GetAsync(bronze.Id))!.Name);
    }

    [Fact]
    public void Profiler_CountsNullsDistinctAndMean()
    {
        IDataProfiler profiler = new FrameProfiler();
        var frame = new Frame
        {
            Columns = new List<string> { "n", "s" },
            Rows = new List<List<string?>>
            {
                new() { "2", "x" }, new() { "4", null }, new() { "2", "y" },
            },
        };

        var profiles = profiler.Profile(frame).ToDictionary(p => p.Column);
        Assert.Equal(8.0 / 3, profiles["n"].Mean!.Value, precision: 10);
        Assert.Equal(2, profiles["n"].DistinctCount);
        Assert.Equal(1, profiles["s"].NullCount);
        Assert.Null(profiles["s"].Mean);
    }

    [Fact]
    public void Quality_RequiredAndUnique_CountsFailures()
    {
        var frame = new Frame
        {
            Columns = new List<string> { "id", "email" },
            Rows = new List<List<string?>>
            {
                new() { "1", "a@x.com" }, new() { null, "b@x.com" }, new() { "1", "c@x.com" },
            },
        };

        var result = QualityEvaluator.Evaluate(frame, new[] { "id" }, new[] { "id" });
        Assert.Equal(3, result.InputCount);
        Assert.Equal(1, result.ValidCount);
        Assert.Equal(2, result.RejectedCount);
        Assert.True(result.FailuresByRule.ContainsKey("required:id"));
        Assert.True(result.FailuresByRule.ContainsKey("unique:id"));
    }

    [Fact]
    public async Task Lineage_UpstreamDownstream_NoSelfLoop()
    {
        ILineageStore store = new InMemoryLineageStore();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await store.AddAsync(new LineageEdge(a, b, null, "clean"));

        Assert.Single(await store.DownstreamOfAsync(a));
        Assert.Single(await store.UpstreamOfAsync(b));
        Assert.Empty(await store.UpstreamOfAsync(a));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AddAsync(new LineageEdge(a, a, null, null)));
    }
}
