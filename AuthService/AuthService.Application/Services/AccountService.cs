using AuthService.Application.DTOs.Account;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Application.Services;

public sealed class AccountService(
    IUserRepository users,
    IEmailChangeRequestRepository emailChanges,
    IPasswordHasher passwordHasher,
    IVerificationCodeService verificationCodes,
    IEmailService emailService,
    IRefreshTokenRepository refreshTokens,
    IAuthCodeConcurrencyLock codeLock,
    IAdminUserMutationLock adminMutationLock,
    IAvatarStorage avatarStorage,
    IAccountOperationLogger operationLogger,
    IUnitOfWork unitOfWork) : IAccountService
{
    private const long MaxAvatarBytes = 5L * 1024 * 1024;

    public async Task<UserResponse> ChangeNameAsync(Guid userId, ChangeNameRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await users.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserResponseMapper.Map(user);
    }

    public Task<EmailChangeResponse> StartEmailChangeAsync(Guid userId, EmailChangeStartRequest request, CancellationToken cancellationToken = default) =>
        codeLock.ExecuteAsync($"email-change-user:{userId:N}", async ct =>
        {
            var user = await GetActiveUserAsync(userId, ct);
            EnsureCurrentPassword(user, request.CurrentPassword);
            var newEmail = NormalizeEmail(request.NewEmail);
            if (newEmail == user.Email || await users.ExistsByEmailAsync(newEmail, ct))
                throw new AuthException(AuthErrorCodes.EmailAlreadyRegistered, "Email is already registered.", 409);

            var previous = await emailChanges.GetLatestByUserIdAsync(userId, ct);
            if (previous is not null && !previous.Used) previous.Used = true;
            var delivery = await CreateEmailChangeAsync(user, newEmail, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await emailService.SendEmailChangeCodeAsync(newEmail, delivery.Code, verificationCodes.Lifetime, ct);
            return Response();
        }, cancellationToken);

    public Task<EmailChangeResponse> ResendEmailChangeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        codeLock.ExecuteAsync($"email-change-user:{userId:N}", async ct =>
        {
            var user = await GetActiveUserAsync(userId, ct);
            var pending = await GetPendingAsync(userId, ct);
            var now = DateTime.UtcNow;
            var availableAt = pending.CreatedAt.Add(verificationCodes.ResendCooldown);
            if (now < availableAt)
            {
                var retry = Math.Max(1, checked((int)Math.Ceiling((availableAt - now).TotalSeconds)));
                throw new AuthException("RESEND_COOLDOWN_ACTIVE", "Resend cooldown is active.", 429);
            }

            pending.Used = true;
            var delivery = await CreateEmailChangeAsync(user, pending.NewEmail, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await emailService.SendEmailChangeCodeAsync(pending.NewEmail, delivery.Code, verificationCodes.Lifetime, ct);
            return Response();
        }, cancellationToken);

    public async Task VerifyEmailChangeAsync(Guid userId, EmailChangeVerifyRequest request, CancellationToken cancellationToken = default)
    {
        var initial = await emailChanges.GetLatestByUserIdAsync(userId, cancellationToken);
        if (initial is null || initial.Used)
            throw new AuthException(AuthErrorCodes.EmailChangeNotPending, "Email change is not pending.");

        await codeLock.ExecuteAsync($"email-change-target:{initial.NewEmail}", async ct =>
        {
            var user = await GetActiveUserAsync(userId, ct);
            var pending = await GetPendingAsync(userId, ct);
            var now = DateTime.UtcNow;
            if (pending.ExpiresAt <= now)
                throw new AuthException(AuthErrorCodes.EmailChangeCodeExpired, "Email change code expired.");
            if (pending.Attempts >= verificationCodes.MaxAttempts)
                throw new AuthException(AuthErrorCodes.EmailChangeAttemptsExceeded, "Email change attempts exceeded.", 429);
            if (!verificationCodes.VerifyCode(request.Code, pending.CodeHash))
            {
                pending.Attempts++;
                await unitOfWork.SaveChangesAsync(ct);
                throw new AuthException(AuthErrorCodes.EmailChangeCodeInvalid, "Email change code is invalid.");
            }
            if (await users.ExistsByEmailAsync(pending.NewEmail, ct))
                throw new AuthException(AuthErrorCodes.EmailAlreadyRegistered, "Email is already registered.", 409);

            user.Email = pending.NewEmail;
            user.EmailVerified = true;
            user.UpdatedAt = now;
            pending.Used = true;
            await refreshTokens.RevokeAllActiveByUserIdAsync(user.Id, now, ct);
            await users.UpdateAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        EnsureCurrentPassword(user, request.CurrentPassword);
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await refreshTokens.RevokeAllActiveByUserIdAsync(user.Id, user.UpdatedAt, cancellationToken);
        await users.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken = default) =>
        adminMutationLock.ExecuteAsync(async ct =>
        {
            var user = await GetActiveUserAsync(userId, ct);
            EnsureCurrentPassword(user, request.CurrentPassword);
            if (!string.Equals(request.Confirmation, "DELETE", StringComparison.Ordinal))
                throw new AuthException(AuthErrorCodes.AccountDeleteForbidden, "Account deletion confirmation is invalid.");
            if (user.Role == UserRole.Admin && await users.CountActiveAdminsAsync(ct) <= 1)
                throw new AuthException(AuthErrorCodes.LastAdminProtection, "Last active administrator cannot be deleted.", 409);
            var now = DateTime.UtcNow;
            user.Status = UserStatus.Deleted;
            user.UpdatedAt = now;
            await refreshTokens.RevokeAllActiveByUserIdAsync(user.Id, now, ct);
            await users.UpdateAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public async Task<string> UploadAvatarAsync(Guid userId, Stream content, long length, string contentType, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        if (length <= 0) throw new AuthException(AuthErrorCodes.AvatarContentInvalid, "Avatar content is invalid.");
        if (length > MaxAvatarBytes) throw new AuthException(AuthErrorCodes.AvatarTooLarge, "Avatar is too large.", 413);
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => throw new AuthException(AuthErrorCodes.AvatarTypeUnsupported, "Avatar type is unsupported.", 415)
        };

        await using var buffered = new MemoryStream(checked((int)length));
        await content.CopyToAsync(buffered, cancellationToken);
        if (buffered.Length != length || !HasValidSignature(buffered.GetBuffer(), buffered.Length, contentType))
            throw new AuthException(AuthErrorCodes.AvatarContentInvalid, "Avatar content is invalid.");
        buffered.Position = 0;

        var newBlob = $"users/{user.Id:N}/avatar-{Guid.NewGuid():N}{extension}";
        var oldBlob = user.AvatarBlobName;
        try
        {
            await avatarStorage.UploadAsync(newBlob, buffered, contentType, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch { throw StorageFailure(); }

        try
        {
            user.AvatarBlobName = newBlob;
            user.UpdatedAt = DateTime.UtcNow;
            await users.UpdateAsync(user, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await BestEffortDeleteAsync(user.Id, newBlob, "rollback-new-avatar");
            throw;
        }

        if (oldBlob is not null) await BestEffortDeleteAsync(user.Id, oldBlob, "replace-old-avatar");
        return "/api/account/avatar";
    }

    public async Task<AvatarFile> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        if (user.AvatarBlobName is null)
            throw new AuthException(AuthErrorCodes.AvatarNotFound, "Avatar was not found.", 404);
        try { return await avatarStorage.OpenReadAsync(user.AvatarBlobName, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch { throw StorageFailure(); }
    }

    public async Task DeleteAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        var oldBlob = user.AvatarBlobName;
        if (oldBlob is null) return;
        user.AvatarBlobName = null;
        user.UpdatedAt = DateTime.UtcNow;
        await users.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await BestEffortDeleteAsync(user.Id, oldBlob, "delete-avatar");
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new AuthException(AuthErrorCodes.UserNotFound, "User was not found.", 404);
        if (user.Status == UserStatus.Blocked) throw new AuthException(AuthErrorCodes.AccountBlocked, "Account is blocked.", 403);
        if (user.Status == UserStatus.Deleted) throw new AuthException(AuthErrorCodes.AccountDeleted, "Account is deleted.", 403);
        return user;
    }

    private void EnsureCurrentPassword(User user, string password)
    {
        if (!passwordHasher.Verify(password, user.PasswordHash))
            throw new AuthException(AuthErrorCodes.CurrentPasswordInvalid, "Current password is invalid.");
    }

    private async Task<EmailChangeRequest> GetPendingAsync(Guid userId, CancellationToken ct)
    {
        var pending = await emailChanges.GetLatestByUserIdAsync(userId, ct);
        return pending is null || pending.Used
            ? throw new AuthException(AuthErrorCodes.EmailChangeNotPending, "Email change is not pending.")
            : pending;
    }

    private async Task<(EmailChangeRequest Request, string Code)> CreateEmailChangeAsync(User user, string email, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var code = verificationCodes.GenerateCode();
        var entity = new EmailChangeRequest
        {
            Id = Guid.NewGuid(), UserId = user.Id, NewEmail = email,
            CodeHash = verificationCodes.HashCode(code), ExpiresAt = now.Add(verificationCodes.Lifetime),
            Attempts = 0, Used = false, CreatedAt = now, User = user
        };
        await emailChanges.AddAsync(entity, ct);
        return (entity, code);
    }

    private EmailChangeResponse Response() => new()
    {
        CodeExpiresInSeconds = checked((int)verificationCodes.Lifetime.TotalSeconds),
        ResendAvailableInSeconds = checked((int)verificationCodes.ResendCooldown.TotalSeconds)
    };

    private async Task BestEffortDeleteAsync(Guid userId, string blobName, string operation)
    {
        try { await avatarStorage.DeleteAsync(blobName, CancellationToken.None); }
        catch { operationLogger.AvatarCleanupFailed(userId, operation); }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static bool HasValidSignature(byte[] bytes, long length, string contentType) => contentType == "image/jpeg"
        ? length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff
        : length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
    private static AuthException StorageFailure() => new(AuthErrorCodes.AvatarStorageFailed, "Avatar storage is unavailable.", 503);
}
