using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Reflow.Modules.Identity.Features.Logout;

public static class LogoutEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/auth/logout", async (
            HttpContext http,
            LogoutHandler handler,
            CancellationToken ct) =>
        {
            http.Request.Cookies.TryGetValue("Reflow.RefreshToken", out var refreshToken);

            if (string.IsNullOrEmpty(refreshToken))
                return Results.Ok(new { message = "Logged out successfully" });

            var result = await handler.Handle(new LogoutRequest(refreshToken), http, ct);

            var isDev = http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
            var sameSiteMode = isDev ? SameSiteMode.Lax : SameSiteMode.None;

            http.Response.Cookies.Delete("Reflow.Token", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = sameSiteMode
            });
            http.Response.Cookies.Delete("Reflow.RefreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = sameSiteMode,
                Path = "/api/v1/auth/refresh"
            });

            return Results.Ok(new { message = "Logged out successfully" });
        })
        .WithName("Logout")
        .RequireRateLimiting("auth")
        .Produces(200);
    }
}
