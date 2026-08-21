using AuthService.Domain.Entities;
using AuthService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public class AuthDbContext : DbContext, IUnitOfWork
{
    public AuthDbContext(
        DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<EmailVerificationCode> EmailVerificationCodes
        => Set<EmailVerificationCode>();

    public DbSet<PasswordResetCode> PasswordResetCodes
        => Set<PasswordResetCode>();

    public DbSet<RefreshToken> RefreshTokens
        => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AuthDbContext).Assembly);
    }
}
