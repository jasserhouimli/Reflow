using System.Text.Json;
using Reflow.Modules.AiAssistant.Contracts;
using Xunit;

namespace Reflow.UnitTests;

public class WorkflowDraftParserTests
{
    [Fact]
    public void ParsesCanonicalDraft()
    {
        var (draft, error) = WorkflowDraftParser.Parse("""
            {
              "name": "Orders",
              "explanation": "Reads and filters orders",
              "nodes": [
                { "nodeId": "source", "nodeType": "csv.read", "config": { "csvText": "a\n1" } },
                { "nodeId": "filtered", "nodeType": "filter",
                  "config": { "column": "a", "operator": "equals", "value": "1" } }
              ],
              "edges": [ { "sourceNodeId": "source", "targetNodeId": "filtered" } ],
              "warnings": [ "Assumed comma delimiter" ]
            }
            """);

        Assert.Null(error);
        Assert.NotNull(draft);
        Assert.Equal("Orders", draft.Name);
        Assert.Equal(2, draft.Nodes.Count);
        Assert.Single(draft.Edges);
        Assert.Single(draft.Warnings);
        Assert.Equal("csv.read", draft.Nodes[0].NodeType);
        Assert.Equal(JsonValueKind.Object, draft.Nodes[0].Config.ValueKind);
    }

    [Fact]
    public void AcceptsAliasesUsedByModels()
    {
        var (draft, error) = WorkflowDraftParser.Parse("""
            {
              "nodes": [ { "id": "src", "type": "csv.read", "config": {} } ],
              "edges": [ ]
            }
            """);

        Assert.Null(error);
        Assert.Equal("src", draft!.Nodes[0].NodeId);
        Assert.Equal("Generated workflow", draft.Name);
    }

    [Fact]
    public void StripsMarkdownCodeFences()
    {
        var (draft, error) = WorkflowDraftParser.Parse("""
            ```json
            { "nodes": [ { "nodeId": "src", "nodeType": "csv.read", "config": {} } ] }
            ```
            """);

        Assert.Null(error);
        Assert.Single(draft!.Nodes);
    }

    [Fact]
    public void DecodesDoubleEncodedConfigString()
    {
        var (draft, error) = WorkflowDraftParser.Parse("""
            {
              "nodes": [
                { "nodeId": "src", "nodeType": "csv.read", "config": "{\"csvText\":\"a\\n1\"}" }
              ]
            }
            """);

        Assert.Null(error);
        Assert.Equal("a\n1", draft!.Nodes[0].Config.GetProperty("csvText").GetString());
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("not json", "not valid JSON")]
    [InlineData("[]", "JSON object")]
    [InlineData("{}", "non-empty 'nodes'")]
    [InlineData("""{ "nodes": [] }""", "non-empty 'nodes'")]
    public void RejectsUnusableOutput(string raw, string expectedMessage)
    {
        var (draft, error) = WorkflowDraftParser.Parse(raw);
        Assert.Null(draft);
        Assert.Contains(expectedMessage, error);
    }

    [Fact]
    public void RejectsNodeWithoutConfigObject()
    {
        var (draft, error) = WorkflowDraftParser.Parse("""
            { "nodes": [ { "nodeId": "src", "nodeType": "csv.read", "config": 42 } ] }
            """);

        Assert.Null(draft);
        Assert.Contains("config must be a JSON object", error);
    }

    [Fact]
    public void RejectsEdgeWithoutEndpoints()
    {
        var (draft, error) = WorkflowDraftParser.Parse("""
            {
              "nodes": [ { "nodeId": "src", "nodeType": "csv.read", "config": {} } ],
              "edges": [ { "sourceNodeId": "src" } ]
            }
            """);

        Assert.Null(draft);
        Assert.Contains("sourceNodeId", error);
    }
}
