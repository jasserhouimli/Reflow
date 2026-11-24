using Reflow.Infrastructure.Results;
using Reflow.Modules.Identity.Domain;
using Reflow.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace Reflow.Modules.Identity.Features.RefreshToken;

public class RefreshTokenHandler(
    UserManager<User> userManager,
    ITokenService tokenService)
{
    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenRequest request,
        CancellationToken ct)
    {
        var storedToken = await tokenService.ValidateRefreshTokenAsync(request.RefreshToken, ct);

        if (storedToken is null)
            return Result<RefreshTokenResponse>.Failure("Invalid or expired refresh token", 401);

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString());

        if (user is null)
            return Result<RefreshTokenResponse>.Failure("User not found", 401);

        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);

        var newAccessToken = tokenService.GenerateAccessToken(user);
        var newRefreshToken = await tokenService.GenerateRefreshTokenAsync(storedToken.UserId, ct);

        return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse(newAccessToken, newRefreshToken.Token));
    }
}
