using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task RevokeAllActiveByUserIdAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken = default);
}
