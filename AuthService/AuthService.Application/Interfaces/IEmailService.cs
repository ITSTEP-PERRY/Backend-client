namespace AuthService.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetCodeAsync(
        string email,
        string code,
        TimeSpan codeLifetime,
        CancellationToken cancellationToken = default);
}
