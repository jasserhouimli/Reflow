using System.Security.Cryptography;

namespace Reflow.Modules.Triggers.Services;

/// <summary>Secret URL tokens: 32 random bytes, SHA-256 hex at rest.</summary>
public static class WebhookToken
{
    public static string Generate() =>
        Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
