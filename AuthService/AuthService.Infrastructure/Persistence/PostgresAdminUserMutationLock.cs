using System.Data;
using AuthService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public sealed class PostgresAdminUserMutationLock(AuthDbContext dbContext) : IAdminUserMutationLock
{
    private const long LockKey = 0x504552525941444D; // "PERRYADM"

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        var acquired = false;
        if (shouldClose) await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock({LockKey})", cancellationToken);
            acquired = true;
            return await action(cancellationToken);
        }
        finally
        {
            try
            {
                if (acquired)
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
            }
            finally
            {
                if (shouldClose) await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
