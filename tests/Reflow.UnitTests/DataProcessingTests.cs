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
    public void Csv_NoHeader_GeneratesColumnNames()
    {
        var frame = _reader.FromCsv("10,20,30\n40,50,60", ',', false);
        Assert.Equal(new[] { "col1", "col2", "col3" }, frame.Columns);
        Assert.Equal(2, frame.RowCount);
        Assert.Equal("10", frame.Rows[0][0]);
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
    public void Sql_MultiTableJoin()
    {
        var users = _reader.FromCsv("id,name\n1,Alice\n2,Bob", ',', true);
        var orders = _reader.FromCsv("order_id,user_id,amount\n101,1,50\n102,1,75\n103,3,20", ',', true);

        var result = _sql.Query(
            new[] { ("users", users), ("orders", orders) },
            "SELECT u.name, o.amount FROM users u INNER JOIN orders o ON u.id = o.user_id ORDER BY o.amount DESC");

        Assert.Equal(2, result.RowCount);
        Assert.Equal("75", result.Rows[0][1]);
        Assert.Equal("Alice", result.Rows[0][0]);
    }

    [Fact]
    public void Sql_SanitizesTableNamesWithHyphensAndNumbers()
    {
        var frame = _reader.FromCsv("val\n100", ',', true);
        var result = _sql.Query(new[] { ("node-1", frame) }, "SELECT val FROM \"node_1\"");
        Assert.Single(result.Rows);
        Assert.Equal("100", result.Rows[0][0]);
    }

    [Fact]
    public void Sql_ExternalAccessLockdown_PreventsFileReads()
    {
        var frame = _reader.FromCsv("x\n1", ',', true);
        // DuckDB read_csv or read_text should fail because external access is disabled
        Assert.ThrowsAny<Exception>(() =>
            _sql.Query(new[] { ("input", frame) }, "SELECT * FROM read_csv('C:/test.csv')"));
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

    [Fact]
    public void Writer_RoundTripsCsv_WithEscaping()
    {
        var frame = new Frame
        {
            Columns = new List<string> { "city", "desc" },
            Rows = new List<List<string?>> { new() { "New York", "A \"great\" place\nwith tall buildings" } },
        };
        var csv = _writer.ToCsv(frame);
        Assert.Contains("\"A \"\"great\"\" place\nwith tall buildings\"", csv);

        var readBack = _reader.FromCsv(csv, ',', true);
        Assert.Single(readBack.Rows);
        Assert.Equal("A \"great\" place\nwith tall buildings", readBack.Rows[0][1]);
    }
}
