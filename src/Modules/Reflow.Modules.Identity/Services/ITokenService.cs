using Reflow.Modules.Identity.Domain;

namespace Reflow.Modules.Identity.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token, CancellationToken ct);
    Task RevokeRefreshTokenAsync(string token, CancellationToken ct);
}
