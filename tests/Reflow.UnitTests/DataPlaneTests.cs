using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.Artifacts;
using Reflow.Modules.DataProcessing.InMemory;
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
            Columns = new List<string> { "a", "b" },
            Rows = new List<List<string?>> { new() { "1", "hello" } },
        };

        var info = await store.SaveAsync(runId, "node.1", frame, "json");
        Assert.Equal(1, info.RowCount);
        Assert.NotEmpty(info.ChecksumSha256);

        var loaded = await store.LoadAsync(runId, "node.1");
        Assert.NotNull(loaded);
        Assert.Equal("application/json", loaded.Value.ContentType);
        Assert.Contains("\"a\"", loaded.Value.Content);
        Assert.Contains("\"hello\"", loaded.Value.Content);
    }

    [Fact]
    public async Task Artifact_SaveAndLoad_CsvRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "reflow-artifacts-" + Guid.NewGuid().ToString("N"));
        var store = new LocalArtifactStore(new FrameWriter(), root);
        var runId = Guid.NewGuid();
        var frame = new Frame
        {
            Columns = new List<string> { "name", "score" },
            Rows = new List<List<string?>> { new() { "Alice, A", "100" } },
        };

        var info = await store.SaveAsync(runId, "node.csv", frame, "csv");
        Assert.Equal(1, info.RowCount);

        var loaded = await store.LoadAsync(runId, "node.csv");
        Assert.NotNull(loaded);
        Assert.Equal("text/csv", loaded.Value.ContentType);
        Assert.Contains("\"Alice, A\"", loaded.Value.Content);
    }

    [Fact]
    public async Task Artifact_CustomFileName_PreservesExtension()
    {
        var root = Path.Combine(Path.GetTempPath(), "reflow-artifacts-" + Guid.NewGuid().ToString("N"));
        var store = new LocalArtifactStore(new FrameWriter(), root);
        var runId = Guid.NewGuid();
        var frame = new Frame { Columns = new List<string> { "id" }, Rows = new() };

        var info = await store.SaveAsync(runId, "export_node", frame, "csv", fileName: "monthly_report");
        Assert.Equal("monthly_report.csv", info.FileName);

        var loaded = await store.LoadAsync(runId, "export_node");
        Assert.NotNull(loaded);
        Assert.Equal("monthly_report.csv", loaded.Value.FileName);
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
    public async Task Artifact_LoadMissing_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "reflow-artifacts-" + Guid.NewGuid().ToString("N"));
        var store = new LocalArtifactStore(new FrameWriter(), root);
        var loaded = await store.LoadAsync(Guid.NewGuid(), "non_existent");
        Assert.Null(loaded);
    }
}
