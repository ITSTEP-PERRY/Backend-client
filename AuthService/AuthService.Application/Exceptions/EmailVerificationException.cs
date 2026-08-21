namespace AuthService.Application.Exceptions;

public sealed class EmailVerificationException : Exception
{
    public EmailVerificationException(
        string errorCode,
        string message,
        int? retryAfterSeconds = null)
        : base(message)
    {
        ErrorCode = errorCode;
        RetryAfterSeconds = retryAfterSeconds;
    }

    public string ErrorCode { get; }

    public int? RetryAfterSeconds { get; }
}

public static class EmailVerificationErrorCodes
{
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string CodeNotFound = "VERIFICATION_CODE_NOT_FOUND";
    public const string CodeExpired = "VERIFICATION_CODE_EXPIRED";
    public const string InvalidCode = "INVALID_VERIFICATION_CODE";
    public const string AttemptsExceeded = "VERIFICATION_ATTEMPTS_EXCEEDED";
    public const string EmailAlreadyVerified = "EMAIL_ALREADY_VERIFIED";
    public const string ResendCooldownActive = "RESEND_COOLDOWN_ACTIVE";
}
