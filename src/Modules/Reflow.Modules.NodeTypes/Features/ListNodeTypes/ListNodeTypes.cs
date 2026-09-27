using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Reflow.Modules.NodeTypes.Registry;

namespace Reflow.Modules.NodeTypes.Features.ListNodeTypes;

public static class ListNodeTypesEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/node-types", (
            NodeTypeRegistry registry) =>
        {
            return Results.Ok(registry.Definitions());
        }).WithName("ListNodeTypes");
    }
}
