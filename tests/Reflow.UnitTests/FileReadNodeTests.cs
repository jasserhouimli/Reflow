using System.Text.Json;
using Reflow.Modules.DataProcessing.InMemory;
using Reflow.Modules.NodeTypes.Abstractions;
using Reflow.Modules.NodeTypes.FileRead;
using Xunit;

namespace Reflow.UnitTests;

public class FileReadNodeTests
{
    private static JsonElement Config(string json) =>
        JsonDocument.Parse(json).RootElement;

    private sealed class FakeFiles : IFileSourceReader
    {
        private readonly Dictionary<Guid, SourceFileContent> _files = new();

        public FakeFiles(Guid id, string content, string contentType = "text/csv") =>
            _files[id] = new SourceFileContent("source", contentType, content);

        public Task<SourceFileContent?> FindAsync(Guid fileId, CancellationToken ct = default) =>
            Task.FromResult(_files.TryGetValue(fileId, out var file) ? file : null);
    }

    private static FileReadHandler Handler(Guid id, string content, string type = "text/csv") =>
        new(new CsvCodec(), new FakeFiles(id, content, type));

    [Fact]
    public void Definition_IsSourceWithNoInputs()
    {
        var handler = Handler(Guid.NewGuid(), "a\n1");
        Assert.Equal("file.read", handler.Type);
        Assert.Empty(handler.Definition.InputPorts);
        Assert.NotEmpty(handler.Definition.OutputPorts);
    }

    [Fact]
    public async Task ParsesCsvFile()
    {
        var id = Guid.NewGuid();
        var frame = await Handler(id, "name,age\nAda,36\nGrace,85")
            .ExecuteAsync(NodeExecutionContext.ForTest(
                config: Config($"{{\"fileId\":\"{id}\"}}")));

        Assert.Equal(new[] { "name", "age" }, frame.Columns);
        Assert.Equal(2, frame.RowCount);
        Assert.Equal("Ada", frame.Rows[0][0]);
    }

    [Fact]
    public async Task ParsesCsvWithCustomDelimiterAndNoHeader()
    {
        var id = Guid.NewGuid();
        var frame = await Handler(id, "Ada;36\nGrace;85")
            .ExecuteAsync(NodeExecutionContext.ForTest(
                config: Config($"{{\"fileId\":\"{id}\",\"delimiter\":\";\",\"hasHeader\":false}}")));

        Assert.Equal(new[] { "col1", "col2" }, frame.Columns);
        Assert.Equal(2, frame.RowCount);
    }

    [Fact]
    public async Task ParsesJsonFileWithRootPath()
    {
        var id = Guid.NewGuid();
        var frame = await Handler(id, "{\"data\":{\"orders\":[{\"id\":1},{\"id\":2}]}}", "application/json")
            .ExecuteAsync(NodeExecutionContext.ForTest(
                config: Config($"{{\"fileId\":\"{id}\",\"format\":\"json\",\"rootPath\":\"data.orders\"}}")));

        Assert.Equal(2, frame.RowCount);
        Assert.Contains("id", frame.Columns);
    }

    [Fact]
    public async Task MissingFile_FailsWithClearMessage()
    {
        var handler = Handler(Guid.NewGuid(), "a\n1");
        var unknown = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecuteAsync(NodeExecutionContext.ForTest(
                config: Config($"{{\"fileId\":\"{unknown}\"}}"))));

        Assert.Contains("was not found", ex.Message);
    }

    [Theory]
    [InlineData("{}", "fileId")]
    [InlineData("{\"fileId\":\"not-a-guid\"}", "fileId")]
    public void InvalidFileId_IsRejected(string config, string expected)
    {
        var handler = Handler(Guid.NewGuid(), "a\n1");
        var errors = handler.ValidateConfig(Config(config));
        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void UnknownFormat_IsRejected()
    {
        var id = Guid.NewGuid();
        var handler = Handler(id, "a\n1");
        var errors = handler.ValidateConfig(Config($"{{\"fileId\":\"{id}\",\"format\":\"xml\"}}"));
        Assert.Contains(errors, e => e.Contains("csv or json"));
    }

    [Fact]
    public void MultiCharacterDelimiter_IsRejected()
    {
        var id = Guid.NewGuid();
        var handler = Handler(id, "a\n1");
        var errors = handler.ValidateConfig(Config($"{{\"fileId\":\"{id}\",\"delimiter\":\"::\"}}"));
        Assert.Contains(errors, e => e.Contains("single character"));
    }

    [Fact]
    public void ValidConfig_HasNoErrors()
    {
        var id = Guid.NewGuid();
        var handler = Handler(id, "a\n1");
        var errors = handler.ValidateConfig(
            Config($"{{\"fileId\":\"{id}\",\"format\":\"csv\",\"delimiter\":\",\",\"hasHeader\":true}}"));
        Assert.Empty(errors);
    }
}
