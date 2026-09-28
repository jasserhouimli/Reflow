namespace Reflow.Modules.PipelineExecution.Worker;

/// <summary>
/// Best-effort wake-up for the execution worker. Pulses on run start,
/// task completion and manual retry so work starts immediately; the 2s
/// poll remains the correctness net if a pulse is ever missed.
/// </summary>
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
