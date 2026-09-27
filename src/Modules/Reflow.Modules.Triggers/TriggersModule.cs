using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.Triggers.Features.Schedules;
using Reflow.Modules.Triggers.Features.Webhooks;
using Reflow.Modules.Triggers.Persistence;
using Reflow.Modules.Triggers.Worker;

namespace Reflow.Modules.Triggers;

public static class TriggersModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Reflow");
        builder.Services.AddDbContext<TriggersDbContext>(options =>
            options.UseNpgsql(connectionString));
        builder.Services.AddScoped<TriggersDbContext>();
        builder.Services.AddScoped<ScheduleHandler>();
        builder.Services.AddScoped<WebhookHandler>();
        builder.Services.AddScoped<ReceiveWebhookHandler>();
        builder.Services.AddHostedService<SchedulerWorker>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        ScheduleEndpoints.Map(app);
        WebhookEndpoints.Map(app);
    }
}
