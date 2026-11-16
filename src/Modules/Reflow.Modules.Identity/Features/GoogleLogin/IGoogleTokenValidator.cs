namespace Reflow.Modules.Identity.Features.GoogleLogin;

public record GoogleUserInfo(
    string Subject,
    string Email,
    bool EmailVerified,
    string Name
);

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo> ValidateAsync(GoogleLoginRequest request, CancellationToken ct = default);
}
