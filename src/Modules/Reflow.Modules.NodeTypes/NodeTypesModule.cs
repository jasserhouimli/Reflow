using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Reflow.Modules.DataProcessing;
using Reflow.Modules.NodeTypes.Abstractions;
using Reflow.Modules.NodeTypes.CsvRead;
using Reflow.Modules.NodeTypes.Aggregate;
using Reflow.Modules.NodeTypes.DataSql;
using Reflow.Modules.NodeTypes.DataOutput;
using Reflow.Modules.NodeTypes.Join;
using Reflow.Modules.NodeTypes.Sort;
using Reflow.Modules.NodeTypes.Filter;
using Reflow.Modules.NodeTypes.JsonRead;
using Reflow.Modules.NodeTypes.Transform;
using Reflow.Modules.NodeTypes.TriggerPayload;
using Reflow.Modules.NodeTypes.Features.ListNodeTypes;
using Reflow.Modules.NodeTypes.Registry;

namespace Reflow.Modules.NodeTypes;

public static class NodeTypesModule
{
    public static void Register(WebApplicationBuilder builder)
    {
        DataProcessingModule.Register(builder);
        builder.Services.AddSingleton<NodeTypeRegistry>();
        builder.Services.AddSingleton<INodeHandler, CsvReadHandler>();
        builder.Services.AddSingleton<INodeHandler, DataSqlHandler>();
        builder.Services.AddSingleton<INodeHandler, AggregateHandler>();
        builder.Services.AddSingleton<INodeHandler, SortHandler>();
        builder.Services.AddSingleton<INodeHandler, JoinHandler>();
        builder.Services.AddSingleton<INodeHandler, DataOutputHandler>();
        builder.Services.AddSingleton<INodeHandler, JsonReadHandler>();
        builder.Services.AddSingleton<INodeHandler, FilterHandler>();
        builder.Services.AddSingleton<INodeHandler, TransformHandler>();
        builder.Services.AddSingleton<INodeHandler, TriggerPayloadHandler>();
    }

    public static void MapEndpoints(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<NodeTypeRegistry>();
        foreach (var handler in scope.ServiceProvider.GetServices<INodeHandler>())
            registry.Register(handler);

        ListNodeTypesEndpoint.Map(app);
    }
}
