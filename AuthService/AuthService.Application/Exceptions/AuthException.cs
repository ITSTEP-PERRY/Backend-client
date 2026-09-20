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
    public const string AccountDeleted = "ACCOUNT_DELETED";
    public const string AccountBlocked = "ACCOUNT_BLOCKED";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvalidUserRole = "INVALID_USER_ROLE";
    public const string InvalidUserStatus = "INVALID_USER_STATUS";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string SelfManagementNotAllowed = "SELF_MANAGEMENT_NOT_ALLOWED";
    public const string LastAdminProtection = "LAST_ADMIN_PROTECTION";
    public const string CurrentPasswordInvalid = "CURRENT_PASSWORD_INVALID";
    public const string EmailAlreadyRegistered = "EMAIL_ALREADY_REGISTERED";
    public const string EmailChangeCodeInvalid = "EMAIL_CHANGE_CODE_INVALID";
    public const string EmailChangeCodeExpired = "EMAIL_CHANGE_CODE_EXPIRED";
    public const string EmailChangeAttemptsExceeded = "EMAIL_CHANGE_ATTEMPTS_EXCEEDED";
    public const string EmailChangeNotPending = "EMAIL_CHANGE_NOT_PENDING";
    public const string AvatarTooLarge = "AVATAR_TOO_LARGE";
    public const string AvatarTypeUnsupported = "AVATAR_TYPE_UNSUPPORTED";
    public const string AvatarContentInvalid = "AVATAR_CONTENT_INVALID";
    public const string AvatarNotFound = "AVATAR_NOT_FOUND";
    public const string AvatarStorageFailed = "AVATAR_STORAGE_FAILED";
    public const string AccountDeleteForbidden = "ACCOUNT_DELETE_FORBIDDEN";
}
