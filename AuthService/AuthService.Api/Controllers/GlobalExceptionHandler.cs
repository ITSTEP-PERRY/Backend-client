using AuthService.Application.Exceptions;
using AuthService.Api.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace AuthService.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, message, retryAfterSeconds) = exception switch
        {
            AuthException e => (e.StatusCode, e.ErrorCode, e.Message, (int?)null),
            DuplicateEmailException => (409, "DUPLICATE_EMAIL", "A user with this email already exists.", null),
            EmailVerificationException e when e.ErrorCode == EmailVerificationErrorCodes.UserNotFound => (404, e.ErrorCode, e.Message, e.RetryAfterSeconds),
            EmailVerificationException e when e.ErrorCode == EmailVerificationErrorCodes.ResendCooldownActive => (429, e.ErrorCode, e.Message, e.RetryAfterSeconds),
            EmailVerificationException e => (400, e.ErrorCode, e.Message, e.RetryAfterSeconds),
            _ => (500, "INTERNAL_ERROR", "An unexpected error occurred.", null)
        };
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");

        context.Response.StatusCode = status;
        if (retryAfterSeconds.HasValue)
            context.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Code = code, Message = message, RetryAfterSeconds = retryAfterSeconds
        }, cancellationToken);
        return true;
    }
}
