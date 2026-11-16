namespace Reflow.Modules.Identity.Features.GoogleLogin;

public record GoogleLoginRequest(
    string? IdToken = null,
    string? AccessToken = null
);
