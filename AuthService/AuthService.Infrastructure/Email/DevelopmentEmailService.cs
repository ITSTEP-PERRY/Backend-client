using AuthService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Email;

public class DevelopmentEmailService : IEmailService
{
    private readonly ILogger<DevelopmentEmailService> _logger;

    public DevelopmentEmailService(
        ILogger<DevelopmentEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendVerificationCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EMAIL VERIFICATION CODE for {Email}: {Code}",
            email,
            code);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "PASSWORD RESET CODE for {Email}: {Code}",
            email,
            code);

        return Task.CompletedTask;
    }
}
