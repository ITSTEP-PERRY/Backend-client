using AuthService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace AuthService.Infrastructure.Email;

public class DevelopmentEmailService : IEmailService
{
    private readonly ILogger<DevelopmentEmailService> _logger;

    public DevelopmentEmailService(
        ILogger<DevelopmentEmailService> logger,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Development email service can only run in Development.");
        _logger = logger;
    }

    public Task SendVerificationCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Development verification email suppressed; plaintext code is not logged.");

        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Development password-reset email suppressed; plaintext code is not logged.");

        return Task.CompletedTask;
    }

    public Task SendEmailChangeCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Development email-change message suppressed; recipient and plaintext code are not logged.");
        return Task.CompletedTask;
    }
}
