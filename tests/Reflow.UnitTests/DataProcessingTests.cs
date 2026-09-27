using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.DuckDb;
using Reflow.Modules.DataProcessing.InMemory;
using Xunit;

namespace Reflow.UnitTests;

public class DataProcessingTests
{
    private readonly CsvCodec _reader = new();
    private readonly DuckDbSqlEngine _sql = new();
    private readonly FrameWriter _writer = new();

    [Fact]
    public void Csv_ParsesHeaderAndQuotedCommas()
    {
        var frame = _reader.FromCsv("name,note\n\"Doe, Ada\",hi", ',', true);
        Assert.Equal(new[] { "name", "note" }, frame.Columns);
        Assert.Equal("Doe, Ada", frame.Rows[0][0]);
    }

    [Fact]
    public void Csv_DuplicateHeader_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => _reader.FromCsv("a,A\n1,2", ',', true));
    }

    [Fact]
    public void Csv_UnterminatedQuote_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => _reader.FromCsv("a\n\"oops", ',', true));
    }

    [Fact]
    public void Csv_SupportsCustomDelimiter()
    {
        var frame = _reader.FromCsv("a;b\n1;2", ';', true);
        Assert.Equal("2", frame.Rows[0][1]);
    }

    [Fact]
    public void Json_NavigatesRootPath()
    {
        var frame = _reader.FromJson("{\"data\":{\"orders\":[{\"id\":7}]}}", "data.orders");
        Assert.Equal(new[] { "id" }, frame.Columns);
        Assert.Equal("7", frame.Rows[0][0]);
    }

    [Fact]
    public void Json_BadPath_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => _reader.FromJson("{\"a\":1}", "missing.path"));
    }

    [Fact]
    public void Sql_FiltersAndProjects()
    {
        var frame = _reader.FromCsv("name,age\nAda,36\nGrace,85", ',', true);
        var filtered = _sql.Query(new[] { ("input", frame) },
            "SELECT * FROM input WHERE age = 'Ada' OR name = 'Ada'");
        Assert.Single(filtered.Rows);
        var projected = _sql.Query(new[] { ("input", frame) },
            "SELECT age FROM input LIMIT 1 OFFSET 1");
        Assert.Equal(new[] { "age" }, projected.Columns);
        Assert.Equal("85", projected.Rows[0][0]);
    }

    [Fact]
    public void Sql_UnknownColumn_Throws()
    {
        var frame = _reader.FromCsv("a\n1", ',', true);
        Assert.ThrowsAny<Exception>(() => _sql.Query(
            new[] { ("input", frame) }, "SELECT * FROM input WHERE nope = '1'"));
    }

    [Fact]
    public void Writer_RoundTripsJson()
    {
        var frame = _reader.FromCsv("a,b\n1,2", ',', true);
        var json = _writer.ToJson(frame);
        Assert.Contains("\"a\"", json);
    }
}
