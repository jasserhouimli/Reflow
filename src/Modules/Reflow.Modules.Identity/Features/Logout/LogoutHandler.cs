using Reflow.Infrastructure.Results;
using Reflow.Modules.Identity.Services;
using Microsoft.AspNetCore.Http;

namespace Reflow.Modules.Identity.Features.Logout;

public class LogoutHandler(ITokenService tokenService)
{
    public async Task<Result<LogoutResponse>> Handle(
        LogoutRequest request,
        HttpContext http,
        CancellationToken ct)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        http.Response.Cookies.Delete("Reflow.Token");

        return Result<LogoutResponse>.Success(new LogoutResponse("Logged out successfully"));
    }
}

public record LogoutResponse(string Message);
