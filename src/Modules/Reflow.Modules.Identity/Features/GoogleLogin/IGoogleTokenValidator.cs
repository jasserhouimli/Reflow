using Google.Apis.Auth;

namespace Reflow.Modules.Identity.Features.GoogleLogin;

public interface IGoogleTokenValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, CancellationToken ct = default);
}
