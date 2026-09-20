namespace AuthService.Application.Interfaces;

public interface IAdminUserMutationLock
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
