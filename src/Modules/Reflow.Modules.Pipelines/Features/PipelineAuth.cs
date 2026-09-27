using System.Security.Claims;

namespace Reflow.Modules.Pipelines.Features;

internal static class PipelineAuth
{
    public static bool TryOwnerId(ClaimsPrincipal user, out Guid ownerId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out ownerId);
}
