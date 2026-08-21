using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public class EmailVerificationCodeRepository
    : IEmailVerificationCodeRepository
{
    private readonly AuthDbContext _dbContext;

    public EmailVerificationCodeRepository(
        AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EmailVerificationCode?>
        GetLatestByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmailVerificationCodes
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(
     EmailVerificationCode verificationCode,
     CancellationToken cancellationToken = default)
    {
        await _dbContext.EmailVerificationCodes.AddAsync(
            verificationCode,
            cancellationToken);
    }

    public Task UpdateAsync(
        EmailVerificationCode verificationCode,
        CancellationToken cancellationToken = default)
    {
        _dbContext.EmailVerificationCodes.Update(
            verificationCode);

        return Task.CompletedTask;
    }
}
