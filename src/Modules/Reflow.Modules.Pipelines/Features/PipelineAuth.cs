using System.Security.Claims;
using Reflow.Infrastructure.Auth;

namespace Reflow.Modules.Pipelines.Features;

internal static class PipelineAuth
{
    public static bool TryOwnerId(ClaimsPrincipal user, out Guid ownerId) =>
        user.TryGetUserId(out ownerId);
}
