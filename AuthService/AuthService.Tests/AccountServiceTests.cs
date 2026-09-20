using AuthService.Application.DTOs.Account;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Domain.Entities;
using Xunit;

namespace AuthService.Tests;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task ChangeName_TrimsAndUpdatesTimestamp()
    {
        var f = new Fixture(); var before = f.User.UpdatedAt;
        var response = await f.Service.ChangeNameAsync(f.User.Id, new() { FirstName = "  New ", LastName = " Name  " });
        Assert.Equal("New", response.FirstName); Assert.Equal("Name", response.LastName);
        Assert.True(f.User.UpdatedAt > before); Assert.Equal(1, f.Unit.Saves);
    }

    [Theory]
    [InlineData(UserStatus.Blocked, AuthErrorCodes.AccountBlocked)]
    [InlineData(UserStatus.Deleted, AuthErrorCodes.AccountDeleted)]
    public async Task Mutations_RejectInactiveUser(UserStatus status, string code)
    {
        var f = new Fixture(); f.User.Status = status;
        var ex = await Assert.ThrowsAsync<AuthException>(() => f.Service.ChangeNameAsync(f.User.Id, new() { FirstName = "New", LastName = "Name" }));
        Assert.Equal(code, ex.ErrorCode);
    }

    [Fact]
    public async Task EmailRequest_RequiresCurrentPassword()
    {
        var f = new Fixture();
        var ex = await Assert.ThrowsAsync<AuthException>(() => f.Service.StartEmailChangeAsync(f.User.Id,
            new() { NewEmail = "new@example.com", CurrentPassword = "wrong" }));
        Assert.Equal(AuthErrorCodes.CurrentPasswordInvalid, ex.ErrorCode);
    }

    [Fact]
    public async Task EmailRequest_CreatesPendingWithoutChangingUserEmail()
    {
        var f = new Fixture();
        await f.Service.StartEmailChangeAsync(f.User.Id, new() { NewEmail = " NEW@Example.com ", CurrentPassword = "password" });
        Assert.Equal("user@example.com", f.User.Email);
        Assert.Equal("new@example.com", f.Changes.Latest!.NewEmail);
        Assert.NotEqual("123456", f.Changes.Latest.CodeHash);
        Assert.Equal("new@example.com", f.Email.LastRecipient);
    }

    [Fact]
    public async Task EmailRequest_RejectsDuplicateEmail()
    {
        var f = new Fixture(); f.Users.EmailExists = true;
        var ex = await Assert.ThrowsAsync<AuthException>(() => f.Service.StartEmailChangeAsync(f.User.Id,
            new() { NewEmail = "used@example.com", CurrentPassword = "password" }));
        Assert.Equal(AuthErrorCodes.EmailAlreadyRegistered, ex.ErrorCode);
    }

    [Fact]
    public async Task EmailVerify_InvalidCodeIncrementsAttemptsWithoutChangingEmail()
    {
        var f = new Fixture(); await f.StartEmailChange();
        var ex = await Assert.ThrowsAsync<AuthException>(() => f.Service.VerifyEmailChangeAsync(f.User.Id, new() { Code = "000000" }));
        Assert.Equal(AuthErrorCodes.EmailChangeCodeInvalid, ex.ErrorCode);
        Assert.Equal(1, f.Changes.Latest!.Attempts); Assert.Equal("user@example.com", f.User.Email);
    }

    [Fact]
    public async Task EmailVerify_ChangesEmailAndRevokesSessions()
    {
        var f = new Fixture(); await f.StartEmailChange();
        await f.Service.VerifyEmailChangeAsync(f.User.Id, new() { Code = "123456" });
        Assert.Equal("new@example.com", f.User.Email); Assert.True(f.User.EmailVerified);
        Assert.True(f.Changes.Latest!.Used); Assert.Equal(1, f.Refresh.Revocations);
    }

    [Fact]
    public async Task EmailResend_EnforcesCooldown()
    {
        var f = new Fixture(); await f.StartEmailChange();
        var ex = await Assert.ThrowsAsync<AuthException>(() => f.Service.ResendEmailChangeAsync(f.User.Id));
        Assert.Equal("RESEND_COOLDOWN_ACTIVE", ex.ErrorCode); Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ChangesHashAndRevokesSessions()
    {
        var f = new Fixture();
        await f.Service.ChangePasswordAsync(f.User.Id, new() { CurrentPassword = "password", NewPassword = "new-password", ConfirmPassword = "new-password" });
        Assert.Equal("hash:new-password", f.User.PasswordHash); Assert.Equal(1, f.Refresh.Revocations);
    }

    [Fact]
    public async Task DeleteAccount_SoftDeletesAndRevokesSessions()
    {
        var f = new Fixture();
        await f.Service.DeleteAccountAsync(f.User.Id, new() { CurrentPassword = "password", Confirmation = "DELETE" });
        Assert.Equal(UserStatus.Deleted, f.User.Status); Assert.Equal(1, f.Refresh.Revocations);
    }

    [Fact]
    public async Task DeleteAccount_ProtectsLastActiveAdmin()
    {
        var f = new Fixture(); f.User.Role = UserRole.Admin; f.Users.ActiveAdmins = 1;
        var ex = await Assert.ThrowsAsync<AuthException>(() => f.Service.DeleteAccountAsync(f.User.Id,
            new() { CurrentPassword = "password", Confirmation = "DELETE" }));
        Assert.Equal(AuthErrorCodes.LastAdminProtection, ex.ErrorCode); Assert.Equal(UserStatus.Active, f.User.Status);
    }

    [Fact]
    public async Task AvatarUpload_ValidJpegUsesGeneratedSafeKey()
    {
        var f = new Fixture(); var bytes = new byte[] { 0xff, 0xd8, 0xff, 1, 2 };
        var url = await f.Service.UploadAvatarAsync(f.User.Id, new MemoryStream(bytes), bytes.Length, "image/jpeg");
        Assert.Equal("/api/account/avatar", url); Assert.StartsWith($"users/{f.User.Id:N}/avatar-", f.User.AvatarBlobName);
        Assert.EndsWith(".jpg", f.User.AvatarBlobName); Assert.DoesNotContain("..", f.User.AvatarBlobName);
    }

    [Fact]
    public async Task AvatarUpload_RejectsInvalidSignature()
    {
        var f = new Fixture(); var bytes = new byte[] { 1, 2, 3, 4 };
        var ex = await Assert.ThrowsAsync<AuthException>(() => f.Service.UploadAvatarAsync(f.User.Id,
            new MemoryStream(bytes), bytes.Length, "image/png"));
        Assert.Equal(AuthErrorCodes.AvatarContentInvalid, ex.ErrorCode); Assert.Empty(f.Storage.Files);
    }

    [Fact]
    public async Task AvatarReplacement_DeletesOldBlobAfterCommit()
    {
        var f = new Fixture(); f.User.AvatarBlobName = "users/old.png"; f.Storage.Files["users/old.png"] = ([1], "image/png");
        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        await f.Service.UploadAvatarAsync(f.User.Id, new MemoryStream(bytes), bytes.Length, "image/png");
        Assert.DoesNotContain("users/old.png", f.Storage.Files.Keys); Assert.Contains(f.User.AvatarBlobName!, f.Storage.Files.Keys);
    }

    [Fact]
    public async Task DeleteAvatar_IsIdempotentAndClearsDatabaseFirst()
    {
        var f = new Fixture();
        await f.Service.DeleteAvatarAsync(f.User.Id);
        f.User.AvatarBlobName = "users/avatar.jpg"; f.Storage.Files[f.User.AvatarBlobName] = ([1], "image/jpeg");
        await f.Service.DeleteAvatarAsync(f.User.Id);
        Assert.Null(f.User.AvatarBlobName); Assert.Empty(f.Storage.Files);
    }

    private sealed class Fixture
    {
        public User User { get; } = new()
        {
            Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hash:password", FirstName = "Test", LastName = "User",
            EmailVerified = true, Role = UserRole.User, Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        public Users Users { get; }
        public Changes Changes { get; } = new();
        public Email Email { get; } = new();
        public Refresh Refresh { get; } = new();
        public Storage Storage { get; } = new();
        public Unit Unit { get; } = new();
        public AccountService Service { get; }

        public Fixture()
        {
            Users = new Users(User);
            Service = new AccountService(Users, Changes, new Passwords(), new Codes(), Email, Refresh,
                new Lock(), new AdminLock(), Storage, new Log(), Unit);
        }
        public Task StartEmailChange() => Service.StartEmailChangeAsync(User.Id,
            new EmailChangeStartRequest { NewEmail = "new@example.com", CurrentPassword = "password" });
    }

    private sealed class Users(User user) : IUserRepository
    {
        public bool EmailExists; public int ActiveAdmins = 1;
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<User?>(user.Email == email ? user : null);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(EmailExists || user.Email == email);
        public Task AddAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetPageAsync(AuthService.Application.DTOs.Users.GetUsersRequest request, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([user]);
        public Task<int> CountAsync(AuthService.Application.DTOs.Users.GetUsersRequest request, CancellationToken ct = default) => Task.FromResult(1);
        public Task<int> CountActiveAdminsAsync(CancellationToken ct = default) => Task.FromResult(ActiveAdmins);
    }
    private sealed class Changes : IEmailChangeRequestRepository
    {
        public EmailChangeRequest? Latest;
        public Task<EmailChangeRequest?> GetLatestByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Latest);
        public Task AddAsync(EmailChangeRequest request, CancellationToken ct = default) { Latest = request; return Task.CompletedTask; }
    }
    private sealed class Passwords : IPasswordHasher { public string Hash(string value) => $"hash:{value}"; public bool Verify(string value, string hash) => hash == Hash(value); }
    private sealed class Codes : IVerificationCodeService
    {
        public TimeSpan Lifetime => TimeSpan.FromMinutes(10); public int MaxAttempts => 5; public TimeSpan ResendCooldown => TimeSpan.FromMinutes(1);
        public string GenerateCode() => "123456"; public string HashCode(string code) => $"code:{code}"; public bool VerifyCode(string code, string hash) => hash == HashCode(code);
    }
    private sealed class Email : IEmailService
    {
        public string? LastRecipient;
        public Task SendVerificationCodeAsync(string e, string c, TimeSpan l, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetCodeAsync(string e, string c, TimeSpan l, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendEmailChangeCodeAsync(string e, string c, TimeSpan l, CancellationToken ct = default) { LastRecipient = e; return Task.CompletedTask; }
    }
    private sealed class Refresh : IRefreshTokenRepository
    {
        public int Revocations;
        public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct = default) => Task.FromResult<RefreshToken?>(null);
        public Task AddAsync(RefreshToken t, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAllActiveByUserIdAsync(Guid id, DateTime at, CancellationToken ct = default) { Revocations++; return Task.CompletedTask; }
    }
    private sealed class Lock : IAuthCodeConcurrencyLock { public Task<T> ExecuteAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct = default) => action(ct); }
    private sealed class AdminLock : IAdminUserMutationLock { public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default) => action(ct); }
    private sealed class Storage : IAvatarStorage
    {
        public Dictionary<string, (byte[] Bytes, string Type)> Files { get; } = [];
        public async Task UploadAsync(string name, Stream content, string type, CancellationToken ct = default) { using var m = new MemoryStream(); await content.CopyToAsync(m, ct); Files[name] = (m.ToArray(), type); }
        public Task<AvatarFile> OpenReadAsync(string name, CancellationToken ct = default) { var value = Files[name]; return Task.FromResult(new AvatarFile(new MemoryStream(value.Bytes), value.Type)); }
        public Task DeleteAsync(string name, CancellationToken ct = default) { Files.Remove(name); return Task.CompletedTask; }
    }
    private sealed class Log : IAccountOperationLogger { public void AvatarCleanupFailed(Guid id, string operation) { } }
    private sealed class Unit : IUnitOfWork { public int Saves; public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(++Saves); }
}
