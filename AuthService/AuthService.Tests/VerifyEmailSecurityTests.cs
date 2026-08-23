using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Xunit;
using ApplicationAuthService = AuthService.Application.Services.AuthService;

namespace AuthService.Tests;

public sealed class VerifyEmailSecurityTests
{
    private const string Email = "user@example.com";
    private const string ValidCode = "123456";

    [Fact]
    public async Task VerifyEmail_WithValidCode_IssuesRegistrationToken()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.VerifyEmailAsync(Request(ValidCode));

        Assert.True(response.EmailVerified);
        Assert.Equal(Email, response.Email);
        Assert.Equal($"registration:{fixture.User.Id}", response.RegistrationToken);
        Assert.True(fixture.User.EmailVerified);
        Assert.True(fixture.Code.Used);
        Assert.Equal(1, fixture.RegistrationTokens.GenerateCalls);
        Assert.Equal(fixture.User.Id, fixture.RegistrationTokens.LastGeneratedUserId);
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidCode_DoesNotIssueRegistrationToken()
    {
        var fixture = CreateFixture();

        var exception = await Assert.ThrowsAsync<EmailVerificationException>(
            () => fixture.Service.VerifyEmailAsync(Request("999999")));

        Assert.Equal(EmailVerificationErrorCodes.InvalidCode, exception.ErrorCode);
        Assert.Equal(0, fixture.RegistrationTokens.GenerateCalls);
        Assert.False(fixture.User.EmailVerified);
        Assert.False(fixture.Code.Used);
        Assert.Equal(1, fixture.Code.Attempts);
    }

    [Fact]
    public async Task VerifyEmail_WhenEmailAlreadyVerifiedAndRegistrationIncomplete_DoesNotIssueRegistrationToken()
    {
        var fixture = CreateFixture(emailVerified: true);

        var exception = await Assert.ThrowsAsync<EmailVerificationException>(
            () => fixture.Service.VerifyEmailAsync(Request("999999")));

        Assert.Equal(EmailVerificationErrorCodes.EmailAlreadyVerified, exception.ErrorCode);
        Assert.Equal(0, fixture.RegistrationTokens.GenerateCalls);
        Assert.Equal(0, fixture.Codes.GetLatestCalls);
    }

    [Fact]
    public async Task VerifyEmail_WithUsedCode_DoesNotIssueRegistrationToken()
    {
        var fixture = CreateFixture(codeUsed: true);

        var exception = await Assert.ThrowsAsync<EmailVerificationException>(
            () => fixture.Service.VerifyEmailAsync(Request(ValidCode)));

        Assert.Equal(EmailVerificationErrorCodes.CodeNotFound, exception.ErrorCode);
        Assert.Equal(0, fixture.RegistrationTokens.GenerateCalls);
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredCode_DoesNotIssueRegistrationToken()
    {
        var fixture = CreateFixture(codeExpiresAt: DateTime.UtcNow.AddMinutes(-1));

        var exception = await Assert.ThrowsAsync<EmailVerificationException>(
            () => fixture.Service.VerifyEmailAsync(Request(ValidCode)));

        Assert.Equal(EmailVerificationErrorCodes.CodeExpired, exception.ErrorCode);
        Assert.Equal(0, fixture.RegistrationTokens.GenerateCalls);
    }

    [Fact]
    public async Task VerifyEmail_WhenRegistrationAlreadyCompleted_DoesNotIssueRegistrationToken()
    {
        var fixture = CreateFixture(
            emailVerified: true,
            firstName: "Test",
            lastName: "User");

        var exception = await Assert.ThrowsAsync<EmailVerificationException>(
            () => fixture.Service.VerifyEmailAsync(Request(ValidCode)));

        Assert.Equal(EmailVerificationErrorCodes.EmailAlreadyVerified, exception.ErrorCode);
        Assert.Equal(0, fixture.RegistrationTokens.GenerateCalls);
    }

    private static VerifyEmailRequest Request(string code) => new()
    {
        Email = Email,
        Code = code
    };

    private static Fixture CreateFixture(
        bool emailVerified = false,
        bool codeUsed = false,
        DateTime? codeExpiresAt = null,
        string? firstName = null,
        string? lastName = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = Email,
            PasswordHash = "not-used",
            EmailVerified = emailVerified,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var verificationCodes = new FakeVerificationCodeService();
        var code = new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            CodeHash = verificationCodes.HashCode(ValidCode),
            ExpiresAt = codeExpiresAt ?? DateTime.UtcNow.AddMinutes(10),
            Used = codeUsed,
            CreatedAt = DateTime.UtcNow
        };
        var users = new FakeUserRepository(user);
        var codes = new FakeEmailVerificationCodeRepository(code);
        var registrationTokens = new SpyRegistrationTokenService();
        var service = new ApplicationAuthService(
            users,
            codes,
            new StubPasswordHasher(),
            verificationCodes,
            new StubEmailService(),
            new FakeUnitOfWork(),
            new StubJwtService(),
            new StubRefreshTokenService(),
            new StubRefreshTokenRepository(),
            new StubPasswordResetCodeRepository(),
            registrationTokens,
            new PassThroughAuthCodeConcurrencyLock());

        return new Fixture(service, user, code, codes, registrationTokens);
    }

