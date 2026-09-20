using System.Threading.RateLimiting;
using AuthService.Api.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthService.Api.Security;

internal static class RateLimitingConfiguration
{
    public const string Register = "register";
    public const string Login = "login";
    public const string VerifyEmail = "verify-email";
    public const string ResendVerification = "resend-verification";
    public const string PasswordRecovery = "password-recovery";
    public const string Refresh = "refresh";
    public const string InternalToken = "internal-token";
    public const string AccountName = "account-name";
    public const string EmailChangeRequest = "email-change-request";
    public const string EmailChangeResend = "email-change-resend";
    public const string EmailChangeVerify = "email-change-verify";
    public const string ChangePassword = "change-password";
    public const string DeleteAccount = "delete-account";
    public const string AvatarUpload = "avatar-upload";
    public const string AvatarDelete = "avatar-delete";

    public static void Configure(RateLimiterOptions options)
    {
        Add(options, Register, 5, TimeSpan.FromMinutes(10));
        Add(options, Login, 10, TimeSpan.FromMinutes(1));
        Add(options, VerifyEmail, 10, TimeSpan.FromMinutes(5));
        Add(options, ResendVerification, 3, TimeSpan.FromMinutes(10));
        Add(options, PasswordRecovery, 5, TimeSpan.FromMinutes(10));
        Add(options, Refresh, 30, TimeSpan.FromMinutes(1));
        Add(options, InternalToken, 10, TimeSpan.FromMinutes(1));
        Add(options, AccountName, 20, TimeSpan.FromMinutes(10), true);
        Add(options, EmailChangeRequest, 5, TimeSpan.FromMinutes(10), true);
        Add(options, EmailChangeResend, 3, TimeSpan.FromMinutes(10), true);
        Add(options, EmailChangeVerify, 10, TimeSpan.FromMinutes(5), true);
        Add(options, ChangePassword, 5, TimeSpan.FromMinutes(10), true);
        Add(options, DeleteAccount, 3, TimeSpan.FromMinutes(10), true);
        Add(options, AvatarUpload, 10, TimeSpan.FromMinutes(10), true);
        Add(options, AvatarDelete, 10, TimeSpan.FromMinutes(10), true);
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            await context.HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse
            {
                Code = "RATE_LIMIT_EXCEEDED",
                Message = "Забагато запитів. Спробуйте ще раз пізніше."
            }, cancellationToken);
        };
    }

    private static void Add(RateLimiterOptions options, string name, int limit, TimeSpan window, bool authenticated = false) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            authenticated
                ? context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client"
                : context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit, Window = window, QueueLimit = 0, AutoReplenishment = true
            }));
}
