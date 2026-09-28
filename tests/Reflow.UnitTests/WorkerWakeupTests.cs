using Reflow.Modules.PipelineExecution.Worker;
using Xunit;

namespace Reflow.UnitTests;

public class WorkerWakeupTests
{
    [Fact]
    public async Task Pulse_WakesWaiterImmediately()
    {
        var wakeup = new WorkerWakeup();
        wakeup.Pulse();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await wakeup.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NoPulse_WaitsForFallback()
    {
        var wakeup = new WorkerWakeup();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await wakeup.WaitAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public async Task DoublePulse_DoesNotThrowOrAccumulate()
    {
        var wakeup = new WorkerWakeup();
        wakeup.Pulse();
        wakeup.Pulse();
        await wakeup.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await wakeup.WaitAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(150));
    }
}