    private sealed record Fixture(
        ApplicationAuthService Service,
        User User,
        EmailVerificationCode Code,
        FakeEmailVerificationCodeRepository Codes,
        SpyRegistrationTokenService RegistrationTokens);

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(string.Equals(email, user.Email, StringComparison.Ordinal) ? user : null);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(id == user.Id ? user : null);

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(email, user.Email, StringComparison.Ordinal));

        public Task AddAsync(User addedUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(User updatedUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeEmailVerificationCodeRepository(EmailVerificationCode code)
        : IEmailVerificationCodeRepository
    {
        public int GetLatestCalls { get; private set; }

        public Task<EmailVerificationCode?> GetLatestByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            GetLatestCalls++;
            return Task.FromResult<EmailVerificationCode?>(userId == code.UserId ? code : null);
        }

        public Task AddAsync(EmailVerificationCode verificationCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(EmailVerificationCode verificationCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeVerificationCodeService : IVerificationCodeService
    {
        public string GenerateCode() => ValidCode;
        public string HashCode(string code) => $"hash:{code}";
        public bool VerifyCode(string code, string codeHash) => HashCode(code) == codeHash;
        public TimeSpan Lifetime => TimeSpan.FromMinutes(10);
        public int MaxAttempts => 5;
        public TimeSpan ResendCooldown => TimeSpan.FromMinutes(1);
    }

    private sealed class SpyRegistrationTokenService : IRegistrationTokenService
    {
        public int GenerateCalls { get; private set; }
        public Guid? LastGeneratedUserId { get; private set; }

        public string Generate(Guid userId)
        {
            GenerateCalls++;
            LastGeneratedUserId = userId;
            return $"registration:{userId}";
        }

        public Guid Validate(string token) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class PassThroughAuthCodeConcurrencyLock : IAuthCodeConcurrencyLock
    {
        public Task<T> ExecuteAsync<T>(
            string normalizedEmail,
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken = default) => action(cancellationToken);
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => throw new NotSupportedException();
        public bool Verify(string password, string passwordHash) => throw new NotSupportedException();
    }

    private sealed class StubEmailService : IEmailService
    {
        public Task SendVerificationCodeAsync(string email, string code, TimeSpan codeLifetime, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SendPasswordResetCodeAsync(string email, string code, TimeSpan codeLifetime, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubJwtService : IJwtService
    {
        public string GenerateAccessToken(User user) => throw new NotSupportedException();
        public int GetAccessTokenExpirationSeconds() => throw new NotSupportedException();
    }

    private sealed class StubRefreshTokenService : IRefreshTokenService
    {
        public string GenerateToken() => throw new NotSupportedException();
        public string HashToken(string token) => throw new NotSupportedException();
        public TimeSpan GetLifetime(bool rememberMe) => throw new NotSupportedException();
        public bool IsRememberMeLifetime(TimeSpan lifetime) => throw new NotSupportedException();
    }

    private sealed class StubRefreshTokenRepository : IRefreshTokenRepository
    {
        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeAllActiveByUserIdAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubPasswordResetCodeRepository : IPasswordResetCodeRepository
    {
        public Task<PasswordResetCode?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(PasswordResetCode code, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
