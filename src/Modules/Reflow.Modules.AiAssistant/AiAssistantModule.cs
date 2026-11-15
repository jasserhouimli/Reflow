using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reflow.Modules.AiAssistant.Abstractions;
using Reflow.Modules.AiAssistant.Features.GenerateWorkflow;
using Reflow.Modules.AiAssistant.LanguageModels;
using Reflow.Modules.AiAssistant.Options;

namespace Reflow.Modules.AiAssistant;

public static class AiAssistantModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.Configure<AiOptions>(
            builder.Configuration.GetSection(AiOptions.SectionName));

        builder.Services.AddSingleton<PromptBuilder>();
        builder.Services.AddSingleton<WorkflowDraftValidator>();
        // Scoped: it holds an HttpClient from the typed-client factory.
        builder.Services.AddScoped<WorkflowGenerator>();
        builder.Services.AddScoped<GenerateWorkflowHandler>();

        builder.Services.AddHttpClient<ILanguageModel, OpenAiCompatibleLanguageModel>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 120));
        });
    }

    public static void MapEndpoints(WebApplication app)
    {
        GenerateWorkflowEndpoint.Map(app);
    }
}
