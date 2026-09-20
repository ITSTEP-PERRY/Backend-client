using System.Net.Http.Json;
using AdminService.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace AdminService.Infrastructure.AuthService;

internal sealed class ServiceTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AuthServiceOptions> options) : IServiceTokenProvider, IDisposable
{
    private readonly AuthServiceOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _refreshAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (TokenIsCurrent()) return _accessToken!;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (TokenIsCurrent()) return _accessToken!;

            HttpResponseMessage response;
            try
            {
                var client = httpClientFactory.CreateClient(AuthServiceClient.ServiceTokenClientName);
                response = await client.PostAsJsonAsync("internal/auth/token", new ServiceTokenRequest
                {
                    ServiceName = _options.ServiceName,
                    Credential = _options.ServiceCredential
                }, cancellationToken);
            }
            catch (Exception exception) when (
                exception is HttpRequestException ||
                exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw AuthServiceErrorMapper.Unavailable(exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    throw await AuthServiceErrorMapper.ServiceAuthenticationFailedAsync(response, cancellationToken);

                var token = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>(cancellationToken)
                    ?? throw AuthServiceErrorMapper.InvalidResponse();
                if (string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0)
                    throw AuthServiceErrorMapper.InvalidResponse();

                _accessToken = token.AccessToken;
                var refreshSkew = Math.Min(30, Math.Max(1, token.ExpiresIn / 10));
                _refreshAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, token.ExpiresIn - refreshSkew));
                return _accessToken;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool TokenIsCurrent() => _accessToken is not null && DateTimeOffset.UtcNow < _refreshAt;

    public void Dispose() => _lock.Dispose();
}
