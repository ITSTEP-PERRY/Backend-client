using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IPasswordResetCodeRepository
{
    Task<PasswordResetCode?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(PasswordResetCode code, CancellationToken cancellationToken = default);
}
