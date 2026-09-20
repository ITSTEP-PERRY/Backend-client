using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdminService.Application.DTOs.Users;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Application.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AdminService.Tests;

public sealed class AdminUsersApiTests : IClassFixture<AdminUsersApiTests.Factory>
{
    private static readonly Guid ExistingUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UnavailableUserId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid InvalidResponseUserId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid UnexpectedErrorUserId = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private readonly Factory _factory;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public AdminUsersApiTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task AnonymousRequest_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserRole_ReturnsForbidden()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/admin/users", "User");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-issuer", "test-user-audience", "admin-service-integration-user-secret-32-chars", false, "HS256")]
    [InlineData("test-user-issuer", "wrong-audience", "admin-service-integration-user-secret-32-chars", false, "HS256")]
    [InlineData("test-user-issuer", "test-user-audience", "different-admin-signing-secret-at-least-32-chars", false, "HS256")]
    [InlineData("test-user-issuer", "test-user-audience", "admin-service-integration-user-secret-32-chars", true, "HS256")]
    [InlineData("test-user-issuer", "test-user-audience", "admin-service-integration-user-secret-32-chars", false, "none")]
    public async Task InvalidJwt_IsRejected(string issuer, string audience, string secret, bool expired, string algorithm)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            Factory.UserToken("Admin", issuer: issuer, audience: audience, secret: secret, expired: expired, algorithm: algorithm));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task AdminRole_IsAllowed()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/admin/users", "Admin");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_RemainsAvailable_AndDocumentsAdminApi()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");
        Assert.Contains("/api/admin/users", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetUsers_ReturnsPage()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/admin/users?page=2&pageSize=10", "Admin");
        var body = await response.Content.ReadFromJsonAsync<PaginatedUsersResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body!.Page);
        Assert.Equal(10, body.PageSize);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task GetUser_ReturnsUser()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/admin/users/{ExistingUserId}", "Admin");
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ExistingUserId, body!.Id);
    }

    [Fact]
    public async Task GetUser_ResponseDoesNotContainSensitiveFields()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/admin/users/{ExistingUserId}", "Admin");
        var json = (await response.Content.ReadAsStringAsync()).ToLowerInvariant();
        Assert.DoesNotContain("password", json);
        Assert.DoesNotContain("refreshtoken", json);
        Assert.DoesNotContain("verification", json);
        Assert.DoesNotContain("resetcode", json);
    }

    [Fact]
    public async Task UpdateRole_ReturnsUpdatedUser()
    {
        var response = await SendAsync(HttpMethod.Patch, $"/api/admin/users/{ExistingUserId}/role", "Admin",
            JsonContent.Create(new { role = "Admin" }));
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserRole.Admin, body!.Role);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsUpdatedUser()
    {
        var response = await SendAsync(HttpMethod.Patch, $"/api/admin/users/{ExistingUserId}/status", "Admin",
            JsonContent.Create(new { status = "Deleted" }));
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserStatus.Deleted, body!.Status);
    }

    [Fact]
    public async Task PublicActorHeader_CannotSpoofJwtSubject()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/users/{ExistingUserId}/role")
        {
            Content = JsonContent.Create(new { role = "User" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Factory.UserToken("Admin", ExistingUserId));
        request.Headers.Add("X-Admin-Actor-Id", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("SELF_MANAGEMENT_NOT_ALLOWED", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AuthServiceNotFound_IsMapped()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/admin/users/{Guid.Empty}", "Admin");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AuthServiceUnavailable_ReturnsServiceUnavailable()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/admin/users/{UnavailableUserId}", "Admin");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPagination_ReturnsBadRequest()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/admin/users?page=0&pageSize=20", "Admin");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/users?sortBy=unknown")]
    [InlineData("/api/admin/users?sortDirection=sideways")]
    [InlineData("/api/admin/users?pageSize=101")]
    [InlineData("/api/admin/users?role=Owner")]
    [InlineData("/api/admin/users?emailVerified=perhaps")]
    public async Task InvalidQuery_ReturnsUkrainianValidationError(string path)
    {
        var response = await SendAsync(HttpMethod.Get, path, "Admin");
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", json);
        Assert.Contains("Перевірте правильність введених даних.", json);
    }

    [Fact]
    public async Task AuthorizationErrors_AreUkrainian()
    {
        var anonymous = await _factory.CreateClient().GetAsync("/api/admin/users");
        var forbidden = await SendAsync(HttpMethod.Get, "/api/admin/users", "User");
        Assert.Contains("Потрібна автентифікація.", await anonymous.Content.ReadAsStringAsync());
        Assert.Contains("У вас недостатньо прав", await forbidden.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("role")]
    [InlineData("status")]
    public async Task EmptyMutationBody_ReturnsValidationContract(string mutation)
    {
        var response = await SendAsync(HttpMethod.Patch, $"/api/admin/users/{ExistingUserId}/{mutation}", "Admin",
            JsonContent.Create(new { }));
        await AssertError(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task InvalidEnum_ReturnsValidationContract()
    {
        var response = await SendAsync(HttpMethod.Patch, $"/api/admin/users/{ExistingUserId}/role", "Admin",
            JsonContent.Create(new { role = "Owner" }));
        await AssertError(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task MalformedJson_ReturnsValidationContract()
    {
        var content = new StringContent("{\"role\":", Encoding.UTF8, "application/json");
        var response = await SendAsync(HttpMethod.Patch, $"/api/admin/users/{ExistingUserId}/role", "Admin", content);
        await AssertError(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task UnknownRoute_AndUnsupportedMethod_UseErrorContract()
    {
        await AssertError(await SendAsync(HttpMethod.Get, "/api/admin/missing", "Admin"), HttpStatusCode.NotFound, "NOT_FOUND");
        await AssertError(await SendAsync(HttpMethod.Post, "/api/admin/users", "Admin", JsonContent.Create(new { })),
            HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");
    }

    [Fact]
    public async Task DependencyAndUnexpectedFailures_AreSafe()
    {
        await AssertError(await SendAsync(HttpMethod.Get, $"/api/admin/users/{InvalidResponseUserId}", "Admin"),
            HttpStatusCode.BadGateway, "AUTH_SERVICE_INVALID_RESPONSE");
        await AssertError(await SendAsync(HttpMethod.Get, $"/api/admin/users/{UnexpectedErrorUserId}", "Admin"),
            HttpStatusCode.InternalServerError, "INTERNAL_ERROR");
    }

    [Fact]
    public async Task CorsPreflight_AllowsConfiguredOriginPatchAndAuthorizationHeader()
    {
        await using var factory = new CorsFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, $"/api/admin/users/{ExistingUserId}/role");
        request.Headers.Add("Origin", "https://frontend.example.com");
        request.Headers.Add("Access-Control-Request-Method", "PATCH");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://frontend.example.com", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("PATCH", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("authorization", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);

        request.Headers.Remove("Origin");
        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/admin/users");
        denied.Headers.Add("Origin", "https://unknown.example.com");
        denied.Headers.Add("Access-Control-Request-Method", "GET");
        var deniedResponse = await client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string role,
        HttpContent? content = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.UserToken(role));
        return await client.SendAsync(new HttpRequestMessage(method, path) { Content = content });
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public class Factory : WebApplicationFactory<Program>
    {
        private const string UserSecret = "admin-service-integration-user-secret-32-chars";

        public Factory()
        {
            foreach (var setting in Settings())
                Environment.SetEnvironmentVariable(setting.Key.Replace(":", "__"), setting.Value);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthServiceClient>();
                services.AddSingleton<IAuthServiceClient, FakeAuthServiceClient>();
            });
        }

        public static string UserToken(
            string role,
            Guid? subject = null,
            string issuer = "test-user-issuer",
            string audience = "test-user-audience",
            string secret = UserSecret,
            bool expired = false,
            string algorithm = SecurityAlgorithms.HmacSha256)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, (subject ?? Guid.NewGuid()).ToString()),
                new Claim("role", role)
            };
            var signingCredentials = algorithm == "none" ? null : new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), algorithm);
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer, audience, claims,
                notBefore: expired ? DateTime.UtcNow.AddMinutes(-10) : null,
                expires: expired ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
                signingCredentials: signingCredentials));
        }

        private static Dictionary<string, string?> Settings() => new()
        {
            ["Jwt:Issuer"] = "test-user-issuer",
            ["Jwt:Audience"] = "test-user-audience",
            ["Jwt:SigningSecret"] = UserSecret,
            ["AuthService:BaseUrl"] = "https://auth.test/",
            ["AuthService:TimeoutSeconds"] = "5",
            ["AuthService:ServiceName"] = "AdminService.Tests",
            ["AuthService:ServiceCredential"] = "test-only-credential"
        };
    }

    private sealed class CorsFactory : Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AllowedOrigins:0"] = "https://frontend.example.com" }));
        }
    }

    private sealed class FakeAuthServiceClient : IAuthServiceClient
    {
        public Task<PaginatedUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedUsersResponse
            {
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = 1,
                Items = [User(ExistingUserId)]
            });

        public Task<UserResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
                throw new AdminServiceException("USER_NOT_FOUND", "User was not found.", 404);
            if (id == UnavailableUserId)
                throw new AdminServiceException("AUTH_SERVICE_UNAVAILABLE", "AuthService is temporarily unavailable.", 503);
            if (id == InvalidResponseUserId)
                throw new AdminServiceException("AUTH_SERVICE_INVALID_RESPONSE", "sensitive downstream payload", 502);
            if (id == UnexpectedErrorUserId)
                throw new InvalidOperationException("connection string and secret must not leak");
            return Task.FromResult(User(id));
        }

        public Task<UserResponse> UpdateRoleAsync(Guid actorId, Guid id, UserRole role, CancellationToken cancellationToken = default)
        {
            if (actorId == id)
                throw new AdminServiceException("SELF_MANAGEMENT_NOT_ALLOWED",
                    "Ви не можете змінити роль або статус власного облікового запису.", 409);
            var user = User(id);
            user.Role = role;
            return Task.FromResult(user);
        }

        public Task<UserResponse> UpdateStatusAsync(Guid actorId, Guid id, UserStatus status, CancellationToken cancellationToken = default)
        {
            if (actorId == id)
                throw new AdminServiceException("SELF_MANAGEMENT_NOT_ALLOWED",
                    "Ви не можете змінити роль або статус власного облікового запису.", 409);
            var user = User(id);
            user.Status = status;
            return Task.FromResult(user);
        }

        private static UserResponse User(Guid id) => new()
        {
            Id = id,
            Email = "safe@example.com",
            EmailVerified = true,
            Role = UserRole.User,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
