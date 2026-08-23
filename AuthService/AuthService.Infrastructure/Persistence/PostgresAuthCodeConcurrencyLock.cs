using System.Data;
using AuthService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public sealed class PostgresAuthCodeConcurrencyLock(AuthDbContext dbContext)
    : IAuthCodeConcurrencyLock
{
    public async Task<T> ExecuteAsync<T>(
        string normalizedEmail,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        var lockAcquired = false;

        if (shouldCloseConnection)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock(hashtextextended({normalizedEmail}, 0))",
                cancellationToken);
            lockAcquired = true;

            return await action(cancellationToken);
        }
        finally
        {
            try
            {
                if (lockAcquired)
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_unlock(hashtextextended({normalizedEmail}, 0))",
                        CancellationToken.None);
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }
    }
}
