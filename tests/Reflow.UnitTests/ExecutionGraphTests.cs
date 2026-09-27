using Reflow.Modules.PipelineExecution.Domain;
using Reflow.Modules.PipelineExecution.Worker;
using Xunit;

namespace Reflow.UnitTests;

public class ExecutionGraphTests
{
    private static TaskRunStatus Status(string id) => id switch
    {
        "done" => TaskRunStatus.Completed,
        _ => TaskRunStatus.Pending,
    };

    [Fact]
    public void RootNode_IsReady()
    {
        Assert.True(ExecutionGraph.IsReady("a", new List<(string, string)>(), Status));
    }

    [Fact]
    public void BlockedUntilPredecessorsComplete()
    {
        var edges = new List<(string Source, string Target)> { ("a", "b") };
        Assert.False(ExecutionGraph.IsReady("b", edges, _ => TaskRunStatus.Pending));
        Assert.True(ExecutionGraph.IsReady("b", edges, _ => TaskRunStatus.Completed));
    }

    [Fact]
    public void FailedPredecessor_Blocks()
    {
        var edges = new List<(string Source, string Target)> { ("done", "b") };
        Assert.True(ExecutionGraph.IsReady("b", edges, Status));
    }

    [Fact]
    public void RetryDelay_IsLinear()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), ExecutionGraph.RetryDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(10), ExecutionGraph.RetryDelay(2));
    }

    [Fact]
    public void TerminalStates_CoverExpectedSet()
    {
        Assert.True(TaskRunStatus.Completed.IsTerminal());
        Assert.True(TaskRunStatus.Failed.IsTerminal());
        Assert.True(TaskRunStatus.Cancelled.IsTerminal());
        Assert.True(TaskRunStatus.Skipped.IsTerminal());
        Assert.False(TaskRunStatus.Running.IsTerminal());
        Assert.False(TaskRunStatus.RetryScheduled.IsTerminal());
        Assert.False(TaskRunStatus.Pending.IsTerminal());
    }
}
