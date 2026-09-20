using System.Net;
using System.Text;
using AdminService.Application.Exceptions;
using AdminService.Infrastructure.AuthService;
using Xunit;

namespace AuthService.Tests;

public sealed class AdminServiceDownstreamErrorTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task MalformedAuthServiceResponse_ReturnsSafeBadGateway(HttpStatusCode status)
    {
        using var httpClient = new HttpClient(new StubHandler(status)) { BaseAddress = new Uri("https://auth.test/") };
        var client = new AuthServiceClient(httpClient, new StubTokenProvider());

        var exception = await Assert.ThrowsAsync<AdminServiceException>(() => client.GetUserAsync(Guid.NewGuid()));

        Assert.Equal(502, exception.StatusCode);
        Assert.Equal("AUTH_SERVICE_INVALID_RESPONSE", exception.ErrorCode);
        Assert.Equal("Сервіс авторизації повернув некоректну відповідь.", exception.Message);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubTokenProvider : IServiceTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-token");
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{not-json secret}", Encoding.UTF8, "application/json")
            });
    }
}
