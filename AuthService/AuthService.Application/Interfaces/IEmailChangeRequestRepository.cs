using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IEmailChangeRequestRepository
{
    Task<EmailChangeRequest?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(EmailChangeRequest request, CancellationToken cancellationToken = default);
}
