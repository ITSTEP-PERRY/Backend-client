using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Application.DTOs.Internal;
using AuthService.Application.DTOs.Users;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using System.Text.Json;

namespace AuthService.Tests;

public sealed class AuthorizationIntegrationTests : IClassFixture<AuthorizationIntegrationTests.Factory>
{
    private readonly Factory _factory;
    public AuthorizationIntegrationTests(Factory factory) => _factory = factory;

    [Theory]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/api/admin/users/10000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/admin/users/10000000-0000-0000-0000-000000000001/role")]
    [InlineData("PATCH", "/api/admin/users/10000000-0000-0000-0000-000000000001/status")]
    public async Task LegacyPublicAdminApi_DoesNotExist(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "PATCH") request.Content = JsonContent.Create(new { role = "Admin", status = "Active" });
        var response = await _factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertError(response, "NOT_FOUND", "Запитаний ресурс не знайдено.");
    }

    [Fact]
    public async Task UserToken_InternalApi_IsRejected() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendUserGet(UserRole.Admin, "/internal/users")).StatusCode);

    [Fact]
    public async Task ServiceWithoutPermission_InternalGet_IsForbidden() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await SendService(HttpMethod.Get, "/internal/users", "none", "credential-none")).StatusCode);

    [Fact]
    public async Task UsersRead_InternalGet_IsAllowed() =>
        Assert.Equal(HttpStatusCode.OK, (await SendService(HttpMethod.Get, "/internal/users", "reader", "credential-reader")).StatusCode);

    [Fact]
    public async Task UsersManage_InternalPatch_IsAllowed()
    {
        var response = await SendService(HttpMethod.Patch, $"/internal/users/{Guid.NewGuid()}/status",
            "manager", "credential-manager", JsonContent.Create(new { status = "Deleted" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsersRead_InternalPatch_IsForbidden()
    {
        var response = await SendService(HttpMethod.Patch, $"/internal/users/{Guid.NewGuid()}/status",
            "reader", "credential-reader", JsonContent.Create(new { status = "Deleted" }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidServiceCredential_IsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/internal/auth/token",
            new ServiceTokenRequest { ServiceName = "reader", Credential = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredServiceToken_IsUnauthorized()
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "expired-reader"),
            new Claim(InternalAuthConstants.TokenUseClaim, InternalAuthConstants.TokenUseService),
            new Claim(InternalAuthConstants.PermissionClaim, InternalAuthConstants.UsersRead)
        };
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "test-internal-issuer", "test-internal-audience", claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10), expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-internal-signing-secret-32-chars")),
                SecurityAlgorithms.HmacSha256)));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/internal/users")).StatusCode);
    }

    [Theory]
    [InlineData("wrong-issuer", "test-user-audience", "integration-user-signing-secret-at-least-32-chars", false, "HS256")]
    [InlineData("test-user-issuer", "wrong-audience", "integration-user-signing-secret-at-least-32-chars", false, "HS256")]
    [InlineData("test-user-issuer", "test-user-audience", "different-signing-secret-at-least-32-characters", false, "HS256")]
    [InlineData("test-user-issuer", "test-user-audience", "integration-user-signing-secret-at-least-32-chars", true, "HS256")]
    [InlineData("test-user-issuer", "test-user-audience", "integration-user-signing-secret-at-least-32-chars", false, "HS384")]
    public async Task InvalidUserJwt_IsRejected(string issuer, string audience, string secret, bool expired, string algorithm)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            Factory.UserToken(UserRole.Admin, issuer, audience, secret, expired, algorithm));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ServiceToken_IsRejectedByUserAuthentication()
    {
        var client = _factory.CreateClient();
        var tokenResponse = await client.PostAsJsonAsync("/internal/auth/token", new ServiceTokenRequest
        { ServiceName = "AdminService", Credential = "credential-admin-service" });
        var token = await tokenResponse.Content.ReadFromJsonAsync<ServiceTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    private async Task<HttpResponseMessage> SendUserGet(UserRole role, string path)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.UserToken(role));
        return await client.GetAsync(path);
    }

    private async Task<HttpResponseMessage> SendUser(HttpMethod method, string path, UserRole role, HttpContent? content = null)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Factory.UserToken(role));
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendService(HttpMethod method, string path, string serviceName,
        string credential, HttpContent? content = null)
    {
        var client = _factory.CreateClient();
        var tokenResponse = await client.PostAsJsonAsync("/internal/auth/token", new ServiceTokenRequest
        { ServiceName = serviceName, Credential = credential });
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<ServiceTokenResponse>();
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        if (method != HttpMethod.Get)
            request.Headers.Add("X-Admin-Actor-Id", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task AssertError(HttpResponseMessage response, string code, string message)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(message, json.RootElement.GetProperty("message").GetString());
    }

    public class Factory : WebApplicationFactory<Program>
    {
        private const string UserSecret = "integration-user-signing-secret-at-least-32-chars";
        private const string InternalSecret = "integration-internal-signing-secret-32-chars";

        public Factory()
        {
            foreach (var setting in Configuration())
                Environment.SetEnvironmentVariable(setting.Key.Replace(":", "__"), setting.Value);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserManagementService>();
                services.AddSingleton<IUserManagementService, StubUsers>();
            });
        }

        public static string UserToken(
            UserRole role,
            string issuer = "test-user-issuer",
            string audience = "test-user-audience",
            string secret = UserSecret,
            bool expired = false,
            string algorithm = SecurityAlgorithms.HmacSha256)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim("role", role.ToString())
            };
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer, audience, claims,
                notBefore: expired ? DateTime.UtcNow.AddMinutes(-10) : null,
                expires: expired ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), algorithm)));
        }

        private static Dictionary<string, string?> Configuration() => new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Jwt:Issuer"] = "test-user-issuer", ["Jwt:Audience"] = "test-user-audience",
            ["Jwt:SigningSecret"] = UserSecret, ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "1", ["Jwt:RememberMeRefreshTokenDays"] = "30",
            ["InternalJwt:Issuer"] = "test-internal-issuer", ["InternalJwt:Audience"] = "test-internal-audience",
            ["InternalJwt:SigningSecret"] = InternalSecret, ["InternalJwt:AccessTokenMinutes"] = "5",
            ["InternalJwt:Services:0:Name"] = "none",
            ["InternalJwt:Services:0:CredentialHash"] = Hash("credential-none"),
            ["InternalJwt:Services:1:Name"] = "reader",
            ["InternalJwt:Services:1:CredentialHash"] = Hash("credential-reader"),
            ["InternalJwt:Services:1:Permissions:0"] = InternalAuthConstants.UsersRead,
            ["InternalJwt:Services:2:Name"] = "manager",
            ["InternalJwt:Services:2:CredentialHash"] = Hash("credential-manager"),
            ["InternalJwt:Services:2:Permissions:0"] = InternalAuthConstants.UsersManage,
            ["InternalJwt:Services:3:Name"] = "AdminService",
            ["InternalJwt:Services:3:CredentialHash"] = Hash("credential-admin-service"),
            ["InternalJwt:Services:3:Permissions:0"] = InternalAuthConstants.UsersRead,
            ["InternalJwt:Services:3:Permissions:1"] = InternalAuthConstants.UsersManage,
            ["VerificationCodes:HashSecret"] = "test-verification-secret",
            ["Resend:ApiKey"] = "test-key", ["Resend:FromEmail"] = "test@example.com", ["Resend:FromName"] = "Test"
        };

        private static string Hash(string credential) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
    }

    private sealed class StubUsers : IUserManagementService
    {
        private static readonly UserManagementResponse User = new()
        { Id = Guid.NewGuid(), Email = "safe@example.com", Role = UserRole.User, Status = UserStatus.Active };
        public Task<PaginatedUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken ct = default) =>
            Task.FromResult(new PaginatedUsersResponse { Page = request.Page, PageSize = request.PageSize, TotalCount = 1, Items = [User] });
        public Task<UserManagementResponse> GetUserAsync(Guid id, CancellationToken ct = default) => Task.FromResult(User);
        public Task<UserManagementResponse> UpdateRoleAsync(Guid actorId, Guid id, UserRole role, CancellationToken ct = default) =>
            actorId == Guid.Empty
                ? throw new AuthException("ACTOR_REQUIRED", "Actor is required.")
                : Task.FromResult(new UserManagementResponse { Id = id, Role = role, Status = UserStatus.Active });
        public Task<UserManagementResponse> UpdateStatusAsync(Guid actorId, Guid id, UserStatus status, CancellationToken ct = default) =>
            actorId == Guid.Empty
                ? throw new AuthException("ACTOR_REQUIRED", "Actor is required.")
                : Task.FromResult(new UserManagementResponse { Id = id, Role = UserRole.User, Status = status });
    }
}
