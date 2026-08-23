using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuthService.Tests;

public sealed class PostgresAuthCodeConcurrencyLockTests
{
    [Fact]
    public async Task SameEmail_IsSerializedAcrossIndependentDbContexts_WhenPostgresIsConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable("AUTH_SERVICE_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var firstContext = new AuthDbContext(options);
        await using var secondContext = new AuthDbContext(options);
        var firstLock = new PostgresAuthCodeConcurrencyLock(firstContext);
        var secondLock = new PostgresAuthCodeConcurrencyLock(secondContext);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = false;

        var firstTask = firstLock.ExecuteAsync(
            "same@example.com",
            async _ =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
                return true;
            });

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var secondTask = secondLock.ExecuteAsync(
            "same@example.com",
            _ =>
            {
                secondEntered = true;
                return Task.FromResult(true);
            });

        await Task.Delay(200);
        Assert.False(secondEntered);

        releaseFirst.SetResult();
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(secondEntered);
    }
}
