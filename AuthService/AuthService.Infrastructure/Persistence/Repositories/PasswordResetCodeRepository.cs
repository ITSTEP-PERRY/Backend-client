using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetCodeRepository(AuthDbContext dbContext) : IPasswordResetCodeRepository
{
    public Task<PasswordResetCode?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.PasswordResetCodes.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(PasswordResetCode code, CancellationToken cancellationToken = default) =>
        await dbContext.PasswordResetCodes.AddAsync(code, cancellationToken);
}
