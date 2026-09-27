using Reflow.Modules.DataProcessing.Abstractions;
using Reflow.Modules.DataProcessing.InMemory;
using Xunit;

namespace Reflow.UnitTests;

public class DataProcessingTests
{
    private readonly CsvCodec _reader = new();
    private readonly InMemoryQueryEngine _engine = new();
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
    public void Filter_EqualsAndContains()
    {
        var frame = _reader.FromCsv("name,age\nAda,36\nGrace,85", ',', true);
        Assert.Single(_engine.Filter(frame, "name", "equals", "Ada").Rows);
        Assert.Equal(2, _engine.Filter(frame, "name", "contains", "a").Rows.Count);
    }

    [Fact]
    public void Filter_UnknownColumn_Throws()
    {
        var frame = _reader.FromCsv("a\n1", ',', true);
        Assert.Throws<InvalidOperationException>(
            () => _engine.Filter(frame, "nope", "equals", "1"));
    }

    [Fact]
    public void Select_And_Limit_SliceRows()
    {
        var frame = _reader.FromCsv("a,b,c\n1,2,3\n4,5,6", ',', true);
        var selected = _engine.Select(frame, new[] { "c", "a" });
        Assert.Equal(new[] { "c", "a" }, selected.Columns);
        var limited = _engine.Limit(frame, 1, 1);
        Assert.Single(limited.Rows);
        Assert.Equal("4", limited.Rows[0][0]);
    }

    [Fact]
    public void Writer_RoundTripsJson()
    {
        var frame = _reader.FromCsv("a,b\n1,2", ',', true);
        var json = _writer.ToJson(frame);
        Assert.Contains("\"a\"", json);
    }
}
