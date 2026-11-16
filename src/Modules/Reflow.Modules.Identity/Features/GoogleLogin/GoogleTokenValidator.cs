using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Reflow.Modules.Identity.Features.GoogleLogin;

public class GoogleTokenValidator(
    IConfiguration configuration,
    ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    public async Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var clientId = configuration["Authentication:Google:ClientId"];
        var settings = new GoogleJsonWebSignature.ValidationSettings();

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            settings.Audience = [clientId];
        }
        else
        {
            logger.LogWarning("Authentication:Google:ClientId is not set. Google token audience validation is skipped in current environment.");
        }

        return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }
}
