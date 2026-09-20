using System.Net;
using System.Net.Http.Json;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AuthService.Tests;

public sealed class SecurityHardeningIntegrationTests
{
    [Fact]
    public async Task LoginLimiter_AllowsNormalTraffic_ThenReturnsUnified429()
    {
        await using var factory = new Factory();
        var client = factory.CreateClient();
        HttpResponseMessage? response = null;
        for (var index = 0; index < 11; index++)
            response = await client.PostAsJsonAsync("/api/auth/login", new
            { email = "user@example.com", password = "password1", rememberMe = false });

        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("RATE_LIMIT_EXCEEDED", body);
        Assert.Contains("Забагато запитів", body);
        Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task RegisterAndInternalToken_HaveIndependentLimiters()
    {
        await using var factory = new Factory();
        var client = factory.CreateClient();
        HttpResponseMessage? register = null;
        for (var index = 0; index < 6; index++)
            register = await client.PostAsJsonAsync("/api/auth/register", new
            { email = "user@example.com", password = "password1", confirmPassword = "password1" });
        Assert.Equal(HttpStatusCode.TooManyRequests, register!.StatusCode);

        HttpResponseMessage? token = null;
        for (var index = 0; index < 11; index++)
            token = await client.PostAsJsonAsync("/internal/auth/token", new
            { serviceName = "AdminService", credential = "credential-admin-service" });
        Assert.Equal(HttpStatusCode.TooManyRequests, token!.StatusCode);
        Assert.Contains("RATE_LIMIT_EXCEEDED", await token.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RefreshAndLogout_RequireAllowedOrigin()
    {
        await using var factory = new Factory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await SendCookiePost(client, "/api/auth/refresh", "https://frontend.example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendCookiePost(client, "/api/auth/logout", "https://frontend.example.com")).StatusCode);
        await AssertCsrfRejected(await SendCookiePost(client, "/api/auth/refresh", "https://evil.example.com"));
        await AssertCsrfRejected(await SendCookiePost(client, "/api/auth/logout", "not-a-valid-origin"));
        await AssertCsrfRejected(await SendCookiePost(client, "/api/auth/refresh", null));
    }

    [Fact]
    public async Task Swagger_RemainsAvailable_WithoutLegacyAdminPaths()
    {
        await using var factory = new Factory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var document = await client.GetStringAsync("/swagger/v1/swagger.json");
        Assert.DoesNotContain("/api/admin/users", document, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/internal/users", document, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> SendCookiePost(HttpClient client, string path, string? origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", "perry_refresh_token=test-refresh-token");
        if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
        return await client.SendAsync(request);
    }

    private static async Task AssertCsrfRejected(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("CSRF_VALIDATION_FAILED", await response.Content.ReadAsStringAsync());
    }

    private sealed class Factory : AuthorizationIntegrationTests.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AllowedOrigins:0"] = "https://frontend.example.com" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddSingleton<IAuthService, StubAuthService>();
            });
        }
    }

    private sealed class StubAuthService : IAuthService
    {
        private static readonly UserResponse User = new() { Id = Guid.NewGuid(), Email = "user@example.com" };
        public Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
            Task.FromResult(new RegisterResponse { UserId = User.Id, Email = request.Email });
        public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default) => Task.FromResult(Session());
        public Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default) => Task.FromResult(Session());
        public Task LogoutAsync(string refreshToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(User);
        public Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ResendVerificationCodeResponse> ResendVerificationCodeAsync(ResendVerificationCodeRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompleteRegistrationResponse> CompleteRegistrationAsync(CompleteRegistrationRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default) => Task.CompletedTask;
        private static AuthResponse Session() => new()
        {
            AccessToken = "access-token", RefreshToken = "new-refresh-token",
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1), ExpiresIn = 900, User = User
        };
    }
}
