using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Reflow.Modules.Identity.Domain;
using Reflow.Modules.Identity.Options;
using Reflow.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Reflow.Modules.Identity.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IdentityDbContext _db;

    public TokenService(IOptions<JwtOptions> options, IdentityDbContext db)
    {
        _options = options.Value;
        _db = db;
    }

    // Protected constructor for test doubles
    protected TokenService()
    {
        _options = new JwtOptions();
        _db = null!;
    }

    public virtual string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.Key));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expirationMinutes = _options.AccessTokenExpirationMinutes;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public virtual async Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var expirationDays = _options.RefreshTokenExpirationDays;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return refreshToken;
    }

    public virtual async Task<RefreshToken?> ValidateRefreshTokenAsync(string token, CancellationToken ct)
    {
        var refreshToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);

        if (refreshToken is null || !refreshToken.IsActive)
            return null;

        return refreshToken;
    }

    public virtual async Task RevokeRefreshTokenAsync(string token, CancellationToken ct)
    {
        var refreshToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);

        if (refreshToken is not null)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
