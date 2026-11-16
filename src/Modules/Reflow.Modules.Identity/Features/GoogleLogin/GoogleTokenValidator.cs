using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Reflow.Modules.Identity.Features.GoogleLogin;

public class GoogleTokenValidator(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    private sealed class GoogleUserInfoResponse
    {
        [JsonPropertyName("sub")]
        public string Sub { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("email_verified")]
        public bool EmailVerified { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public async Task<GoogleUserInfo> ValidateAsync(GoogleLoginRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.IdToken))
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

            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.Name
            );
        }

        if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

            var res = await httpClient.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("Google UserInfo endpoint returned HTTP status {StatusCode}", res.StatusCode);
                throw new InvalidJwtException($"Invalid or expired Google access token (HTTP {res.StatusCode}).");
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            var info = JsonSerializer.Deserialize<GoogleUserInfoResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (info == null || string.IsNullOrWhiteSpace(info.Sub))
            {
                throw new InvalidJwtException("Unable to parse Google user information.");
            }

            return new GoogleUserInfo(info.Sub, info.Email, info.EmailVerified, info.Name);
        }

        throw new ArgumentException("Neither IdToken nor AccessToken was provided.");
    }
}
