using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Reflow.Infrastructure.Results;
using Reflow.Modules.Identity.Domain;
using Reflow.Modules.Identity.Features.Login;
using Reflow.Modules.Identity.Services;

namespace Reflow.Modules.Identity.Features.GoogleLogin;

public class GoogleLoginHandler(
    UserManager<User> userManager,
    TokenService tokenService,
    IGoogleTokenValidator googleTokenValidator,
    ILogger<GoogleLoginHandler> logger)
{
    public async Task<Result<LoginResponse>> Handle(
        GoogleLoginRequest request,
        CancellationToken ct)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await googleTokenValidator.ValidateAsync(request.IdToken, ct);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Invalid Google ID token supplied.");
            return Result<LoginResponse>.Failure("Invalid Google token.", 401);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error validating Google ID token.");
            return Result<LoginResponse>.Failure("Authentication with Google failed.", 401);
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
        {
            return Result<LoginResponse>.Failure("Google account does not provide an email address.", 400);
        }

        if (!payload.EmailVerified)
        {
            return Result<LoginResponse>.Failure("Google email address is not verified.", 400);
        }

        // 1. Check if user has previously linked this Google Subject ID
        var user = await userManager.FindByLoginAsync("Google", payload.Subject);

        if (user is null)
        {
            // 2. Check if a user with this email already exists
            user = await userManager.FindByEmailAsync(payload.Email);

            if (user is not null)
            {
                // Account exists; link this Google login provider key
                var linkResult = await userManager.AddLoginAsync(user, new UserLoginInfo("Google", payload.Subject, "Google"));
                if (!linkResult.Succeeded)
                {
                    logger.LogWarning("Failed to attach Google login to existing user {UserId}: {Errors}",
                        user.Id, string.Join(", ", linkResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                // 3. User does not exist, auto-provision
                var displayName = !string.IsNullOrWhiteSpace(payload.Name)
                    ? payload.Name
                    : payload.Email.Split('@')[0];

                user = new User
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    FullName = displayName,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create user from Google login: {Errors}", errors);
                    return Result<LoginResponse>.Failure($"Failed to create user: {errors}", 500);
                }

                await userManager.AddLoginAsync(user, new UserLoginInfo("Google", payload.Subject, "Google"));
            }
        }

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(Guid.Parse(user.Id), ct);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            refreshToken.Token,
            Guid.Parse(user.Id),
            user.Email!,
            user.FullName));
    }
}
