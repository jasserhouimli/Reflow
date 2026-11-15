using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Reflow.Modules.Identity.Features.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/auth/refresh", async (
            HttpContext http,
            RefreshTokenHandler handler,
            CancellationToken ct) =>
        {
            http.Request.Cookies.TryGetValue("Reflow.RefreshToken", out var refreshToken);

            if (string.IsNullOrEmpty(refreshToken))
                return Results.Json(new { error = "Refresh token not found" }, statusCode: 401);

            var result = await handler.Handle(new RefreshTokenRequest(refreshToken), ct);

            if (!result.IsSuccess)
                return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);

            var isDev = http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
            var sameSiteMode = isDev ? SameSiteMode.Lax : SameSiteMode.None;

            var accessCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = sameSiteMode,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            };

            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = sameSiteMode,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/api/v1/auth/refresh"
            };

            http.Response.Cookies.Append("Reflow.Token", result.Value!.AccessToken, accessCookieOptions);
            http.Response.Cookies.Append("Reflow.RefreshToken", result.Value.RefreshToken, refreshCookieOptions);

            return Results.Ok(result.Value);
        })
        .WithName("RefreshToken")
        .RequireRateLimiting("auth")
        .Produces(200)
        .Produces(401);
    }
}
