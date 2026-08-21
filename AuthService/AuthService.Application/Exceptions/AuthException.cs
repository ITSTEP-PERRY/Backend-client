namespace AuthService.Application.Exceptions;

public sealed class AuthException : Exception
{
    public AuthException(string errorCode, string message, int statusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
}

public static class AuthErrorCodes
{
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string EmailNotVerified = "EMAIL_NOT_VERIFIED";
    public const string RegistrationNotCompleted = "REGISTRATION_NOT_COMPLETED";
    public const string RegistrationAlreadyCompleted = "REGISTRATION_ALREADY_COMPLETED";
    public const string InvalidRegistrationToken = "INVALID_REGISTRATION_TOKEN";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string RefreshTokenExpired = "REFRESH_TOKEN_EXPIRED";
    public const string PasswordResetCodeExpired = "PASSWORD_RESET_CODE_EXPIRED";
    public const string InvalidPasswordResetCode = "INVALID_PASSWORD_RESET_CODE";
    public const string PasswordResetAttemptsExceeded = "PASSWORD_RESET_ATTEMPTS_EXCEEDED";
}
