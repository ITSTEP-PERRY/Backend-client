using AuthService.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace AuthService.Api.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, message) = exception switch
        {
            AuthException e => (e.StatusCode, e.ErrorCode, e.Message),
            DuplicateEmailException e => (409, "DUPLICATE_EMAIL", e.Message),
            EmailVerificationException e when e.ErrorCode == EmailVerificationErrorCodes.UserNotFound => (404, e.ErrorCode, e.Message),
            EmailVerificationException e when e.ErrorCode == EmailVerificationErrorCodes.ResendCooldownActive => (429, e.ErrorCode, e.Message),
            EmailVerificationException e => (400, e.ErrorCode, e.Message),
            _ => (500, "INTERNAL_ERROR", "An unexpected error occurred.")
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { code, message }, cancellationToken);
        return true;
    }
}
