using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.PipelineExecution.Features.CancelRun;
using Reflow.Modules.PipelineExecution.Features.GetRun;
using Reflow.Modules.PipelineExecution.Features.RetryTask;
using Reflow.Modules.PipelineExecution.Features.RunTasks;
using Reflow.Modules.PipelineExecution.Features.StartRun;
using Reflow.Modules.PipelineExecution.Persistence;
using Reflow.Modules.PipelineExecution.Worker;

namespace Reflow.Modules.PipelineExecution;

public static class PipelineExecutionModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Reflow");
        builder.Services.AddDbContext<PipelineExecutionDbContext>(options =>
            options.UseNpgsql(connectionString));
        builder.Services.AddScoped<PipelineExecutionDbContext>();
        builder.Services.AddScoped<PipelineRunStarter>();
        builder.Services.AddScoped<CancelRunHandler>();
        builder.Services.AddScoped<RetryTaskHandler>();
        builder.Services.AddHostedService<ExecutionWorker>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        StartRunEndpoint.Map(app);
        GetRunEndpoint.Map(app);
        CancelRunEndpoint.Map(app);
        RunTasksEndpoints.Map(app);
        RetryTaskEndpoint.Map(app);
    }
}
