using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IEmailVerificationCodeRepository _codes;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IVerificationCodeService _verificationCodes;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordResetCodeRepository _passwordResetCodes;
    private readonly IRegistrationTokenService _registrationTokens;
    private readonly IAuthCodeConcurrencyLock _codeConcurrencyLock;

    public AuthService(IUserRepository users, IEmailVerificationCodeRepository codes,
        IPasswordHasher passwordHasher, IVerificationCodeService verificationCodes,
        IEmailService emailService, IUnitOfWork unitOfWork, IJwtService jwtService,
        IRefreshTokenService refreshTokenService, IRefreshTokenRepository refreshTokens,
        IPasswordResetCodeRepository passwordResetCodes, IRegistrationTokenService registrationTokens,
        IAuthCodeConcurrencyLock codeConcurrencyLock)
    {
        _users = users;
        _codes = codes;
        _passwordHasher = passwordHasher;
        _verificationCodes = verificationCodes;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _refreshTokens = refreshTokens;
        _passwordResetCodes = passwordResetCodes;
        _registrationTokens = registrationTokens;
        _codeConcurrencyLock = codeConcurrencyLock;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.ExistsByEmailAsync(email, cancellationToken))
            throw new DuplicateEmailException(email);

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, PasswordHash = _passwordHasher.Hash(request.Password),
            EmailVerified = false, FirstName = null, LastName = null, CreatedAt = now, UpdatedAt = now
        };
        var plaintextCode = _verificationCodes.GenerateCode();
        var code = new EmailVerificationCode
        {
            Id = Guid.NewGuid(), UserId = user.Id, CodeHash = _verificationCodes.HashCode(plaintextCode),
            ExpiresAt = now.Add(_verificationCodes.Lifetime), Attempts = 0, Used = false,
            CreatedAt = now, User = user
        };

        await _users.AddAsync(user, cancellationToken);
        await _codes.AddAsync(code, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _emailService.SendVerificationCodeAsync(email, plaintextCode, _verificationCodes.Lifetime, cancellationToken);

        return new RegisterResponse
        {
            UserId = user.Id, Email = email, RequiresEmailVerification = true,
            CodeExpiresInSeconds = checked((int)_verificationCodes.Lifetime.TotalSeconds)
        };
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        return await _codeConcurrencyLock.ExecuteAsync(
            email,
            ct => VerifyEmailLockedAsync(request, email, ct),
            cancellationToken);
    }

    private async Task<VerifyEmailResponse> VerifyEmailLockedAsync(
        VerifyEmailRequest request,
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            throw new EmailVerificationException(
                EmailVerificationErrorCodes.UserNotFound,
                "Unable to verify email.");
        }

        if (user.EmailVerified)
        {
            throw new EmailVerificationException(
                EmailVerificationErrorCodes.EmailAlreadyVerified,
                "Email is already verified.");
        }

        var verificationCode = await _codes.GetLatestByUserIdAsync(
            user.Id,
            cancellationToken);

        if (verificationCode is null || verificationCode.Used)
        {
            throw new EmailVerificationException(
                EmailVerificationErrorCodes.CodeNotFound,
                "Verification code was not found.");
        }

        var now = DateTime.UtcNow;
        if (now >= verificationCode.ExpiresAt)
        {
            throw new EmailVerificationException(
                EmailVerificationErrorCodes.CodeExpired,
                "Verification code has expired.");
        }

        if (verificationCode.Attempts >= _verificationCodes.MaxAttempts)
        {
            throw new EmailVerificationException(
                EmailVerificationErrorCodes.AttemptsExceeded,
                "Maximum verification attempts exceeded.");
        }

        if (!_verificationCodes.VerifyCode(request.Code, verificationCode.CodeHash))
        {
            verificationCode.Attempts++;
            await _codes.UpdateAsync(verificationCode, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new EmailVerificationException(
                EmailVerificationErrorCodes.InvalidCode,
                "Verification code is invalid.");
        }

        verificationCode.Used = true;
        user.EmailVerified = true;
        user.UpdatedAt = now;

        await _codes.UpdateAsync(verificationCode, cancellationToken);
        await _users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VerifyEmailResponse
        {
            EmailVerified = true,
            Email = user.Email,
            RegistrationToken = _registrationTokens.Generate(user.Id)
        };
    }
    public async Task<ResendVerificationCodeResponse> ResendVerificationCodeAsync(
        ResendVerificationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var delivery = await _codeConcurrencyLock.ExecuteAsync(
            email,
            ct => ResendVerificationCodeLockedAsync(email, ct),
            cancellationToken);

        await _emailService.SendVerificationCodeAsync(
            delivery.Email,
            delivery.PlaintextCode,
            _verificationCodes.Lifetime,
            cancellationToken);

        return delivery.Response;
    }

    private async Task<VerificationCodeDelivery> ResendVerificationCodeLockedAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            throw new EmailVerificationException(
                EmailVerificationErrorCodes.UserNotFound,
                "User was not found.");
        }

        if (user.EmailVerified)
        {
            throw new EmailVerificationException(
                EmailVerificationErrorCodes.EmailAlreadyVerified,
                "Email is already verified.");
        }

        var latestCode = await _codes.GetLatestByUserIdAsync(
            user.Id,
            cancellationToken);
        var now = DateTime.UtcNow;

        if (latestCode is not null)
        {
            var nextAvailableAt = latestCode.CreatedAt.Add(
                _verificationCodes.ResendCooldown);

            if (now < nextAvailableAt)
            {
                var retryAfterSeconds = Math.Max(
                    1,
                    checked((int)Math.Ceiling(
                        (nextAvailableAt - now).TotalSeconds)));

                throw new EmailVerificationException(
                    EmailVerificationErrorCodes.ResendCooldownActive,
                    "Verification code resend cooldown is active.",
                    retryAfterSeconds);
            }

            if (!latestCode.Used)
            {
                latestCode.Used = true;
                await _codes.UpdateAsync(latestCode, cancellationToken);
            }
        }

        var plaintextCode = _verificationCodes.GenerateCode();
        var newCode = new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeHash = _verificationCodes.HashCode(plaintextCode),
            ExpiresAt = now.Add(_verificationCodes.Lifetime),
            Attempts = 0,
            Used = false,
            CreatedAt = now,
            User = user
        };

        await _codes.AddAsync(newCode, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VerificationCodeDelivery(user.Email, plaintextCode, new ResendVerificationCodeResponse
        {
            Email = user.Email,
            CodeExpiresInSeconds = checked(
                (int)_verificationCodes.Lifetime.TotalSeconds),
            ResendAvailableInSeconds = checked(
                (int)_verificationCodes.ResendCooldown.TotalSeconds)
        });
    }
    public async Task<CompleteRegistrationResponse> CompleteRegistrationAsync(CompleteRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId;
        try { userId = _registrationTokens.Validate(request.RegistrationToken); }
        catch { throw new AuthException(AuthErrorCodes.InvalidRegistrationToken, "Registration token is invalid or expired.", 403); }

        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException(AuthErrorCodes.InvalidRegistrationToken, "Registration token is invalid or expired.", 403);
        if (!user.EmailVerified)
            throw new AuthException(AuthErrorCodes.EmailNotVerified, "Email is not verified.", 403);
        if (IsRegistrationCompleted(user))
            throw new AuthException(AuthErrorCodes.RegistrationAlreadyCompleted, "Registration is already completed.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new CompleteRegistrationResponse { RegistrationCompleted = true, User = MapUser(user) };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new AuthException(AuthErrorCodes.InvalidCredentials, "Invalid email or password.", 401);
        if (!user.EmailVerified)
            throw new AuthException(AuthErrorCodes.EmailNotVerified, "Email is not verified.", 403);
        if (!IsRegistrationCompleted(user))
            throw new AuthException(AuthErrorCodes.RegistrationNotCompleted, "Registration is not completed.", 403);
        return await CreateSessionAsync(user, request.RememberMe, cancellationToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "Refresh token is invalid.", 401);
        var token = await _refreshTokens.GetByHashAsync(_refreshTokenService.HashToken(request.RefreshToken), cancellationToken);
        if (token is null || token.RevokedAt is not null)
            throw new AuthException(AuthErrorCodes.InvalidRefreshToken, "Refresh token is invalid.", 401);
        if (token.ExpiresAt <= DateTime.UtcNow)
            throw new AuthException(AuthErrorCodes.RefreshTokenExpired, "Refresh token has expired.", 401);

        token.RevokedAt = DateTime.UtcNow;
        var remainingLifetime = token.ExpiresAt - token.CreatedAt;
        var rememberMe = _refreshTokenService.IsRememberMeLifetime(remainingLifetime);
        var response = await CreateSessionAsync(token.User, rememberMe, cancellationToken, saveChanges: false);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var token = await _refreshTokens.GetByHashAsync(_refreshTokenService.HashToken(refreshToken), cancellationToken);
        if (token is null || token.RevokedAt is not null) return;
        token.RevokedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var delivery = await _codeConcurrencyLock.ExecuteAsync(
            email,
            ct => PreparePasswordResetCodeLockedAsync(email, ct),
            cancellationToken);

        if (delivery is null) return;

        await _emailService.SendPasswordResetCodeAsync(
            delivery.Email,
            delivery.PlaintextCode,
            _verificationCodes.Lifetime,
            cancellationToken);
    }

    private async Task<PasswordResetCodeDelivery?> PreparePasswordResetCodeLockedAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null) return null;
        var now = DateTime.UtcNow;
        var latest = await _passwordResetCodes.GetLatestByUserIdAsync(user.Id, cancellationToken);
        if (latest is not null && now < latest.CreatedAt.Add(_verificationCodes.ResendCooldown)) return null;
        if (latest is not null) latest.Used = true;
        var plaintextCode = _verificationCodes.GenerateCode();
        await _passwordResetCodes.AddAsync(new PasswordResetCode
        {
            Id = Guid.NewGuid(), UserId = user.Id, CodeHash = _verificationCodes.HashCode(plaintextCode),
            ExpiresAt = now.Add(_verificationCodes.Lifetime), Attempts = 0, Used = false, CreatedAt = now, User = user
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new PasswordResetCodeDelivery(user.Email, plaintextCode);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        await _codeConcurrencyLock.ExecuteAsync(
            email,
            async ct =>
            {
                await ResetPasswordLockedAsync(request, email, ct);
                return true;
            },
            cancellationToken);
    }

    private async Task ResetPasswordLockedAsync(
        ResetPasswordRequest request,
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null) throw new AuthException(AuthErrorCodes.InvalidPasswordResetCode, "Password reset code is invalid.");
        var code = await _passwordResetCodes.GetLatestByUserIdAsync(user.Id, cancellationToken);
        if (code is null || code.Used) throw new AuthException(AuthErrorCodes.InvalidPasswordResetCode, "Password reset code is invalid.");
        if (code.ExpiresAt <= DateTime.UtcNow) throw new AuthException(AuthErrorCodes.PasswordResetCodeExpired, "Password reset code has expired.");
        if (code.Attempts >= _verificationCodes.MaxAttempts) throw new AuthException(AuthErrorCodes.PasswordResetAttemptsExceeded, "Maximum password reset attempts exceeded.", 429);
        if (!_verificationCodes.VerifyCode(request.Code, code.CodeHash))
        {
            code.Attempts++;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthException(AuthErrorCodes.InvalidPasswordResetCode, "Password reset code is invalid.");
        }
        var now = DateTime.UtcNow;
        code.Used = true;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = now;
        await _refreshTokens.RevokeAllActiveByUserIdAsync(user.Id, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("USER_NOT_FOUND", "User was not found.", 404);
        return MapUser(user);
    }

    private async Task<AuthResponse> CreateSessionAsync(User user, bool rememberMe, CancellationToken cancellationToken, bool saveChanges = true)
    {
        var plaintextToken = _refreshTokenService.GenerateToken();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(_refreshTokenService.GetLifetime(rememberMe));
        await _refreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = _refreshTokenService.HashToken(plaintextToken),
            CreatedAt = now, ExpiresAt = expiresAt, User = user
        }, cancellationToken);
        if (saveChanges) await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new AuthResponse
        {
            AccessToken = _jwtService.GenerateAccessToken(user), ExpiresIn = _jwtService.GetAccessTokenExpirationSeconds(),
            User = MapUser(user), RefreshToken = plaintextToken, RefreshTokenExpiresAt = expiresAt
        };
    }

    private static bool IsRegistrationCompleted(User user) =>
        !string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(user.LastName);

    private static UserResponse MapUser(User user) => new()
    {
        Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, EmailVerified = user.EmailVerified
    };

    private sealed record VerificationCodeDelivery(
        string Email,
        string PlaintextCode,
        ResendVerificationCodeResponse Response);

    private sealed record PasswordResetCodeDelivery(string Email, string PlaintextCode);
}
