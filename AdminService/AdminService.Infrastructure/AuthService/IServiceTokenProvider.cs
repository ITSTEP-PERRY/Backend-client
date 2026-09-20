namespace AdminService.Infrastructure.AuthService;

internal interface IServiceTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}
