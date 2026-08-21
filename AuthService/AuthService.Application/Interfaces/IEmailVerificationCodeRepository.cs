using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IEmailVerificationCodeRepository
{
    Task<EmailVerificationCode?> GetLatestByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        EmailVerificationCode verificationCode,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        EmailVerificationCode verificationCode,
        CancellationToken cancellationToken = default);
}
