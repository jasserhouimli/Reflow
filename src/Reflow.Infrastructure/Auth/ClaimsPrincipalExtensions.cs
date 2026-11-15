using System.Security.Claims;

namespace Reflow.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }

    public static Guid? GetUserId(this ClaimsPrincipal? principal) =>
        principal.TryGetUserId(out var userId) ? userId : null;
}
