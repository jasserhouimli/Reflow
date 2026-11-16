using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Reflow.Modules.Identity.Domain;
using Reflow.Modules.Identity.Features.GoogleLogin;
using Reflow.Modules.Identity.Services;
using Xunit;

namespace Reflow.UnitTests;

public class GoogleLoginTests
{
    private sealed class TestUserStore : IUserStore<User>, IUserLoginStore<User>, IUserEmailStore<User>
    {
        private readonly Dictionary<string, User> _users = new();
        private readonly List<(string UserId, UserLoginInfo Login)> _logins = new();

        public void Dispose() { }

        public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }

        public Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(user.Id)) user.Id = Guid.NewGuid().ToString();
            _users[user.Id] = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken) { _users[user.Id] = user; return Task.FromResult(IdentityResult.Success); }
        public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken) { _users.Remove(user.Id); return Task.FromResult(IdentityResult.Success); }
        public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_users.GetValueOrDefault(userId));
        public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult(_users.Values.FirstOrDefault(u => u.UserName == normalizedUserName || u.NormalizedUserName == normalizedUserName));

        public Task AddLoginAsync(User user, UserLoginInfo login, CancellationToken cancellationToken)
        {
            _logins.Add((user.Id, login));
            return Task.CompletedTask;
        }

        public Task RemoveLoginAsync(User user, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            _logins.RemoveAll(l => l.UserId == user.Id && l.Login.LoginProvider == loginProvider && l.Login.ProviderKey == providerKey);
            return Task.CompletedTask;
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(User user, CancellationToken cancellationToken)
        {
            IList<UserLoginInfo> list = _logins.Where(l => l.UserId == user.Id).Select(l => l.Login).ToList();
            return Task.FromResult(list);
        }

        public Task<User?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            var match = _logins.FirstOrDefault(l => l.Login.LoginProvider == loginProvider && l.Login.ProviderKey == providerKey);
            if (match.UserId != null && _users.TryGetValue(match.UserId, out var u)) return Task.FromResult<User?>(u);
            return Task.FromResult<User?>(null);
        }

        public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken) { user.Email = email; return Task.CompletedTask; }
        public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
        public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);
        public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken) { user.EmailConfirmed = confirmed; return Task.CompletedTask; }
        public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(_users.Values.FirstOrDefault(u => string.Equals(u.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)));
        public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
        public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken) { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }
    }

    private sealed class TestTokenService : TokenService
    {
        public TestTokenService() : base(new ConfigurationBuilder().Build(), null!) { }

        public override string GenerateAccessToken(User user) => "fake-jwt-token";

        public override Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new RefreshToken { Id = Guid.NewGuid(), Token = "fake-refresh-token", UserId = userId });
    }

    private sealed class StubGoogleValidator(Func<string, GoogleJsonWebSignature.Payload?> validator) : IGoogleTokenValidator
    {
        public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, CancellationToken ct = default)
        {
            var result = validator(idToken);
            if (result == null) throw new InvalidJwtException("Invalid token signature");
            return Task.FromResult(result);
        }
    }

    private static (GoogleLoginHandler handler, TestUserStore store) CreateHandler(
        Func<string, GoogleJsonWebSignature.Payload?> validator)
    {
        var store = new TestUserStore();
        var userManager = new UserManager<User>(
            store, null!, new PasswordHasher<User>(), null!, null!, null!, null!, null!, null!);

        var tokenService = new TestTokenService();
        var googleValidator = new StubGoogleValidator(validator);
        var logger = NullLogger<GoogleLoginHandler>.Instance;

        var handler = new GoogleLoginHandler(userManager, tokenService, googleValidator, logger);
        return (handler, store);
    }

    [Fact]
    public void Validator_RejectsEmptyToken()
    {
        var validator = new GoogleLoginRequestValidator();
        var result = validator.Validate(new GoogleLoginRequest(""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_AcceptsValidToken()
    {
        var validator = new GoogleLoginRequestValidator();
        var result = validator.Validate(new GoogleLoginRequest("valid-token-string"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Handle_InvalidToken_Returns401()
    {
        var (handler, _) = CreateHandler(_ => null);

        var result = await handler.Handle(new GoogleLoginRequest("bad-token"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Invalid Google token.", result.Error);
    }

    [Fact]
    public async Task Handle_UnverifiedEmail_Returns400()
    {
        var (handler, _) = CreateHandler(_ => new GoogleJsonWebSignature.Payload
        {
            Subject = "sub123",
            Email = "user@test.com",
            EmailVerified = false,
            Name = "Unverified"
        });

        var result = await handler.Handle(new GoogleLoginRequest("valid-unverified"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("not verified", result.Error);
    }

    [Fact]
    public async Task Handle_ValidToken_AutoProvisionsNewUser()
    {
        var (handler, store) = CreateHandler(_ => new GoogleJsonWebSignature.Payload
        {
            Subject = "google-sub-456",
            Email = "newbie@test.com",
            EmailVerified = true,
            Name = "Newbie User"
        });

        var result = await handler.Handle(new GoogleLoginRequest("valid-newbie"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("newbie@test.com", result.Value!.Email);
        Assert.Equal("Newbie User", result.Value.FullName);
        Assert.Equal("fake-jwt-token", result.Value.AccessToken);

        // Verify stored in DB
        var user = await store.FindByEmailAsync("newbie@test.com", CancellationToken.None);
        Assert.NotNull(user);
        Assert.Equal("Newbie User", user.FullName);
    }

    [Fact]
    public async Task Handle_ExistingEmailUser_LinksGoogleAccount()
    {
        var (handler, store) = CreateHandler(_ => new GoogleJsonWebSignature.Payload
        {
            Subject = "google-sub-789",
            Email = "existing@test.com",
            EmailVerified = true,
            Name = "Existing User"
        });

        // Pre-create user without Google login
        var existing = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "existing@test.com",
            UserName = "existing@test.com",
            FullName = "Original Name"
        };
        await store.CreateAsync(existing, CancellationToken.None);

        var result = await handler.Handle(new GoogleLoginRequest("valid-existing"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value!.UserId.ToString());

        // Verify login linked
        var linked = await store.FindByLoginAsync("Google", "google-sub-789", CancellationToken.None);
        Assert.NotNull(linked);
        Assert.Equal(existing.Id, linked.Id);
    }
}
