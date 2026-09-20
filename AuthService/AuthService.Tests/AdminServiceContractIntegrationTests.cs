using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using AdminService.Application.Models;
using AdminService.Infrastructure.AuthService;
using Microsoft.Extensions.Options;
using Xunit;
using AuthTokenRequest = AuthService.Application.DTOs.Internal.ServiceTokenRequest;
using AuthTokenResponse = AuthService.Application.DTOs.Internal.ServiceTokenResponse;

namespace AuthService.Tests;

public sealed class AdminServiceContractIntegrationTests
{
    [Fact]
    public async Task AdminServiceClient_UsesServiceTokenForCompleteUsersContract()
    {
        await using var authFactory = new AuthorizationIntegrationTests.Factory();
        using var authHttpClient = authFactory.CreateClient();

        var tokenResponse = await authHttpClient.PostAsJsonAsync("/internal/auth/token", new AuthTokenRequest
        {
            ServiceName = "AdminService",
            Credential = "credential-admin-service"
        });
        tokenResponse.EnsureSuccessStatusCode();
        var issuedToken = await tokenResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issuedToken!.AccessToken);

        Assert.Contains(jwt.Claims, claim => claim.Type == "token_use" && claim.Value == "service");
        Assert.Contains(jwt.Claims, claim => claim.Type == "permission" && claim.Value == "users.read");
        Assert.Contains(jwt.Claims, claim => claim.Type == "permission" && claim.Value == "users.manage");

        var options = Options.Create(new AuthServiceOptions
        {
            BaseUrl = authHttpClient.BaseAddress!.ToString(),
            TimeoutSeconds = 5,
            ServiceName = "AdminService",
            ServiceCredential = "credential-admin-service"
        });
        var clients = new TestHttpClientFactory(authHttpClient);
        using var tokenProvider = new ServiceTokenProvider(clients, options);
        var adminClient = new AuthServiceClient(authHttpClient, tokenProvider);

        var page = await adminClient.GetUsersAsync(new AdminService.Application.DTOs.Users.GetUsersRequest());
        var user = Assert.Single(page.Items);
        Assert.Equal(user.Id, (await adminClient.GetUserAsync(user.Id)).Id);
        var actorId = Guid.NewGuid();
        Assert.Equal(UserRole.Admin, (await adminClient.UpdateRoleAsync(actorId, user.Id, UserRole.Admin)).Role);
        Assert.Equal(UserStatus.Deleted, (await adminClient.UpdateStatusAsync(actorId, user.Id, UserStatus.Deleted)).Status);
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
