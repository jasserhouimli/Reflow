using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Reflow.Modules.PipelineExecution.Domain;
using Reflow.Modules.PipelineExecution.Features.StartRun;
using Reflow.Modules.Triggers.Domain;
using Reflow.Modules.Triggers.Persistence;

namespace Reflow.Modules.Triggers.Worker;

public class SchedulerWorker(
    IServiceProvider services,
    IConfiguration config,
    ILogger<SchedulerWorker> logger) : BackgroundService
{
    public const int MaxQueuedRuns = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = config.GetValue("Triggers:TickSeconds", 30);
        tick = Math.Clamp(tick, 2, 600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(tick));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await FireDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduler tick failed");
            }
        }
    }

    private async Task FireDueAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TriggersDbContext>();
        var now = DateTime.UtcNow;

        var due = await db.Triggers
            .Where(t => t.Kind == TriggerKind.Schedule && t.IsEnabled && t.NextRunAt <= now)
            .ToListAsync(ct);

        foreach (var trigger in due)
        {
            if (ct.IsCancellationRequested)
                return;
            await FireOneAsync(trigger.Id, ct);
        }
    }

    internal async Task FireOneAsync(Guid triggerId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TriggersDbContext>();
        var starter = scope.ServiceProvider.GetRequiredService<IPipelineRunStarter>();
        var monitor = scope.ServiceProvider.GetRequiredService<IRunMonitor>();

        var trigger = await db.Triggers.FirstOrDefaultAsync(t => t.Id == triggerId, ct);
        if (trigger is null || !trigger.IsEnabled || trigger.Cron is null || trigger.Timezone is null)
            return;

        var next = Services.CronSchedule.NextOccurrence(trigger.Cron, trigger.Timezone, DateTime.UtcNow);
        if (next is null)
        {
            trigger.IsEnabled = false;
            trigger.NextRunAt = null;
            await db.SaveChangesAsync(ct);
            return;
        }
        trigger.NextRunAt = next;
        trigger.LastFiredAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var active = await monitor.CountActiveRunsAsync(trigger.PipelineId, ct);
        if (trigger.Overlap == OverlapPolicy.Skip && active > 0)
            return;
        if (active >= MaxQueuedRuns)
            return;

        var result = await starter.StartRunAsync(
            trigger.PipelineId, trigger.OwnerId, "schedule", null, ct);
        if (!result.IsSuccess)
            logger.LogWarning("Scheduled start failed for {Trigger}: {Error}", trigger.Id, result.Error);
    }
}
