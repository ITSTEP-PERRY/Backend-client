using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AuthService.Tests;

public sealed class ApiContractIntegrationTests
{
    [Fact]
    public async Task ConfiguredOrigin_AndPreflight_AreAllowedWithCredentialsHeadersAndMethods()
    {
        await using var factory = new ConfiguredCorsFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://frontend.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://frontend.example.com", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        var headers = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
        Assert.Contains("authorization", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("content-type", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyOrigins_DoesNotAllowCrossOriginRequests()
    {
        await using var factory = new AuthorizationIntegrationTests.Factory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "https://unconfigured.example.com");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task ValidationError_UsesApiErrorContract()
    {
        await using var factory = new AuthorizationIntegrationTests.Factory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJson(response);
        Assert.Equal("VALIDATION_ERROR", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Перевірте правильність введених даних.", json.RootElement.GetProperty("message").GetString());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("email", out _));
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task UnknownRoute_UsesNotFoundContract()
    {
        await using var factory = new AuthorizationIntegrationTests.Factory();
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertError(response, "NOT_FOUND", "Запитаний ресурс не знайдено.");
    }

    [Theory]
    [InlineData("/api/auth/register", HttpStatusCode.Conflict, "EMAIL_ALREADY_REGISTERED")]
    [InlineData("/api/auth/resend-verification-code", HttpStatusCode.TooManyRequests, "RESEND_COOLDOWN_ACTIVE")]
    [InlineData("/api/auth/login", HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    public async Task Exceptions_UseApiErrorContract(string path, HttpStatusCode status, string code)
    {
        await using var factory = new ErrorFactory();
        var body = path.EndsWith("register")
            ? new { email = "user@example.com", password = "password1", confirmPassword = "password1" }
            : path.EndsWith("login")
                ? new { email = "user@example.com", password = "password1", rememberMe = false }
                : (object)new { email = "user@example.com" };
        var response = await factory.CreateClient().PostAsJsonAsync(path, body);
        Assert.Equal(status, response.StatusCode);
        await AssertError(response, code, null);
        if (status == HttpStatusCode.TooManyRequests)
        {
            using var json = await ReadJson(response);
            Assert.Equal(60, json.RootElement.GetProperty("retryAfterSeconds").GetInt32());
            Assert.Equal("60", response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString() ??
                               response.Headers.GetValues("Retry-After").Single());
        }
    }

    private static async Task AssertError(HttpResponseMessage response, string code, string? message)
    {
        using var json = await ReadJson(response);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
        if (message is not null) Assert.Equal(message, json.RootElement.GetProperty("message").GetString());
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private sealed class ConfiguredCorsFactory : AuthorizationIntegrationTests.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AllowedOrigins:0"] = "https://frontend.example.com" }));
        }
    }

    private sealed class ErrorFactory : AuthorizationIntegrationTests.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuthService>();
                services.AddSingleton<IAuthService, ThrowingAuthService>();
            });
        }
    }

    private sealed class ThrowingAuthService : IAuthService
    {
        public Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
            throw new DuplicateEmailException(request.Email);
        public Task<ResendVerificationCodeResponse> ResendVerificationCodeAsync(ResendVerificationCodeRequest request, CancellationToken ct = default) =>
            throw new EmailVerificationException(EmailVerificationErrorCodes.ResendCooldownActive,
                "Verification code resend cooldown is active.", 60);
        public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("Sensitive internal details.");
        public Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CompleteRegistrationResponse> CompleteRegistrationAsync(CompleteRegistrationRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task LogoutAsync(string refreshToken, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
