using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.Pipelines.Features.ArchivePipeline;
using Reflow.Modules.Pipelines.Features.CreatePipeline;
using Reflow.Modules.Pipelines.Features.DeletePipeline;
using Reflow.Modules.Pipelines.Features.GetPipeline;
using Reflow.Modules.Pipelines.Features.ListPipelines;
using Reflow.Modules.Pipelines.Features.PublishPipeline;
using Reflow.Modules.Pipelines.Features.UpdatePipeline;
using Reflow.Modules.Pipelines.Features.ValidatePipeline;
using Reflow.Modules.Pipelines.Features.Versions;
using Reflow.Modules.PipelineExecution.Snapshots;
using Reflow.Modules.Pipelines.Features.Snapshots;
using Reflow.Modules.Pipelines.Persistence;

namespace Reflow.Modules.Pipelines;

public static class PipelinesModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Reflow");
        builder.Services.AddDbContext<PipelinesDbContext>(options =>
            options.UseNpgsql(connectionString));
        builder.Services.AddScoped<PipelinesDbContext>();
        builder.Services.AddValidatorsFromAssemblyContaining<PipelinesDbContext>();
        builder.Services.AddScoped<CreatePipelineHandler>();
        builder.Services.AddScoped<UpdatePipelineHandler>();
        builder.Services.AddScoped<PublishPipelineHandler>();
        builder.Services.AddScoped<IPipelineSnapshotProvider, PipelineSnapshotProvider>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        CreatePipelineEndpoint.Map(app);
        ListPipelinesEndpoint.Map(app);
        GetPipelineEndpoint.Map(app);
        UpdatePipelineEndpoint.Map(app);
        DeletePipelineEndpoint.Map(app);
        ValidatePipelineEndpoint.Map(app);
        PublishPipelineEndpoint.Map(app);
        ArchivePipelineEndpoint.Map(app);
        VersionEndpoints.Map(app);
    }
}
