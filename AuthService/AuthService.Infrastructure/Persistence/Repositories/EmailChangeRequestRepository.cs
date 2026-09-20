using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public sealed class EmailChangeRequestRepository(AuthDbContext dbContext) : IEmailChangeRequestRepository
{
    public Task<EmailChangeRequest?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.EmailChangeRequests.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(EmailChangeRequest request, CancellationToken cancellationToken = default) =>
        await dbContext.EmailChangeRequests.AddAsync(request, cancellationToken);
}
