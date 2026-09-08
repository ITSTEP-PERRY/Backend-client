using System.IdentityModel.Tokens.Jwt;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Xunit;
using ApplicationAuthService = AuthService.Application.Services.AuthService;

namespace AuthService.Tests;

public sealed class AuthenticationRoleStatusTests
{
    [Fact]
    public void AccessToken_ContainsSubjectAndRole()
    {
        var jwt = new JwtService(Options.Create(new JwtOptions
        {
            Issuer = "test-issuer", Audience = "test-audience",
            SigningSecret = "test-signing-secret-with-at-least-32-characters", AccessTokenMinutes = 15
        }));
        var user = User(UserStatus.Active, UserRole.Admin);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt.GenerateAccessToken(user));

        Assert.Equal(user.Id.ToString(), token.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Admin", token.Claims.Single(x => x.Type == "role").Value);
    }

    [Fact]
    public async Task DeletedUser_CannotLogin()
    {
        var fixture = new Fixture(User(UserStatus.Deleted));
        var exception = await Assert.ThrowsAsync<AuthException>(() => fixture.Service.LoginAsync(Login()));
        Assert.Equal(AuthErrorCodes.AccountDeleted, exception.ErrorCode);
    }

    [Fact]
    public async Task ActiveUser_CanLogin()
    {
        var fixture = new Fixture(User(UserStatus.Active));
        var response = await fixture.Service.LoginAsync(Login());
        Assert.Equal("access-token", response.AccessToken);
    }

    [Fact]
    public async Task DeletedUser_CannotRefresh()
    {
        var fixture = new Fixture(User(UserStatus.Deleted));
        var exception = await Assert.ThrowsAsync<AuthException>(() => fixture.Service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = "refresh-token" }));
        Assert.Equal(AuthErrorCodes.AccountDeleted, exception.ErrorCode);
    }

    private static LoginRequest Login() => new() { Email = "user@example.com", Password = "password" };
    private static User User(UserStatus status, UserRole role = UserRole.User) => new()
    {
        Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User",
        EmailVerified = true, Role = role, Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class Fixture
    {
        public Fixture(User user)
        {
            var users = new Users(user);
            Service = new ApplicationAuthService(users, new VerificationCodes(), new Passwords(), new Codes(),
                new Email(), new UnitOfWork(), new Jwt(), new RefreshService(), new RefreshTokens(user),
                new PasswordResetCodes(), new RegistrationTokens(), new Lock());
        }
        public ApplicationAuthService Service { get; }
    }

    private sealed class Users(User user) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<User?>(user);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(user);
        public Task<User?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(user);
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(true);
        public Task AddAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetPageAsync(int page, int size, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([user]);
        public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
    private sealed class Passwords : IPasswordHasher { public string Hash(string value) => "hash"; public bool Verify(string value, string hash) => true; }
    private sealed class Jwt : IJwtService { public string GenerateAccessToken(User user) => "access-token"; public int GetAccessTokenExpirationSeconds() => 900; }
    private sealed class RefreshService : IRefreshTokenService
    {
        public string GenerateToken() => "new-refresh-token";
        public string HashToken(string token) => "hash";
        public TimeSpan GetLifetime(bool rememberMe) => TimeSpan.FromDays(1);
        public bool IsRememberMeLifetime(TimeSpan lifetime) => false;
    }
    private sealed class RefreshTokens(User user) : IRefreshTokenRepository
    {
        public Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken ct = default) => Task.FromResult<RefreshToken?>(new()
        { Id = Guid.NewGuid(), UserId = user.Id, User = user, TokenHash = hash, CreatedAt = DateTime.UtcNow.AddHours(-1), ExpiresAt = DateTime.UtcNow.AddHours(1) });
        public Task AddAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAllActiveByUserIdAsync(Guid id, DateTime at, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class UnitOfWork : IUnitOfWork { public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1); }
    private sealed class Lock : IAuthCodeConcurrencyLock { public Task<T> ExecuteAsync<T>(string email, Func<CancellationToken, Task<T>> action, CancellationToken ct = default) => action(ct); }
    private sealed class VerificationCodes : IEmailVerificationCodeRepository
    {
        public Task<EmailVerificationCode?> GetLatestByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<EmailVerificationCode?>(null);
        public Task AddAsync(EmailVerificationCode code, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(EmailVerificationCode code, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class PasswordResetCodes : IPasswordResetCodeRepository
    {
        public Task<PasswordResetCode?> GetLatestByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<PasswordResetCode?>(null);
        public Task AddAsync(PasswordResetCode code, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Codes : IVerificationCodeService
    {
        public string GenerateCode() => "123456"; public string HashCode(string code) => code; public bool VerifyCode(string code, string hash) => code == hash;
        public TimeSpan Lifetime => TimeSpan.FromMinutes(10); public int MaxAttempts => 5; public TimeSpan ResendCooldown => TimeSpan.FromMinutes(1);
    }
    private sealed class Email : IEmailService
    {
        public Task SendVerificationCodeAsync(string e, string c, TimeSpan l, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetCodeAsync(string e, string c, TimeSpan l, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class RegistrationTokens : IRegistrationTokenService { public string Generate(Guid id) => "token"; public Guid Validate(string token) => Guid.Empty; }
}
