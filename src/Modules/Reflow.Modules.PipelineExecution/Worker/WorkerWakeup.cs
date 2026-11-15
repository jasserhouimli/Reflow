namespace Reflow.Modules.PipelineExecution.Worker;

// Signals the background worker to claim ready tasks immediately without waiting for poll intervals.
public sealed class WorkerWakeup
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Pulse()
    {
        if (_signal.CurrentCount == 0)
        {
            try { _signal.Release(); }
            catch (SemaphoreFullException) { }
        }
    }

    public async Task WaitAsync(TimeSpan fallback, CancellationToken ct)
    {
        try
        {
            await _signal.WaitAsync(fallback, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
