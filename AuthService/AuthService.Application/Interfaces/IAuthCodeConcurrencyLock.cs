namespace AuthService.Application.Interfaces;

public interface IAuthCodeConcurrencyLock
{
    Task<T> ExecuteAsync<T>(
        string normalizedEmail,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}
