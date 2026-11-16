using System.Text.Json;
using Reflow.Infrastructure.Files;
using Reflow.Modules.AiAssistant.Features.GenerateWorkflow;
using Reflow.Modules.NodeTypes.Abstractions;
using Reflow.Modules.NodeTypes.CsvRead;
using Reflow.Modules.NodeTypes.DataOutput;
using Reflow.Modules.NodeTypes.FileRead;
using Reflow.Modules.NodeTypes.Filter;
using Reflow.Modules.NodeTypes.Registry;
using Reflow.Modules.NodeTypes.Transform;
using Xunit;

namespace Reflow.UnitTests;

public class WorkflowDraftValidatorTests
{
    private static readonly NodeTypeRegistry Registry = new(new INodeHandler[]
    {
        // Only metadata and ValidateConfig are exercised; execution
        // dependencies are irrelevant here.
        new CsvReadHandler(null!),
        new FileReadHandler(null!, null!),
        new FilterHandler(null!),
        new TransformHandler(null!),
        new DataOutputHandler(null!),
    });

    private static readonly WorkflowDraftValidator Validator = new(Registry);

    private static JsonElement Json(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static DraftNode CsvNode(string id = "source") =>
        new(id, "csv.read", null, Json("""{ "csvText": "a\n1" }"""));

    private static DraftNode FileReadNode(string id = "source", string fileId = "00000000-0000-0000-0000-000000000001") =>
        new(id, "file.read", null, Json($$"""{ "fileId": "{{fileId}}", "format": "csv" }"""));

    [Fact]
    public void AcceptsGraphThatPassesEveryNodeValidator()
    {
        var draft = new WorkflowDraft(
            "Orders",
            "Filter then output",
            [
                CsvNode(),
                new("filtered", "filter", null,
                    Json("""{ "column": "a", "operator": "equals", "value": "1" }""")),
                new("output", "data.output", null, Json("""{ "format": "json" }""")),
            ],
            [new("source", "filtered"), new("filtered", "output")],
            []);

        Assert.Empty(Validator.Validate(draft, 20));
    }

    [Fact]
    public void RejectsUnknownNodeType()
    {
        var draft = new WorkflowDraft(
            "Bad", "",
            [new("source", "http.request", null, Json("{}"))],
            [], []);

        var errors = Validator.Validate(draft, 20);

        Assert.Contains(errors, e => e.Contains("unknown node type 'http.request'"));
    }

    [Fact]
    public void UsesTheNodesOwnValidatorForConfig()
    {
        var draft = new WorkflowDraft(
            "Bad", "",
            [new("filtered", "filter", null, Json("""{ "operator": "equals" }"""))],
            [], []);

        var errors = Validator.Validate(draft, 20);

        Assert.Contains(errors, e => e.Contains("node 'filtered' (filter)") && e.Contains("column"));
    }

    [Theory]
    [InlineData("My Node")]
    [InlineData("1source")]
    [InlineData("source-dash")]
    [InlineData("")]
    public void RejectsIdsThatAreNotSlugs(string id)
    {
        var draft = new WorkflowDraft("Bad", "", [CsvNode(id)], [], []);

        var errors = Validator.Validate(draft, 20);

        Assert.Contains(errors, e => e.Contains("must be a lowercase slug"));
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        var draft = new WorkflowDraft(
            "Bad", "", [CsvNode(), CsvNode()], [], []);

        var errors = Validator.Validate(draft, 20);

        Assert.Contains(errors, e => e.Contains("used more than once"));
    }

    [Fact]
    public void RejectsCycle()
    {
        var config = Json("""{ "select": ["a"] }""");
        var draft = new WorkflowDraft(
            "Bad", "",
            [
                CsvNode(),
                new("first", "transform", null, config),
                new("second", "transform", null, config),
            ],
            [new("source", "first"), new("first", "second"), new("second", "first")],
            []);

        var errors = Validator.Validate(draft, 20);

        Assert.Contains(errors, e => e.Contains("cycle"));
    }

    [Fact]
    public void RejectsEdgeToUnknownNode()
    {
        var draft = new WorkflowDraft(
            "Bad", "", [CsvNode()], [new("source", "ghost")], []);

        var errors = Validator.Validate(draft, 20);

        Assert.Contains(errors, e => e.Contains("unknown target node 'ghost'"));
    }

    [Fact]
    public void RejectsTooManyNodes()
    {
        var draft = new WorkflowDraft(
            "Big", "",
            [CsvNode("source"), CsvNode("other")],
            [], []);

        var errors = Validator.Validate(draft, 1);

        Assert.Contains(errors, e => e.Contains("maximum is 1"));
    }

    [Fact]
    public void RejectsEmptyGraph()
    {
        var errors = Validator.Validate(new WorkflowDraft("Empty", "", [], [], []), 20);

        Assert.Contains(errors, e => e.Contains("no nodes"));
    }

    [Fact]
    public void RejectsFileReadWhenNoAvailableFiles()
    {
        var draft = new WorkflowDraft("WithFile", "", [FileReadNode()], [], []);
        var errors = Validator.Validate(draft, 20, Array.Empty<SourceFileRef>());

        Assert.Contains(errors, e => e.Contains("AVAILABLE FILES is empty"));
    }

    [Fact]
    public void RejectsFileReadWhenFileIdNotInAvailableFiles()
    {
        var validId = Guid.NewGuid();
        var files = new[] { new SourceFileRef(validId, "orders.csv") };
        var draft = new WorkflowDraft("WithFile", "", [FileReadNode(fileId: Guid.NewGuid().ToString())], [], []);
        var errors = Validator.Validate(draft, 20, files);

        Assert.Contains(errors, e => e.Contains("is not in AVAILABLE FILES"));
    }

    [Fact]
    public void AcceptsFileReadWhenFileIdMatchesAvailableFile()
    {
        var validId = Guid.NewGuid();
        var files = new[] { new SourceFileRef(validId, "orders.csv") };
        var draft = new WorkflowDraft(
            "WithFile", "",
            [
                FileReadNode(fileId: validId.ToString()),
                new("out", "data.output", null, Json("{}")),
            ],
            [new("source", "out")],
            []);
        var errors = Validator.Validate(draft, 20, files);

        Assert.Empty(errors);
    }
}
