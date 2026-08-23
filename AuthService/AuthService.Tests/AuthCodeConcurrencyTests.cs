using System.Collections.Concurrent;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Xunit;
using ApplicationAuthService = AuthService.Application.Services.AuthService;

namespace AuthService.Tests;

public sealed class AuthCodeConcurrencyTests
{
    private const string Email = "concurrency@example.com";
    private const string ValidCode = "123456";

    [Fact]
    public async Task Verification_ConcurrentWrongAttempts_AreNotLost()
    {
        var fixture = Fixture.ForVerification();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => IgnoreFailure(
            () => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Email = Email, Code = "999999" }))));

        Assert.Equal(4, fixture.VerificationCodes.Latest.Attempts);
    }

    [Fact]
    public async Task Verification_ConcurrentValidVerify_OnlyOneSucceeds()
    {
        var fixture = Fixture.ForVerification();

        var outcomes = await Task.WhenAll(
            Capture(() => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Email = Email, Code = ValidCode })),
            Capture(() => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Email = Email, Code = ValidCode })));

        Assert.Single(outcomes, x => x.Success);
        Assert.Single(outcomes, x => !x.Success);
        Assert.Equal(1, fixture.RegistrationTokens.GenerateCalls);
        Assert.True(fixture.VerificationCodes.Latest.Used);
    }

    [Fact]
    public async Task Verification_UsedCode_CannotBeConsumedAgain()
    {
        var fixture = Fixture.ForVerification();
        await fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Email = Email, Code = ValidCode });

        await Assert.ThrowsAsync<EmailVerificationException>(() =>
            fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Email = Email, Code = ValidCode }));

        Assert.Equal(1, fixture.RegistrationTokens.GenerateCalls);
    }

    [Fact]
    public async Task Verification_ConcurrentResend_LeavesOnlyOneActiveCode()
    {
        var fixture = Fixture.ForVerification(codeCreatedAt: DateTime.UtcNow.AddMinutes(-5));

        await Task.WhenAll(
            IgnoreFailure(() => fixture.Service.ResendVerificationCodeAsync(new ResendVerificationCodeRequest { Email = Email })),
            IgnoreFailure(() => fixture.Service.ResendVerificationCodeAsync(new ResendVerificationCodeRequest { Email = Email })));

        Assert.Single(fixture.VerificationCodes.Codes, x => !x.Used);
        Assert.Equal(2, fixture.VerificationCodes.Codes.Count);
        Assert.Equal(1, fixture.Emails.VerificationDeliveries);
    }

    [Fact]
    public async Task Verification_VerifyAndResendRace_HasSafeOutcome()
    {
        var fixture = Fixture.ForVerification(codeCreatedAt: DateTime.UtcNow.AddMinutes(-5));

        await Task.WhenAll(
            IgnoreFailure(() => fixture.Service.VerifyEmailAsync(new VerifyEmailRequest { Email = Email, Code = ValidCode })),
            IgnoreFailure(() => fixture.Service.ResendVerificationCodeAsync(new ResendVerificationCodeRequest { Email = Email })));

        Assert.True(fixture.RegistrationTokens.GenerateCalls <= 1);
        Assert.True(fixture.VerificationCodes.Codes.Count(x => !x.Used) <= 1);
        if (fixture.User.EmailVerified)
        {
            Assert.DoesNotContain(fixture.VerificationCodes.Codes, x => !x.Used);
        }
    }

    [Fact]
    public async Task PasswordReset_ConcurrentWrongAttempts_AreNotLost()
    {
        var fixture = Fixture.ForPasswordReset();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => IgnoreFailure(() =>
            fixture.Service.ResetPasswordAsync(ResetRequest("999999")))));

        Assert.Equal(4, fixture.PasswordResetCodes.Latest.Attempts);
    }

    [Fact]
    public async Task PasswordReset_ConcurrentReset_OnlyOneSucceeds()
    {
        var fixture = Fixture.ForPasswordReset();

        var outcomes = await Task.WhenAll(
            Capture(() => fixture.Service.ResetPasswordAsync(ResetRequest(ValidCode))),
            Capture(() => fixture.Service.ResetPasswordAsync(ResetRequest(ValidCode))));

        Assert.Single(outcomes, x => x.Success);
        Assert.Single(outcomes, x => !x.Success);
        Assert.True(fixture.PasswordResetCodes.Latest.Used);
        Assert.Equal(1, fixture.RefreshTokens.RevokeAllCalls);
    }

    [Fact]
    public async Task PasswordReset_ConsumedCode_RemainsSingleUse()
    {
        var fixture = Fixture.ForPasswordReset();
        await fixture.Service.ResetPasswordAsync(ResetRequest(ValidCode));

        await Assert.ThrowsAsync<AuthException>(() =>
            fixture.Service.ResetPasswordAsync(ResetRequest(ValidCode)));

        Assert.Equal(1, fixture.RefreshTokens.RevokeAllCalls);
    }

    [Fact]
    public async Task PasswordReset_ConcurrentForgotPassword_LeavesOnlyOneActiveCode()
    {
        var fixture = Fixture.ForPasswordReset(withCode: false);
        var request = new ForgotPasswordRequest { Email = Email };

        await Task.WhenAll(
            fixture.Service.ForgotPasswordAsync(request),
            fixture.Service.ForgotPasswordAsync(request));

        Assert.Single(fixture.PasswordResetCodes.Codes, x => !x.Used);
        Assert.Equal(1, fixture.Emails.PasswordResetDeliveries);
    }

    private static ResetPasswordRequest ResetRequest(string code) => new()
    {
        Email = Email,
        Code = code,
        NewPassword = "new-password",
        ConfirmPassword = "new-password"
    };

    private static async Task IgnoreFailure(Func<Task> action)
    {
        try { await action(); }
        catch (EmailVerificationException) { }
        catch (AuthException) { }
    }

    private static async Task<Outcome> Capture(Func<Task> action)
    {
        try
        {
            await action();
            return new Outcome(true);
        }
        catch (EmailVerificationException) { return new Outcome(false); }
        catch (AuthException) { return new Outcome(false); }
    }

    private sealed record Outcome(bool Success);

    private sealed class Fixture
    {
        private Fixture(bool emailVerified, bool withVerificationCode, bool withResetCode, DateTime? codeCreatedAt)
        {
            User = new User
            {
                Id = Guid.NewGuid(), Email = AuthCodeConcurrencyTests.Email, PasswordHash = "hash:old-password",
                EmailVerified = emailVerified, FirstName = emailVerified ? "Test" : null,
                LastName = emailVerified ? "User" : null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            Users = new MemoryUserRepository(User);
            Verification = new DeterministicVerificationCodeService();
            VerificationCodes = new MemoryEmailVerificationCodeRepository();
            PasswordResetCodes = new MemoryPasswordResetCodeRepository();
            Emails = new RecordingEmailService();
            RegistrationTokens = new RecordingRegistrationTokenService();
            RefreshTokens = new RecordingRefreshTokenRepository();

            if (withVerificationCode)
            {
                VerificationCodes.Codes.Add(new EmailVerificationCode
                {
                    Id = Guid.NewGuid(), UserId = User.Id, User = User,
                    CodeHash = Verification.HashCode(ValidCode), ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    CreatedAt = codeCreatedAt ?? DateTime.UtcNow
                });
            }
            if (withResetCode)
            {
                PasswordResetCodes.Codes.Add(new PasswordResetCode
                {
                    Id = Guid.NewGuid(), UserId = User.Id, User = User,
                    CodeHash = Verification.HashCode(ValidCode), ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    CreatedAt = codeCreatedAt ?? DateTime.UtcNow
                });
            }

            Service = new ApplicationAuthService(
                Users, VerificationCodes, new MemoryPasswordHasher(), Verification, Emails,
                new MemoryUnitOfWork(), new StubJwtService(), new StubRefreshTokenService(), RefreshTokens,
                PasswordResetCodes, RegistrationTokens, new SerializingAuthCodeLock());
        }

        public ApplicationAuthService Service { get; }
        public User User { get; }
        public MemoryUserRepository Users { get; }
        public MemoryEmailVerificationCodeRepository VerificationCodes { get; }
        public MemoryPasswordResetCodeRepository PasswordResetCodes { get; }
        public DeterministicVerificationCodeService Verification { get; }
        public RecordingEmailService Emails { get; }
        public RecordingRegistrationTokenService RegistrationTokens { get; }
        public RecordingRefreshTokenRepository RefreshTokens { get; }

        public static Fixture ForVerification(DateTime? codeCreatedAt = null) =>
            new(false, true, false, codeCreatedAt);

        public static Fixture ForPasswordReset(bool withCode = true) =>
            new(true, false, withCode, DateTime.UtcNow.AddMinutes(-5));
    }

    private sealed class SerializingAuthCodeLock : IAuthCodeConcurrencyLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

        public async Task<T> ExecuteAsync<T>(string normalizedEmail, Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken = default)
        {
            var gate = _locks.GetOrAdd(normalizedEmail, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try { return await action(cancellationToken); }
            finally { gate.Release(); }
        }
    }

    private sealed class MemoryUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(email == user.Email ? user : null);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(email == user.Email);
        public Task AddAsync(User addedUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(User updatedUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MemoryEmailVerificationCodeRepository : IEmailVerificationCodeRepository
    {
        public List<EmailVerificationCode> Codes { get; } = [];
        public EmailVerificationCode Latest => Codes.OrderByDescending(x => x.CreatedAt).First();
        public Task<EmailVerificationCode?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Codes.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).FirstOrDefault());
        public Task AddAsync(EmailVerificationCode code, CancellationToken cancellationToken = default)
        {
            Codes.Add(code); return Task.CompletedTask;
        }
        public Task UpdateAsync(EmailVerificationCode code, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MemoryPasswordResetCodeRepository : IPasswordResetCodeRepository
    {
        public List<PasswordResetCode> Codes { get; } = [];
        public PasswordResetCode Latest => Codes.OrderByDescending(x => x.CreatedAt).First();
        public Task<PasswordResetCode?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Codes.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).FirstOrDefault());
        public Task AddAsync(PasswordResetCode code, CancellationToken cancellationToken = default)
        {
            Codes.Add(code); return Task.CompletedTask;
        }
    }

    private sealed class DeterministicVerificationCodeService : IVerificationCodeService
    {
        private int _nextCode = 200000;
        public string GenerateCode() => Interlocked.Increment(ref _nextCode).ToString("D6");
        public string HashCode(string code) => $"hash:{code}";
        public bool VerifyCode(string code, string codeHash) => HashCode(code) == codeHash;
        public TimeSpan Lifetime => TimeSpan.FromMinutes(10);
        public int MaxAttempts => 5;
        public TimeSpan ResendCooldown => TimeSpan.FromMinutes(1);
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public int VerificationDeliveries;
        public int PasswordResetDeliveries;
        public Task SendVerificationCodeAsync(string email, string code, TimeSpan lifetime, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref VerificationDeliveries); return Task.CompletedTask; }
        public Task SendPasswordResetCodeAsync(string email, string code, TimeSpan lifetime, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref PasswordResetDeliveries); return Task.CompletedTask; }
    }

    private sealed class RecordingRegistrationTokenService : IRegistrationTokenService
    {
        public int GenerateCalls;
        public string Generate(Guid userId) { Interlocked.Increment(ref GenerateCalls); return $"token:{userId}"; }
        public Guid Validate(string token) => throw new NotSupportedException();
    }

    private sealed class RecordingRefreshTokenRepository : IRefreshTokenRepository
    {
        public int RevokeAllCalls;
        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAllActiveByUserIdAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref RevokeAllCalls); return Task.CompletedTask; }
    }

    private sealed class MemoryPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class MemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
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
}
